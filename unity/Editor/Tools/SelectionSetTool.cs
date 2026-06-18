using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Sets the Editor selection from a list of asset paths or instance IDs.
    /// Each target is resolved to a UnityEngine.Object before assigning
    /// Selection.objects. Runs synchronously on the main thread.
    /// </summary>
    public class SelectionSetTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "selection_set";

        /// <inheritdoc />
        public override string Description =>
            "Set the selection from asset paths or instance IDs. Returns the count selected.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Resolve each target and assign the selection.
        /// </summary>
        /// <param name="parameters">targets: array of asset paths or instance IDs.</param>
        /// <returns>{ selected }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                if (!(parameters["targets"] is JArray targets))
                    return new JObject { ["error"] = "targets array is required" };

                var resolved = new List<UnityEngine.Object>();
                foreach (var t in targets)
                {
                    UnityEngine.Object obj = null;
                    if (t.Type == JTokenType.Integer)
                    {
                        // Instance ID path.
                        obj = EditorUtility.EntityIdToObject(EntityId.FromULong(t.ToObject<ulong>()));
                    }
                    else
                    {
                        var s = t.ToString();
                        // A numeric string is also treated as an instance ID;
                        // otherwise it is an asset path.
                        if (ulong.TryParse(s, out var id))
                            obj = EditorUtility.EntityIdToObject(EntityId.FromULong(id));
                        else
                            obj = AssetDatabase.LoadMainAssetAtPath(s);
                    }
                    if (obj != null) resolved.Add(obj);
                }

                Selection.objects = resolved.ToArray();
                return new JObject { ["selected"] = resolved.Count };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
