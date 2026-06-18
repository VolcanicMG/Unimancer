using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Creates a new folder under an existing parent folder via
    /// AssetDatabase.CreateFolder.
    /// </summary>
    public class AssetCreateFolderTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "asset_create_folder";

        /// <inheritdoc />
        public override string Description =>
            "Create a new folder under an existing parent folder.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Create the folder.
        /// </summary>
        /// <param name="parameters">parent (required), name (required).</param>
        /// <returns>{ path } of the new folder, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var parent = parameters["parent"]?.ToString();
                var name = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                    return new JObject { ["error"] = "parent and name are required" };

                var guard = PathGuard.Validate(parent);
                if (guard != null) return guard;
                if (name.Contains("/") || name.Contains(".."))
                    return new JObject { ["error"] = "name must not contain '/' or '..'" };

                // CreateFolder returns the GUID of the new folder, or "" on failure.
                var guid = AssetDatabase.CreateFolder(parent, name);
                if (string.IsNullOrEmpty(guid))
                    return new JObject { ["error"] = $"could not create folder '{name}' under '{parent}'" };

                return new JObject { ["path"] = AssetDatabase.GUIDToAssetPath(guid) };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
