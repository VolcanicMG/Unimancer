using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Reports the current Editor runtime state: play/pause/compile flags, the
    /// active scene, the selection count, and the Unity version. Runs
    /// synchronously on the main thread.
    /// </summary>
    public class EditorStateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "editor_state";

        /// <inheritdoc />
        public override string Description =>
            "Report current Editor state: isPlaying, isPaused, isCompiling, activeScene, selectionCount, unityVersion.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Read EditorApplication / scene / selection state.
        /// </summary>
        /// <param name="parameters">Ignored (no parameters).</param>
        /// <returns>State fields, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                return new JObject
                {
                    ["isPlaying"] = EditorApplication.isPlaying,
                    ["isPaused"] = EditorApplication.isPaused,
                    ["isCompiling"] = EditorApplication.isCompiling,
                    ["activeScene"] = scene.path,
                    ["selectionCount"] = Selection.objects.Length,
                    ["unityVersion"] = UnityEngine.Application.unityVersion,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
