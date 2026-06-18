using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Instantiates a prefab into the active scene, optionally parenting and
    /// positioning it, and registers the new object for Undo.
    /// </summary>
    public class PrefabInstantiateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "prefab_instantiate";

        /// <inheritdoc />
        public override string Description =>
            "Instantiate a prefab into the active scene (optional parent + local position).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Load and instantiate the prefab.
        /// </summary>
        /// <param name="parameters">prefabPath (required), parentPath (optional), position (optional {x,y,z}).</param>
        /// <returns>{ instanceID, path }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var prefabPath = parameters["prefabPath"]?.ToString();
                if (string.IsNullOrEmpty(prefabPath))
                    return new JObject { ["error"] = "prefabPath is required" };

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    return new JObject { ["error"] = $"prefab not found at {prefabPath}" };

                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                    return new JObject { ["error"] = "InstantiatePrefab returned null" };

                // Register for Undo so the instantiation can be reverted in the Editor.
                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

                var parentPath = parameters["parentPath"]?.ToString();
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = FindGameObject(parentPath);
                    if (parent == null)
                        return new JObject { ["error"] = $"parent not found: {parentPath}" };
                    // worldPositionStays=false keeps the local position we set below.
                    instance.transform.SetParent(parent.transform, false);
                }

                if (parameters["position"] is JObject pos)
                {
                    instance.transform.localPosition = new Vector3(
                        pos["x"]?.ToObject<float>() ?? 0f,
                        pos["y"]?.ToObject<float>() ?? 0f,
                        pos["z"]?.ToObject<float>() ?? 0f);
                }

                return new JObject
                {
                    ["instanceID"] = instance.GetEntityId().ToULong(),
                    ["path"] = GetHierarchyPath(instance.transform),
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

        /// <summary>
        /// Build the full hierarchy path for a transform (root-first, slash-joined).
        /// </summary>
        /// <param name="t">The transform to describe.</param>
        /// <returns>The hierarchy path string.</returns>
        private static string GetHierarchyPath(Transform t)
        {
            var path = t.name;
            var parent = t.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
