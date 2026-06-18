using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Editor-hosted TCP server the Unimancer Node server connects to. Messages are
    /// newline-delimited JSON (one object per line) — we frame ourselves over a raw
    /// socket because Mono's HttpListener can't do WebSocket upgrades.
    ///
    ///   request:  { id, method, params }            (client -> editor)
    ///   response: { id, result } | { id, error }    (editor -> client)
    ///   event:    { event, data }                   (editor -> client, unsolicited)
    ///
    /// Events (compile finished, play-mode change, console errors) are broadcast to
    /// every connected client so the AI can react without polling. Tool execution is
    /// marshalled onto the Unity main thread.
    /// </summary>
    [InitializeOnLoad]
    public static class McpBridge
    {
        private const int Port = 8090;
        private static readonly Dictionary<string, McpToolBase> Tools = new();
        private static readonly ConcurrentQueue<Action> MainThread = new();
        private static TcpListener _listener;

        /// <summary>A connected client + a lock serializing writes to its stream.</summary>
        private sealed class Client
        {
            public StreamWriter Writer;
            public readonly object Lock = new object();
        }
        private static readonly List<Client> Clients = new();

        /// <summary>True while the Editor bridge is accepting TCP connections.</summary>
        public static bool IsListening { get; private set; }

        /// <summary>The endpoint the Unimancer Node server connects to.</summary>
        public static string BridgeUrl => $"tcp://127.0.0.1:{Port}";

        static McpBridge()
        {
            DiscoverTools();
            EditorApplication.update += PumpMainThread;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            HookEvents();
            Start();
        }

        /// <summary>Reflect over loaded assemblies to register every tool.</summary>
        private static void DiscoverTools()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => typeof(McpToolBase).IsAssignableFrom(t) && !t.IsAbstract);
            foreach (var t in types)
                if (Activator.CreateInstance(t) is McpToolBase tool)
                    Tools[tool.Name] = tool;
            Debug.Log($"[Unimancer] Registered {Tools.Count} tools.");
        }

        /// <summary>Subscribe to Editor events we forward to clients as notifications.</summary>
        private static void HookEvents()
        {
            CompilationPipeline.compilationFinished += _ =>
                Broadcast(new JObject { ["event"] = "compilation_finished" });

            EditorApplication.playModeStateChanged += state =>
                Broadcast(new JObject { ["event"] = "play_mode", ["data"] = new JObject { ["state"] = state.ToString() } });

            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    Broadcast(new JObject
                    {
                        ["event"] = "console_error",
                        ["data"] = new JObject { ["type"] = type.ToString(), ["message"] = msg },
                    });
            };
        }

        /// <summary>Drain queued main-thread actions (called each Editor tick).</summary>
        private static void PumpMainThread()
        {
            while (MainThread.TryDequeue(out var action)) action();
        }

        /// <summary>Run a function on the Unity main thread and await its result.</summary>
        public static Task<JObject> OnMainThread(Func<JObject> fn)
        {
            var tcs = new TaskCompletionSource<JObject>();
            MainThread.Enqueue(() =>
            {
                try { tcs.SetResult(fn()); }
                catch (Exception e) { tcs.SetException(e); }
            });
            return tcs.Task;
        }

        /// <summary>Write an unsolicited event line to every connected client.</summary>
        private static void Broadcast(JObject evt)
        {
            string line;
            try { line = evt.ToString(Formatting.None); }
            catch { return; }

            List<Client> snapshot;
            lock (Clients) snapshot = new List<Client>(Clients);
            foreach (var c in snapshot)
            {
                try { lock (c.Lock) { c.Writer.WriteLine(line); } }
                catch { /* drop on a dead/broken client */ }
            }
        }

        private static async void Start()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                IsListening = true;
                Debug.Log($"[Unimancer] Bridge listening on tcp://127.0.0.1:{Port}");
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClient(client);
                }
            }
            catch (ObjectDisposedException) { /* stopped on reload/quit */ }
            catch (SocketException e) { Debug.LogWarning($"[Unimancer] Bridge socket error: {e.Message}"); }
            catch (Exception e) { Debug.LogWarning($"[Unimancer] Bridge stopped: {e.Message}"); }
            finally { IsListening = false; }
        }

        private static void Stop()
        {
            try { _listener?.Stop(); } catch { }
            _listener = null;
            IsListening = false;
            lock (Clients) Clients.Clear();
        }

        /// <summary>Read newline-delimited JSON requests from a client and reply.</summary>
        private static async Task HandleClient(TcpClient tcp)
        {
            Client client = null;
            try
            {
                using (tcp)
                using (var stream = tcp.GetStream())
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    client = new Client { Writer = writer };
                    lock (Clients) Clients.Add(client);

                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Length == 0) continue;
                        var response = await Dispatch(line);
                        lock (client.Lock) { writer.WriteLine(response.ToString(Formatting.None)); }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Unimancer] client error: {e.Message}"); }
            finally { if (client != null) lock (Clients) Clients.Remove(client); }
        }

        /// <summary>Parse a request, run the named tool, and shape the response.</summary>
        private static async Task<JObject> Dispatch(string raw)
        {
            JObject req;
            try { req = JObject.Parse(raw); }
            catch (Exception e) { return new JObject { ["error"] = $"bad json: {e.Message}" }; }

            var id = req["id"];
            var method = req["method"]?.ToString();
            var prms = req["params"] as JObject ?? new JObject();

            if (method == null || !Tools.TryGetValue(method, out var tool))
                return new JObject { ["id"] = id, ["error"] = $"unknown tool: {method}" };

            try
            {
                JObject result;
                if (tool.IsAsync)
                {
                    var tcs = new TaskCompletionSource<JObject>();
                    MainThread.Enqueue(() => tool.ExecuteAsync(prms, tcs));
                    result = await tcs.Task;
                }
                else
                {
                    result = await OnMainThread(() => tool.Execute(prms));
                }
                return new JObject { ["id"] = id, ["result"] = result };
            }
            catch (Exception e)
            {
                return new JObject { ["id"] = id, ["error"] = e.Message };
            }
        }
    }
}
