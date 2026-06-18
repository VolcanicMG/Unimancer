using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Reports the current Editor selection (Selection.objects) as a list of
    /// { name, path, instanceID, type }. Runs synchronously on the main thread.
    /// </summary>
    public class SelectionGetTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "selection_get";

        /// <inheritdoc />
        public override string Description =>
            "Get the current selection. Returns [{ name, path, instanceID, type }].";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Read Selection.objects and project to JSON.
        /// </summary>
        /// <param name="parameters">Ignored (no parameters).</param>
        /// <returns>{ objects: [...] }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var arr = new JArray();
                foreach (var obj in Selection.objects)
                {
                    if (obj == null) continue;
                    arr.Add(new JObject
                    {
                        ["name"] = obj.name,
                        ["path"] = AssetDatabase.GetAssetPath(obj),
                        ["instanceID"] = EntityId.ToULong(obj.GetEntityId()).ToString(),
                        ["type"] = obj.GetType().Name,
                    });
                }
                return new JObject { ["objects"] = arr };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
