using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Editor-hosted WebSocket server that the Unimancer Node MCP server connects
    /// to. On load it discovers every <see cref="McpToolBase"/> subclass via
    /// reflection, then dispatches incoming JSON requests to the matching tool.
    ///
    /// Wire protocol (JSON per message):
    ///   request:  { id, method, params }
    ///   response: { id, result } | { id, error }
    ///
    /// Main-thread work is marshalled through <see cref="EditorApplication.update"/>
    /// so tools can safely touch the Unity API.
    /// </summary>
    [InitializeOnLoad]
    public static class McpBridge
    {
        private const string Url = "http://127.0.0.1:8090/";
        private static readonly Dictionary<string, McpToolBase> Tools = new();
        private static readonly ConcurrentQueue<Action> MainThread = new();
        private static HttpListener _listener;

        /// <summary>True while the Editor bridge is accepting WebSocket connections.</summary>
        public static bool IsListening => _listener?.IsListening ?? false;

        /// <summary>The WebSocket URL the Unimancer Node server connects to.</summary>
        public static string BridgeUrl => "ws://127.0.0.1:8090";

        static McpBridge()
        {
            DiscoverTools();
            EditorApplication.update += PumpMainThread;
            Start();
        }

        /// <summary>Reflect over the loaded assemblies to register every tool.</summary>
        private static void DiscoverTools()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => typeof(McpToolBase).IsAssignableFrom(t) && !t.IsAbstract);
            foreach (var t in types)
            {
                if (Activator.CreateInstance(t) is McpToolBase tool)
                    Tools[tool.Name] = tool;
            }
            Debug.Log($"[Unimancer] Registered {Tools.Count} tools.");
        }

        /// <summary>Drain queued main-thread actions (called each Editor tick).</summary>
        private static void PumpMainThread()
        {
            while (MainThread.TryDequeue(out var action)) action();
        }

        /// <summary>Run an action on the Unity main thread and await its JObject result.</summary>
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

        private static async void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Url);
                _listener.Start();
                Debug.Log($"[Unimancer] Bridge listening on {Url}");
                while (_listener.IsListening)
                {
                    var ctx = await _listener.GetContextAsync();
                    if (ctx.Request.IsWebSocketRequest)
                        _ = HandleSocket(ctx);
                    else
                        ctx.Response.Close();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Unimancer] Bridge stopped: {e.Message}");
            }
        }

        private static async Task HandleSocket(HttpListenerContext ctx)
        {
            var wsCtx = await ctx.AcceptWebSocketAsync(null);
            var socket = wsCtx.WebSocket;
            var buffer = new byte[64 * 1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var raw = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var response = await Dispatch(raw);
                var bytes = Encoding.UTF8.GetBytes(response.ToString(Newtonsoft.Json.Formatting.None));
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
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
