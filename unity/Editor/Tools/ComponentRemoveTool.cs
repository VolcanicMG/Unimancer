using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>Remove a component from a GameObject by type name (undoable).</summary>
    public class ComponentRemoveTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "component_remove";

        /// <inheritdoc />
        public override string Description =>
            "Remove a component by type name (index selects among duplicates); returns {removed, index}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the target/type, pick the indexed match, and destroy it.</summary>
        /// <param name="parameters">target (required), componentType (required), index (default 0).</param>
        /// <returns>{removed, index} or { error }.</returns>
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

                var index = parameters["index"]?.ToObject<int>() ?? 0;
                var matches = go.GetComponents(type);
                if (matches.Length == 0)
                    return new JObject { ["error"] = $"no {type.Name} on {GoResolve.Path(go)}" };
                if (index < 0 || index >= matches.Length)
                    return new JObject { ["error"] = $"index {index} out of range (found {matches.Length})" };

                Undo.DestroyObjectImmediate(matches[index]);
                EditorUtility.SetDirty(go);

                return new JObject { ["removed"] = type.FullName ?? type.Name, ["index"] = index };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
