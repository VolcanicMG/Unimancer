using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>Rename a GameObject (undoable).</summary>
    public class GameObjectRenameTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "gameobject_rename";

        /// <inheritdoc />
        public override string Description => "Rename a GameObject; returns {instanceID, path}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the target and set its name under Undo.</summary>
        /// <param name="parameters">target (required), newName (required).</param>
        /// <returns>{instanceID, path} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var newName = parameters["newName"]?.ToString();
                if (string.IsNullOrEmpty(newName))
                    return new JObject { ["error"] = "newName is required" };

                Undo.RecordObject(go, "Rename");
                go.name = newName;
                EditorUtility.SetDirty(go);

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
