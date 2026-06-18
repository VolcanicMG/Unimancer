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
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// In-build / Play-mode TCP bridge so an AI can drive a RUNNING game (not just
    /// the Editor). Auto-installs a hidden DontDestroyOnLoad object before the first
    /// scene loads, but ONLY in the Editor or development builds — release builds
    /// never open a socket. Same newline-delimited JSON protocol as the Editor
    /// bridge, on port 8091.
    ///
    /// On a device, reach it from the host with: adb forward tcp:8091 tcp:8091
    /// (Android builds also need the INTERNET permission).
    /// </summary>
    public class RuntimeBridge : MonoBehaviour
    {
        private const int Port = 8091;
        private static readonly Dictionary<string, RuntimeToolBase> Tools = new();
        private static readonly ConcurrentQueue<Action> Main = new();
        private static readonly Queue<string> Logs = new();
        private const int MaxLogs = 200;
        private TcpListener _listener;

        /// <summary>Recent log lines captured at runtime (newest last).</summary>
        public static string[] RecentLogs(int count)
        {
            lock (Logs)
            {
                var arr = Logs.ToArray();
                var n = Mathf.Clamp(count, 1, arr.Length);
                return arr.Skip(Math.Max(0, arr.Length - n)).ToArray();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // Editor (Play mode) or development builds only — never release.
            if (!Application.isEditor && !Debug.isDebugBuild) return;

            var go = new GameObject("[Unimancer.RuntimeBridge]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<RuntimeBridge>();
        }

        private void Awake()
        {
            DiscoverTools();
            Application.logMessageReceived += OnLog;
            StartServer();
        }

        private void Update()
        {
            while (Main.TryDequeue(out var a)) a();
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            try { _listener?.Stop(); } catch { }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            lock (Logs)
            {
                Logs.Enqueue($"[{type}] {message}");
                while (Logs.Count > MaxLogs) Logs.Dequeue();
            }
        }

        private static void DiscoverTools()
        {
            if (Tools.Count > 0) return;
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => typeof(RuntimeToolBase).IsAssignableFrom(t) && !t.IsAbstract);
            foreach (var t in types)
                if (Activator.CreateInstance(t) is RuntimeToolBase tool)
                    Tools[tool.Name] = tool;
            Debug.Log($"[Unimancer] Runtime bridge: {Tools.Count} tools.");
        }

        /// <summary>Run a function on the Unity main thread and await its result.</summary>
        public static Task<JObject> OnMainThread(Func<JObject> fn)
        {
            var tcs = new TaskCompletionSource<JObject>();
            Main.Enqueue(() =>
            {
                try { tcs.SetResult(fn()); }
                catch (Exception e) { tcs.SetException(e); }
            });
            return tcs.Task;
        }

        private async void StartServer()
        {
            try
            {
                // Bind Any so a device build is reachable via `adb forward`.
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                Debug.Log($"[Unimancer] Runtime bridge listening on tcp://0.0.0.0:{Port}");
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Handle(client);
                }
            }
            catch (ObjectDisposedException) { /* stopped */ }
            catch (Exception e) { Debug.LogWarning($"[Unimancer] Runtime bridge stopped: {e.Message}"); }
        }

        private async Task Handle(TcpClient tcp)
        {
            try
            {
                using (tcp)
                using (var stream = tcp.GetStream())
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Length == 0) continue;
                        var response = await Dispatch(line);
                        writer.WriteLine(response.ToString(Formatting.None));
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Unimancer] runtime client error: {e.Message}"); }
        }

        private async Task<JObject> Dispatch(string raw)
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
                var result = await OnMainThread(() => tool.Execute(prms));
                return new JObject { ["id"] = id, ["result"] = result };
            }
            catch (Exception e)
            {
                return new JObject { ["id"] = id, ["error"] = e.Message };
            }
        }
    }
}
