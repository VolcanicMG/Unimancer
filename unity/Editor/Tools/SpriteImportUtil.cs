using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Shared TextureImporter configuration for the sprite tools. Sets a texture
    /// up as a Single sprite and applies the optional pixelsPerUnit / pivot /
    /// 9-slice border / filterMode, then reimports. Borrowed from CoPlay
    /// ManageTexture's sprite-config path, extended with spriteBorder support.
    /// </summary>
    public static class SpriteImportUtil
    {
        /// <summary>
        /// Configure the TextureImporter at <paramref name="assetPath"/> as a
        /// Single sprite and apply the provided settings, then reimport.
        /// </summary>
        /// <param name="assetPath">Project-relative path to the image asset.</param>
        /// <param name="parameters">Source params: pixelsPerUnit, pivot, spriteBorder, filterMode.</param>
        /// <returns>A JObject describing the applied settings, or { error } if no importer.</returns>
        public static JObject Apply(string assetPath, JObject parameters)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return new JObject { ["error"] = $"no TextureImporter at: {assetPath} (is it an image asset?)" };

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            var applied = new JObject
            {
                ["textureType"] = "Sprite",
                ["spriteImportMode"] = "Single",
            };

            // Pixels per unit.
            var ppu = parameters["pixelsPerUnit"];
            if (ppu != null && ppu.Type != JTokenType.Null)
            {
                importer.spritePixelsPerUnit = ppu.ToObject<float>();
                applied["pixelsPerUnit"] = importer.spritePixelsPerUnit;
            }

            // Pivot (0..1). Setting a custom pivot also flips the sprite alignment to Custom.
            if (parameters["pivot"] is JObject pivotObj)
            {
                var pivot = RectUtil.ToVector2(pivotObj, new Vector2(0.5f, 0.5f));
                importer.spritePivot = pivot;
                // The importer's per-sprite settings object is what actually persists alignment+pivot.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                applied["pivot"] = RectUtil.FromVector2(pivot);
            }

            // 9-slice border (Unity stores it as Vector4 = Left, Bottom, Right, Top).
            if (parameters["spriteBorder"] is JObject b)
            {
                float left = b["left"]?.ToObject<float>() ?? 0f;
                float top = b["top"]?.ToObject<float>() ?? 0f;
                float right = b["right"]?.ToObject<float>() ?? 0f;
                float bottom = b["bottom"]?.ToObject<float>() ?? 0f;
                importer.spriteBorder = new Vector4(left, bottom, right, top);
                applied["spriteBorder"] = new JObject
                {
                    ["left"] = left, ["top"] = top, ["right"] = right, ["bottom"] = bottom,
                };
            }

            // Filter mode.
            var fm = parameters["filterMode"]?.ToString();
            if (!string.IsNullOrEmpty(fm) && System.Enum.TryParse<FilterMode>(fm, out var filter))
            {
                importer.filterMode = filter;
                applied["filterMode"] = filter.ToString();
            }

            importer.SaveAndReimport();
            return applied;
        }
    }
}
