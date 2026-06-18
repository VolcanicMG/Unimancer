using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Deletes an asset from the project via AssetDatabase.DeleteAsset.
    /// </summary>
    public class AssetDeleteTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "asset_delete";

        /// <inheritdoc />
        public override string Description => "Delete an asset. Returns { deleted: bool }.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Delete the asset.
        /// </summary>
        /// <param name="parameters">path (required).</param>
        /// <returns>{ deleted } flag, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                bool deleted = AssetDatabase.DeleteAsset(path);
                return new JObject { ["deleted"] = deleted };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
