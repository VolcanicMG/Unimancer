using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// EDIT-MODE walk of a Canvas (or every loaded scene's Canvases) returning a
    /// tree of { name, path, components[], rect }. The edit-mode analog of the
    /// play-mode <c>runtime_ui_list</c>: it inspects the scene the Editor has open
    /// rather than a running build.
    /// </summary>
    public class UiDumpTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "ui_dump";

        /// <inheritdoc />
        public override string Description =>
            "Edit-mode walk of a Canvas (or all scene Canvases) returning a tree of {name, path, components[], rect}. Returns { canvases[] }.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Dump one canvas (target) or every scene canvas.</summary>
        /// <param name="parameters">target (optional Canvas path/instanceID), includeInactive (default true).</param>
        /// <returns>{ canvases:[{ path, tree }] } or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                bool includeInactive = parameters["includeInactive"]?.ToObject<bool>() ?? true;
                var canvases = new JArray();

                var target = parameters["target"]?.ToString();
                if (!string.IsNullOrEmpty(target))
                {
                    var go = GoResolve.Resolve(target);
                    if (go == null)
                        return new JObject { ["error"] = $"target not found: {target}" };
                    // Resolve up to the nearest Canvas so callers can pass any UI node.
                    var canvas = go.GetComponent<Canvas>() ?? go.GetComponentInParent<Canvas>();
                    var root = canvas != null ? canvas.gameObject : go;
                    canvases.Add(new JObject
                    {
                        ["path"] = GoResolve.Path(root),
                        ["tree"] = BuildNode(root.transform, GoResolve.Path(root), includeInactive),
                    });
                }
                else
                {
                    // Every Canvas in every loaded scene (top-level canvases only;
                    // nested canvases still appear within their parent's tree).
                    for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    {
                        Scene scene = EditorSceneManager.GetSceneAt(i);
                        if (!scene.isLoaded) continue;
                        foreach (var rootGo in scene.GetRootGameObjects())
                        {
                            foreach (var canvas in rootGo.GetComponentsInChildren<Canvas>(true))
                            {
                                // Skip canvases nested under another canvas (rendered within it).
                                if (canvas.transform.parent != null &&
                                    canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                                    continue;
                                var cgo = canvas.gameObject;
                                canvases.Add(new JObject
                                {
                                    ["scene"] = scene.path,
                                    ["path"] = GoResolve.Path(cgo),
                                    ["tree"] = BuildNode(cgo.transform, GoResolve.Path(cgo), includeInactive),
                                });
                            }
                        }
                    }
                }

                return new JObject { ["canvases"] = canvases };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Recursively serialize a UI node: name, path, components, full rect, and
        /// children.
        /// </summary>
        /// <param name="t">Transform to describe.</param>
        /// <param name="path">Accumulated hierarchy path for this transform.</param>
        /// <param name="includeInactive">Whether to include inactive children.</param>
        /// <returns>A { name, path, active, components[], rect, children[] } JObject.</returns>
        private static JObject BuildNode(Transform t, string path, bool includeInactive)
        {
            var components = new JArray(
                t.GetComponents<Component>()
                 .Where(c => c != null)            // a missing script serializes as null
                 .Select(c => (JToken)c.GetType().Name));

            var node = new JObject
            {
                ["name"] = t.name,
                ["path"] = path,
                ["active"] = t.gameObject.activeSelf,
                ["components"] = components,
            };

            // Full rect when this is a RectTransform (UI nodes always are).
            if (t is RectTransform rt)
                node["rect"] = RectUtil.Describe(rt);

            var children = new JArray();
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;
                children.Add(BuildNode(child, path + "/" + child.name, includeInactive));
            }
            node["children"] = children;
            return node;
        }
    }
}
