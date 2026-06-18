using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Deletes an asset under Assets/ via AssetDatabase.DeleteAsset (which also
    /// removes the .meta file). Runs synchronously.
    /// </summary>
    public class ScriptDeleteTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "script_delete";

        /// <inheritdoc />
        public override string Description => "Delete an asset under Assets/ via AssetDatabase.DeleteAsset.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Delete the asset at the given path.
        /// </summary>
        /// <param name="parameters">path (required, under Assets/).</param>
        /// <returns>{ deleted } (bool), or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                // Path-safety guard: only allow deletions inside the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                var deleted = AssetDatabase.DeleteAsset(path);
                return new JObject { ["deleted"] = deleted };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
