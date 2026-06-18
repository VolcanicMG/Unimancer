using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>Toggle a GameObject's active (enabled) state (undoable).</summary>
    public class GameObjectSetActiveTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "gameobject_set_active";

        /// <inheritdoc />
        public override string Description => "Set a GameObject's active state; returns {path, active}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the target and call SetActive under Undo.</summary>
        /// <param name="parameters">target (required), active (required bool).</param>
        /// <returns>{path, active} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                if (parameters["active"] == null)
                    return new JObject { ["error"] = "active is required" };
                var active = parameters["active"].ToObject<bool>();

                Undo.RecordObject(go, "Set Active");
                go.SetActive(active);
                EditorUtility.SetDirty(go);

                return new JObject { ["path"] = GoResolve.Path(go), ["active"] = active };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
