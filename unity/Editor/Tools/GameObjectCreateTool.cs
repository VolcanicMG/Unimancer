using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Create a GameObject (empty or from a built-in primitive), optionally
    /// parenting it and applying a local transform. Registered for Undo.
    /// </summary>
    public class GameObjectCreateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "gameobject_create";

        /// <inheritdoc />
        public override string Description =>
            "Create a GameObject (empty or a built-in primitive) and return {instanceID, path}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Create the GameObject and apply parent/transform options.</summary>
        /// <param name="parameters">name (required), primitive, parentPath, position, rotation, scale.</param>
        /// <returns>{instanceID, path} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var name = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                    return new JObject { ["error"] = "name is required" };

                GameObject go;
                var primitive = parameters["primitive"]?.ToString();
                if (!string.IsNullOrEmpty(primitive))
                {
                    if (!Enum.TryParse<PrimitiveType>(primitive, out var pt))
                        return new JObject { ["error"] = $"unknown primitive: {primitive}" };
                    go = GameObject.CreatePrimitive(pt);
                }
                else
                {
                    go = new GameObject();
                }
                go.name = name;

                // Register for Undo before mutating so the whole create is one step.
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);

                var parentPath = parameters["parentPath"]?.ToString();
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = GoResolve.Resolve(parentPath);
                    if (parent == null)
                        return new JObject { ["error"] = $"parent not found: {parentPath}" };
                    go.transform.SetParent(parent.transform, false);
                }

                go.transform.localPosition = GoResolve.ToVector3(parameters["position"], go.transform.localPosition);
                go.transform.localEulerAngles = GoResolve.ToVector3(parameters["rotation"], go.transform.localEulerAngles);
                go.transform.localScale = GoResolve.ToVector3(parameters["scale"], go.transform.localScale);

                Selection.activeGameObject = go;

                return new JObject
                {
                    ["instanceID"] = go.GetEntityId().ToULong(),
                    ["path"] = GoResolve.Path(go),
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
