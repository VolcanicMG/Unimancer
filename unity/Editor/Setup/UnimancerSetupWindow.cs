using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// In-Editor setup &amp; status panel (Window → Unimancer → Setup): shows whether
    /// the bridge is live and writes a ready-to-use MCP client config. The Node
    /// server lives outside the Unity project, so the user supplies its path once
    /// (persisted in EditorPrefs); the window can then write a .mcp.json directly.
    /// </summary>
    public class UnimancerSetupWindow : EditorWindow
    {
        private const string RepoUrl = "https://github.com/VolcanicMG/Unimancer";
        private const string PrefPath = "Unimancer.NodeServerPath";
        private const string PrefWsl = "Unimancer.WrapWsl";

        private string _nodePath = "";
        private bool _wrapWsl;

        /// <summary>Open the Unimancer setup window.</summary>
        [MenuItem("Window/Unimancer/Setup")]
        public static void Open()
        {
            var win = GetWindow<UnimancerSetupWindow>(false, "Unimancer", true);
            win.minSize = new Vector2(460, 430);
            win.Show();
        }

        private void OnEnable()
        {
            _nodePath = EditorPrefs.GetString(PrefPath, "");
            _wrapWsl = EditorPrefs.GetBool(PrefWsl, false);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Unimancer 🔮", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Command the Unity engine with AI.", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            DrawStatus();
            EditorGUILayout.Space(10);

            // --- Config writer ---
            EditorGUILayout.LabelField("MCP client config", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Path to the Unimancer Node entry (src/index.js) on the machine that runs your MCP client.\n" +
                "Tip: in the repo, `node scripts/setup.mjs` prints the exact path.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _nodePath = EditorGUILayout.TextField("src/index.js path", _nodePath);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    var picked = EditorUtility.OpenFilePanel("Select Unimancer src/index.js", "", "js");
                    if (!string.IsNullOrEmpty(picked)) _nodePath = picked;
                }
            }
            _wrapWsl = EditorGUILayout.ToggleLeft("Wrap with `wsl` (Unity on Windows, Node in WSL)", _wrapWsl);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save settings")) SaveSettings();
                if (GUILayout.Button("Copy config JSON")) { EditorGUIUtility.systemCopyBuffer = BuildConfig(); ShowNotification(new GUIContent("Config copied")); }
                if (GUILayout.Button("Write .mcp.json…")) WriteConfigFile();
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open GitHub")) Application.OpenURL(RepoUrl);
                if (GUILayout.Button("Refresh status")) Repaint();
            }
        }

        /// <summary>Draw the live bridge status box.</summary>
        private static void DrawStatus()
        {
            bool live = McpBridge.IsListening;
            var prev = GUI.color;
            GUI.color = live ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
            EditorGUILayout.LabelField("Bridge", live ? "● listening" : "○ not listening");
            GUI.color = prev;
            EditorGUILayout.LabelField("Endpoint", McpBridge.BridgeUrl);
            if (!live)
            {
                EditorGUILayout.HelpBox("Bridge not listening — check the Console for [Unimancer] errors (e.g. port 8090 in use).", MessageType.Warning);
                if (GUILayout.Button("Restart bridge")) McpBridge.Restart();
            }
        }

        /// <summary>Persist the path + WSL toggle to EditorPrefs.</summary>
        private void SaveSettings()
        {
            EditorPrefs.SetString(PrefPath, _nodePath ?? "");
            EditorPrefs.SetBool(PrefWsl, _wrapWsl);
            ShowNotification(new GUIContent("Saved"));
        }

        /// <summary>Build the MCP server config JSON for the current settings.</summary>
        private string BuildConfig()
        {
            var path = string.IsNullOrEmpty(_nodePath) ? "/ABSOLUTE/PATH/TO/unimancer/src/index.js" : _nodePath;
            string cmd, args;
            if (_wrapWsl) { cmd = "wsl"; args = $"\"node\", \"{path}\""; }
            else { cmd = "node"; args = $"\"{path}\""; }
            return "{\n  \"mcpServers\": {\n    \"unimancer\": {\n" +
                   $"      \"command\": \"{cmd}\",\n      \"args\": [{args}]\n" +
                   "    }\n  }\n}";
        }

        /// <summary>Write (merge-naive) the config to a user-chosen .mcp.json.</summary>
        private void WriteConfigFile()
        {
            if (string.IsNullOrEmpty(_nodePath))
            {
                EditorUtility.DisplayDialog("Unimancer", "Set the src/index.js path first.", "OK");
                return;
            }
            var target = EditorUtility.SaveFilePanel("Write MCP config", "", ".mcp.json", "json");
            if (string.IsNullOrEmpty(target)) return;
            try
            {
                File.WriteAllText(target, BuildConfig() + "\n");
                SaveSettings();
                ShowNotification(new GUIContent("Wrote " + Path.GetFileName(target)));
                Debug.Log($"[Unimancer] Wrote MCP config to {target}");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Unimancer", "Write failed: " + e.Message, "OK");
            }
        }
    }
}
