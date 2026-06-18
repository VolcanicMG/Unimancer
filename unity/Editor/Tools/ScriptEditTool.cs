using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Edits an existing file under Assets/, either by full content replacement or
    /// by an ordered list of find/replace edits. Exactly one of the two must be
    /// supplied. Reimports the asset afterward. Runs synchronously.
    /// </summary>
    public class ScriptEditTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "script_edit";

        /// <inheritdoc />
        public override string Description =>
            "Edit a file under Assets/ via full content replacement OR an ordered list of {find,replace} edits (exactly one).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Apply the edit and reimport.
        /// </summary>
        /// <param name="parameters">path (required); exactly one of content or replacements.</param>
        /// <returns>{ updated, bytes }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                // Path-safety guard: keep edits inside the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                if (!File.Exists(path))
                    return new JObject { ["error"] = $"file not found: {path}" };

                var contentToken = parameters["content"];
                var replacements = parameters["replacements"] as JArray;

                var hasContent = contentToken != null && contentToken.Type != JTokenType.Null;
                var hasReplacements = replacements != null && replacements.Count > 0;

                // Enforce "exactly one of content / replacements".
                if (hasContent == hasReplacements)
                    return new JObject
                    {
                        ["error"] = "provide exactly one of content (full replacement) or replacements (array of {find,replace})"
                    };

                string updated;
                if (hasContent)
                {
                    updated = contentToken.ToString();
                }
                else
                {
                    updated = File.ReadAllText(path);
                    foreach (var item in replacements)
                    {
                        var find = item["find"]?.ToString();
                        var replace = item["replace"]?.ToString() ?? string.Empty;
                        if (string.IsNullOrEmpty(find))
                            return new JObject { ["error"] = "each replacement requires a non-empty 'find'" };
                        // Ordinal literal replacement, applied in array order.
                        updated = updated.Replace(find, replace);
                    }
                }

                File.WriteAllText(path, updated);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                return new JObject
                {
                    ["updated"] = path,
                    ["bytes"] = Encoding.UTF8.GetByteCount(updated),
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
