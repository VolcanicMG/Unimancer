using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Saves a scene GameObject as a prefab asset, optionally connecting the
    /// source object to the new prefab.
    /// </summary>
    public class PrefabCreateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "prefab_create";

        /// <inheritdoc />
        public override string Description =>
            "Save a scene GameObject as a prefab (SaveAsPrefabAsset, or AndConnect when connect=true).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Create the prefab from the named scene object.
        /// </summary>
        /// <param name="parameters">gameObjectPath (required), prefabPath (required), connect (optional, default true).</param>
        /// <returns>{ path, success }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var prefabPath = parameters["prefabPath"]?.ToString();
                if (string.IsNullOrEmpty(gameObjectPath) || string.IsNullOrEmpty(prefabPath))
                    return new JObject { ["error"] = "gameObjectPath and prefabPath are required" };

                var guard = PathGuard.Validate(prefabPath);
                if (guard != null) return guard;

                var go = FindGameObject(gameObjectPath);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {gameObjectPath}" };

                bool connect = parameters["connect"]?.ToObject<bool>() ?? true;

                bool success;
                if (connect)
                    PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction, out success);
                else
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out success);

                return new JObject
                {
                    ["path"] = prefabPath,
                    ["success"] = success,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Resolve a GameObject by its hierarchy path across every loaded scene.
        /// </summary>
        /// <param name="path">Hierarchy path, e.g. 'Parent/Child'.</param>
        /// <returns>The matching GameObject, or null if not found.</returns>
        private static GameObject FindGameObject(string path)
        {
            var parts = path.Split('/');
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != parts[0])
                        continue;

                    var current = root.transform;
                    bool matched = true;
                    for (int p = 1; p < parts.Length; p++)
                    {
                        current = current.Find(parts[p]);
                        if (current == null)
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                        return current.gameObject;
                }
            }

            return null;
        }
    }
}
