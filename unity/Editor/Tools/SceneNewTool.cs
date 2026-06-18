using System;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Creates a new scene (empty or with default GameObjects) and optionally
    /// saves it to a path immediately.
    /// </summary>
    public class SceneNewTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "scene_new";

        /// <inheritdoc />
        public override string Description =>
            "Create a new scene via EditorSceneManager.NewScene (setup 'empty' or 'defaultGameObjects'); optionally save it to a path.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Create (and optionally save) a new scene.
        /// </summary>
        /// <param name="parameters">setup (optional), path (optional).</param>
        /// <returns>{ created, path, saved } info, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var setupStr = parameters["setup"]?.ToString() ?? "defaultGameObjects";
                var setup = setupStr == "empty"
                    ? NewSceneSetup.EmptyScene
                    : NewSceneSetup.DefaultGameObjects;

                // NewSceneMode.Single replaces the open scene with the new one.
                var scene = EditorSceneManager.NewScene(setup, NewSceneMode.Single);

                var path = parameters["path"]?.ToString();
                bool saved = false;
                if (!string.IsNullOrEmpty(path))
                    saved = EditorSceneManager.SaveScene(scene, path);

                return new JObject
                {
                    ["created"] = true,
                    ["setup"] = setupStr,
                    ["path"] = string.IsNullOrEmpty(path) ? scene.path : path,
                    ["saved"] = saved,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
