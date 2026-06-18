using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Reimports a single asset (AssetDatabase.ImportAsset) when a path is given,
    /// otherwise runs a full AssetDatabase.Refresh().
    /// </summary>
    public class AssetRefreshTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "asset_refresh";

        /// <inheritdoc />
        public override string Description =>
            "Reimport one asset (if path given) or refresh the whole asset database.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Refresh or reimport.
        /// </summary>
        /// <param name="parameters">path (optional).</param>
        /// <returns>{ refreshed: true }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    AssetDatabase.Refresh();
                else
                    AssetDatabase.ImportAsset(path);

                return new JObject { ["refreshed"] = true };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
