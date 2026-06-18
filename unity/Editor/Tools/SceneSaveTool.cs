using System;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Saves the active scene to disk. Defaults to the active scene's current
    /// path; an untitled (never-saved) scene requires an explicit path.
    /// </summary>
    public class SceneSaveTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "scene_save";

        /// <inheritdoc />
        public override string Description =>
            "Save the active scene via EditorSceneManager.SaveScene (path optional unless the scene is untitled).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Save the active scene, optionally to a new path.
        /// </summary>
        /// <param name="parameters">path (optional; required for untitled scenes).</param>
        /// <returns>{ saved } with the saved path, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                var path = parameters["path"]?.ToString();

                // An untitled scene has no path on disk; SaveScene needs a target.
                if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(scene.path))
                    return new JObject { ["error"] = "active scene is untitled; a path is required" };

                bool saved = string.IsNullOrEmpty(path)
                    ? EditorSceneManager.SaveScene(scene)
                    : EditorSceneManager.SaveScene(scene, path);

                if (!saved)
                    return new JObject { ["error"] = "SaveScene returned false" };

                return new JObject { ["saved"] = string.IsNullOrEmpty(path) ? scene.path : path };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
