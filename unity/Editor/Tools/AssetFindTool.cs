using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Searches the project for assets using AssetDatabase filter syntax and
    /// returns up to 200 paths plus the total count.
    /// </summary>
    public class AssetFindTool : McpToolBase
    {
        /// <summary>Cap on the number of returned paths to keep responses bounded.</summary>
        private const int MaxResults = 200;

        /// <inheritdoc />
        public override string Name => "asset_find";

        /// <inheritdoc />
        public override string Description =>
            "Find assets via AssetDatabase filter syntax (e.g. 't:Material wood'); returns up to 200 paths + total.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Run the search.
        /// </summary>
        /// <param name="parameters">filter (required), folders (optional, default ['Assets']).</param>
        /// <returns>{ total, paths[] }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var filter = parameters["filter"]?.ToString();
                if (string.IsNullOrEmpty(filter))
                    return new JObject { ["error"] = "filter is required" };

                string[] folders;
                if (parameters["folders"] is JArray arr && arr.Count > 0)
                    folders = arr.Select(f => f.ToString()).ToArray();
                else
                    folders = new[] { "Assets" };

                var guids = AssetDatabase.FindAssets(filter, folders);
                var paths = new JArray();
                foreach (var guid in guids.Take(MaxResults))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));

                return new JObject
                {
                    ["total"] = guids.Length,
                    ["paths"] = paths,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
