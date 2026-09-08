using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// In-Editor settings panel (Window → Unimancer → Setup). It exists only to
    /// persist the two EditorPrefs the chat window reads: where the Unimancer Node
    /// MCP server lives, and whether the <c>claude</c> invocation is wrapped in
    /// <c>wsl</c> (Unity on Windows, Claude in WSL).
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
            win.minSize = new Vector2(460, 200);
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
            EditorGUILayout.LabelField("Settings for the in-Editor Claude chat window.", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            EditorGUILayout.HelpBox(
                "Path to the Unimancer Node entry (src/index.js) on the machine that runs claude. " +
                "The chat window registers it alongside the `unity` CLI MCP server.\n" +
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
            _wrapWsl = EditorGUILayout.ToggleLeft("Wrap with `wsl` (Unity on Windows, Claude in WSL)", _wrapWsl);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save settings")) SaveSettings();
                if (GUILayout.Button("Open GitHub")) Application.OpenURL(RepoUrl);
            }
        }

        /// <summary>Persist the path + WSL toggle to EditorPrefs.</summary>
        private void SaveSettings()
        {
            EditorPrefs.SetString(PrefPath, _nodePath ?? "");
            EditorPrefs.SetBool(PrefWsl, _wrapWsl);
            ShowNotification(new GUIContent("Saved"));
        }
    }
}
