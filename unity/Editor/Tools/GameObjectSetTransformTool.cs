using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Set a GameObject's position/rotation/scale and/or reparent it. Only the
    /// supplied fields change. Recorded for Undo.
    /// </summary>
    public class GameObjectSetTransformTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "gameobject_set_transform";

        /// <inheritdoc />
        public override string Description =>
            "Set position/rotation/scale and/or reparent a GameObject; returns the resulting transform.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Apply transform/parent changes to the resolved target.</summary>
        /// <param name="parameters">target (required), position, rotation, scale, parentPath, worldSpace.</param>
        /// <returns>The resulting world transform, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var t = go.transform;
                Undo.RecordObject(t, "Set Transform");

                var worldSpace = parameters["worldSpace"]?.ToObject<bool>() ?? true;

                // Reparent first so subsequent world/local writes resolve correctly.
                // A present-but-empty parentPath detaches to the scene root.
                if (parameters["parentPath"] != null)
                {
                    var parentPath = parameters["parentPath"].ToString();
                    if (string.IsNullOrEmpty(parentPath))
                    {
                        Undo.SetTransformParent(t, null, "Reparent");
                    }
                    else
                    {
                        var parent = GoResolve.Resolve(parentPath);
                        if (parent == null)
                            return new JObject { ["error"] = $"parent not found: {parentPath}" };
                        Undo.SetTransformParent(t, parent.transform, "Reparent");
                    }
                }

                if (parameters["position"] != null)
                {
                    var p = GoResolve.ToVector3(parameters["position"], worldSpace ? t.position : t.localPosition);
                    if (worldSpace) t.position = p; else t.localPosition = p;
                }
                if (parameters["rotation"] != null)
                {
                    var r = GoResolve.ToVector3(parameters["rotation"], worldSpace ? t.eulerAngles : t.localEulerAngles);
                    if (worldSpace) t.eulerAngles = r; else t.localEulerAngles = r;
                }
                if (parameters["scale"] != null)
                {
                    // Scale is always local in Unity.
                    t.localScale = GoResolve.ToVector3(parameters["scale"], t.localScale);
                }

                EditorUtility.SetDirty(go);

                return new JObject
                {
                    ["path"] = GoResolve.Path(go),
                    ["position"] = Vec(t.position),
                    ["rotation"] = Vec(t.eulerAngles),
                    ["localScale"] = Vec(t.localScale),
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>Serialize a Vector3 to an {x,y,z} JObject.</summary>
        /// <param name="v">The vector.</param>
        /// <returns>JSON {x,y,z}.</returns>
        private static JObject Vec(Vector3 v) => new JObject { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z };
    }
}
