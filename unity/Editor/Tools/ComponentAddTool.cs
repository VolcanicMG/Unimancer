using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>Add a component to a GameObject by type name (undoable).</summary>
    public class ComponentAddTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "component_add";

        /// <inheritdoc />
        public override string Description => "Add a component by type name; returns {added:<typeName>}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the GameObject and component Type, then add it via Undo.</summary>
        /// <param name="parameters">target (required), componentType (required).</param>
        /// <returns>{added:<typeName>} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var typeName = parameters["componentType"]?.ToString();
                var type = ComponentTypeResolve.Resolve(typeName);
                if (type == null)
                    return new JObject { ["error"] = $"component type not found: {typeName}" };

                var added = Undo.AddComponent(go, type);
                if (added == null)
                    return new JObject { ["error"] = $"could not add component: {type.Name}" };

                EditorUtility.SetDirty(go);
                return new JObject { ["added"] = type.FullName ?? type.Name };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
