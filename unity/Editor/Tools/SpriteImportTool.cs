using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Configure an existing image asset's TextureImporter as a Single sprite,
    /// with optional pixelsPerUnit / pivot / 9-slice border / filterMode, then
    /// reimport. The pixel-pushing variant is <see cref="SpriteGenerateTool"/>.
    /// </summary>
    public class SpriteImportTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "sprite_import";

        /// <inheritdoc />
        public override string Description =>
            "Configure an existing image asset's TextureImporter as a Single sprite (pixelsPerUnit, pivot, 9-slice border, filterMode) and reimport.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Apply the sprite import settings to an existing asset.</summary>
        /// <param name="parameters">path (required), pixelsPerUnit, pivot, spriteBorder, filterMode.</param>
        /// <returns>The applied settings, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                if (AssetImporter.GetAtPath(path) == null)
                    return new JObject { ["error"] = $"asset not found: {path}" };

                var applied = SpriteImportUtil.Apply(path, parameters);
                applied["path"] = path;
                return applied;
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
