using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unimancer.Commands
{
    /// <summary>
    /// Shared TextureImporter configuration for the sprite commands. Sets a
    /// texture up as a Single sprite and applies the optional pixelsPerUnit /
    /// pivot / 9-slice border / filterMode, then reimports.
    /// </summary>
    public static class SpriteImportUtil
    {
        /// <summary>
        /// Configure the TextureImporter at <paramref name="assetPath"/> as a
        /// Single sprite and apply the provided settings, then reimport.
        /// </summary>
        /// <param name="assetPath">Project-relative path to the image asset.</param>
        /// <param name="pixelsPerUnit">Sprite pixels-per-unit; null keeps the current value.</param>
        /// <param name="pivot">Sprite pivot in 0..1; null keeps the current value.</param>
        /// <param name="spriteBorder">9-slice border in pixels; null keeps the current value.</param>
        /// <param name="filterMode">Point/Bilinear/Trilinear; null keeps the current value.</param>
        /// <returns>A map describing the applied settings.</returns>
        /// <exception cref="InvalidOperationException">No TextureImporter at the path.</exception>
        public static Dictionary<string, object> Apply(
            string assetPath, float? pixelsPerUnit, Vec2 pivot, SpriteBorder spriteBorder, string filterMode)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"no TextureImporter at: {assetPath} (is it an image asset?)");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            var applied = new Dictionary<string, object>
            {
                ["textureType"] = "Sprite",
                ["spriteImportMode"] = "Single",
            };

            if (pixelsPerUnit.HasValue)
            {
                importer.spritePixelsPerUnit = pixelsPerUnit.Value;
                applied["pixelsPerUnit"] = importer.spritePixelsPerUnit;
            }

            // Setting a custom pivot also flips the sprite alignment to Custom.
            if (pivot != null)
            {
                var p = pivot.ToVector2();
                importer.spritePivot = p;
                // The importer's per-sprite settings object is what actually persists alignment+pivot.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = p;
                importer.SetTextureSettings(settings);
                applied["pivot"] = RectUtil.FromVector2(p);
            }

            // Unity stores the 9-slice border as Vector4 = Left, Bottom, Right, Top.
            if (spriteBorder != null)
            {
                importer.spriteBorder = new Vector4(
                    spriteBorder.left, spriteBorder.bottom, spriteBorder.right, spriteBorder.top);
                applied["spriteBorder"] = new Dictionary<string, object>
                {
                    ["left"] = spriteBorder.left,
                    ["top"] = spriteBorder.top,
                    ["right"] = spriteBorder.right,
                    ["bottom"] = spriteBorder.bottom,
                };
            }

            if (!string.IsNullOrEmpty(filterMode))
            {
                if (!Enum.TryParse<FilterMode>(filterMode, out var filter))
                    throw new ArgumentException($"unknown filterMode: {filterMode} (expected Point, Bilinear or Trilinear)");
                importer.filterMode = filter;
                applied["filterMode"] = filter.ToString();
            }

            importer.SaveAndReimport();
            return applied;
        }
    }
}
