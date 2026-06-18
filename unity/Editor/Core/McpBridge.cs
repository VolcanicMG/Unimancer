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
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Editor-hosted TCP server that the Unimancer Node MCP server connects to.
    /// Messages are newline-delimited JSON (one object per line). We frame
    /// messages ourselves over a raw socket because Mono's HttpListener (Unity's
    /// Editor runtime) cannot perform WebSocket upgrades — both ends are ours, so
    /// a plain TCP line protocol is simpler and reliable.
    ///
    ///   request:  { id, method, params }
    ///   response: { id, result } | { id, error }
    ///
    /// Tool execution is marshalled onto the Unity main thread via
    /// EditorApplication.update so tools can safely touch the Unity API.
    /// </summary>
    [InitializeOnLoad]
    public static class McpBridge
    {
        private const int Port = 8090;
        private static readonly Dictionary<string, McpToolBase> Tools = new();
        private static readonly ConcurrentQueue<Action> MainThread = new();
        private static TcpListener _listener;

        /// <summary>True while the Editor bridge is accepting TCP connections.</summary>
        public static bool IsListening { get; private set; }

        /// <summary>The endpoint the Unimancer Node server connects to.</summary>
        public static string BridgeUrl => $"tcp://127.0.0.1:{Port}";

        static McpBridge()
        {
            DiscoverTools();
            EditorApplication.update += PumpMainThread;
            // Free the port cleanly across domain reloads / quit so re-init can rebind.
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
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

        /// <summary>Bind the listener and accept clients until stopped.</summary>
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
            catch (ObjectDisposedException) { /* listener stopped (reload/quit) */ }
            catch (SocketException e) { Debug.LogWarning($"[Unimancer] Bridge socket error: {e.Message}"); }
            catch (Exception e) { Debug.LogWarning($"[Unimancer] Bridge stopped: {e.Message}"); }
            finally { IsListening = false; }
        }

        /// <summary>Stop the listener and free the port.</summary>
        private static void Stop()
        {
            try { _listener?.Stop(); } catch { }
            _listener = null;
            IsListening = false;
        }

        /// <summary>Read newline-delimited JSON requests from a client and reply.</summary>
        private static async Task HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Length == 0) continue;
                        var response = await Dispatch(line);
                        await writer.WriteLineAsync(response.ToString(Formatting.None));
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Unimancer] client error: {e.Message}"); }
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
