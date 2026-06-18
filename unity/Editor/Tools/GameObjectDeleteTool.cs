using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>Delete a GameObject from the scene (undoable).</summary>
    public class GameObjectDeleteTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "gameobject_delete";

        /// <inheritdoc />
        public override string Description => "Delete a GameObject by path or instanceID; returns {deleted:true}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the target and destroy it via Undo.</summary>
        /// <param name="parameters">target (required) — path or instanceID.</param>
        /// <returns>{deleted:true, path} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var path = GoResolve.Path(go);
                Undo.DestroyObjectImmediate(go);

                return new JObject { ["deleted"] = true, ["path"] = path };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
