using System;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Opens a scene asset in the Editor, either replacing the current scene
    /// (Single) or loading it alongside the open scenes (Additive).
    /// </summary>
    public class SceneOpenTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "scene_open";

        /// <inheritdoc />
        public override string Description =>
            "Open a scene asset (Single by default, or Additive when additive=true).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Open the scene at the requested path.
        /// </summary>
        /// <param name="parameters">path (required), additive (optional).</param>
        /// <returns>{ opened } with the scene path, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                var additive = parameters["additive"]?.ToObject<bool>() ?? false;
                var mode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;

                var scene = EditorSceneManager.OpenScene(path, mode);
                return new JObject { ["opened"] = scene.path };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
