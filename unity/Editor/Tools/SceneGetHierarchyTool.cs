using System;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Walks the root GameObjects of every loaded scene and returns a nested tree
    /// of { name, path, active, children[] }.
    /// </summary>
    public class SceneGetHierarchyTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "scene_get_hierarchy";

        /// <inheritdoc />
        public override string Description =>
            "Return a nested GameObject hierarchy for every loaded scene.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Build the hierarchy tree.
        /// </summary>
        /// <param name="parameters">includeInactive (optional, default true).</param>
        /// <returns>{ scenes[] } each with a roots tree, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                bool includeInactive = parameters["includeInactive"]?.ToObject<bool>() ?? true;

                var scenes = new JArray();
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    Scene scene = EditorSceneManager.GetSceneAt(i);
                    if (!scene.isLoaded)
                        continue;

                    var roots = new JArray();
                    foreach (var go in scene.GetRootGameObjects())
                    {
                        if (!includeInactive && !go.activeInHierarchy)
                            continue;
                        roots.Add(BuildNode(go.transform, go.name, includeInactive));
                    }

                    scenes.Add(new JObject
                    {
                        ["scene"] = scene.path,
                        ["roots"] = roots,
                    });
                }

                return new JObject { ["scenes"] = scenes };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Recursively build a hierarchy node for a transform.
        /// </summary>
        /// <param name="t">The transform to describe.</param>
        /// <param name="path">The accumulated hierarchy path for this transform.</param>
        /// <param name="includeInactive">Whether to include inactive children.</param>
        /// <returns>A { name, path, active, children[] } JObject.</returns>
        private static JObject BuildNode(Transform t, string path, bool includeInactive)
        {
            var children = new JArray();
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;
                children.Add(BuildNode(child, path + "/" + child.name, includeInactive));
            }

            return new JObject
            {
                ["name"] = t.name,
                ["path"] = path,
                ["active"] = t.gameObject.activeSelf,
                ["children"] = children,
            };
        }
    }
}
