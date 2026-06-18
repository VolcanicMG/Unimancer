using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// In-Editor setup &amp; status panel for Unimancer (Window → Unimancer → Setup).
    /// Shows whether the bridge is live and helps the user connect their MCP client.
    /// The Node server lives outside the Unity project, so this window can't know its
    /// absolute path — it shows a config template and points to `scripts/setup.mjs`,
    /// which prints the exact, path-filled config.
    /// </summary>
    public class UnimancerSetupWindow : EditorWindow
    {
        private const string RepoUrl = "https://github.com/VolcanicMG/Unimancer";

        /// <summary>Open the Unimancer setup window.</summary>
        [MenuItem("Window/Unimancer/Setup")]
        public static void Open()
        {
            var win = GetWindow<UnimancerSetupWindow>(false, "Unimancer", true);
            win.minSize = new Vector2(420, 360);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Unimancer 🔮", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Command the Unity engine with AI.", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            // --- Bridge status ---
            DrawStatus();
            EditorGUILayout.Space(10);

            // --- Connect steps ---
            EditorGUILayout.LabelField("Connect your MCP client", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. In the Unimancer repo, run:  guard install  (or npm install)\n" +
                "2. Run:  node scripts/setup.mjs  — it prints ready-to-paste config for\n" +
                "   Claude Code / Claude Desktop / Cursor with the correct absolute path.\n" +
                "3. Restart your MCP client. This bridge starts automatically.",
                MessageType.Info);

            if (GUILayout.Button("Copy MCP config template"))
            {
                EditorGUIUtility.systemCopyBuffer =
                    "{\n  \"mcpServers\": {\n    \"unimancer\": {\n" +
                    "      \"command\": \"node\",\n" +
                    "      \"args\": [\"/ABSOLUTE/PATH/TO/unimancer/src/index.js\"]\n" +
                    "    }\n  }\n}";
                ShowNotification(new GUIContent("Template copied — replace the path"));
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open GitHub")) Application.OpenURL(RepoUrl);
                if (GUILayout.Button("Refresh status")) Repaint();
            }
        }

        /// <summary>Draw the live bridge status box (listening + URL).</summary>
        private static void DrawStatus()
        {
            bool live = McpBridge.IsListening;
            var prev = GUI.color;
            GUI.color = live ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
            EditorGUILayout.LabelField("Bridge", live ? "● listening" : "○ not listening");
            GUI.color = prev;
            EditorGUILayout.LabelField("URL", McpBridge.BridgeUrl);
            if (!live)
            {
                EditorGUILayout.HelpBox(
                    "The bridge starts on Editor load. If it's not listening, check the Console " +
                    "for [Unimancer] errors (e.g. the port is already in use).",
                    MessageType.Warning);
            }
        }
    }
}
