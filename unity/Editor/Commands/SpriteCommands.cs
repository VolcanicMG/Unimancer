using System;
using System.Collections.Generic;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Unimancer.Commands
{
    /// <summary>
    /// Sprite authoring commands contributed to com.unity.pipeline: configure an
    /// existing image asset as a Sprite, and procedurally generate a PNG and
    /// import it as one. Both share the same sprite-import surface
    /// (<see cref="SpriteImportUtil"/>).
    /// </summary>
    public static class SpriteCommands
    {
        /// <summary>
        /// Configure an existing image asset's TextureImporter as a Single sprite
        /// and reimport it.
        /// </summary>
        [CliCommand("sprite_import",
            "Configure an existing image asset's TextureImporter: textureType=Sprite, spriteImportMode=Single, then reimport. " +
            "Optional: pixelsPerUnit, pivot {x,y} (0..1), 9-slice border (spriteBorder L/T/R/B in pixels), and filterMode (Point/Bilinear/Trilinear).",
            Tags = new[] { "sprites" })]
        public static Dictionary<string, object> SpriteImport(
            [CliArg("path", "Project-relative asset path of the existing image (e.g. 'Assets/UI/panel.png').", Required = true)] string path,
            [CliArg("pixelsPerUnit", "Sprite pixels-per-unit (default keeps current).")] float? pixelsPerUnit = null,
            [CliArg("pivot", "Sprite pivot {x,y} in 0..1 (e.g. {x:0.5,y:0.5} for center).")] Vec2 pivot = null,
            [CliArg("spriteBorder", "9-slice border in pixels {left,top,right,bottom}.")] SpriteBorder spriteBorder = null,
            [CliArg("filterMode", "Texture filter mode (Point/Bilinear/Trilinear).")] string filterMode = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");
            if (AssetImporter.GetAtPath(path) == null)
                throw new ArgumentException($"asset not found: {path}");

            var applied = SpriteImportUtil.Apply(path, pixelsPerUnit, pivot, spriteBorder, filterMode);
            applied["path"] = path;
            return applied;
        }

        /// <summary>
        /// Procedurally generate a Texture2D, encode it to PNG, write it into
        /// Assets/, then apply a Sprite import config. Intermediate folders are
        /// created.
        /// </summary>
        [CliCommand("sprite_generate",
            "Procedurally generate a Texture2D, encode to PNG, write into Assets/, then import it as a Sprite. " +
            "Patterns: solid (uses color), gradient (linear/radial across colorStops), checker (color + color2 squares of `cellSize`), " +
            "border-frame (fill color2 with a `borderThickness` frame in color). Returns {path, settings}.",
            Tags = new[] { "sprites" })]
        public static Dictionary<string, object> SpriteGenerate(
            [CliArg("path", "Project-relative .png output path (e.g. 'Assets/UI/Generated/btn.png'); folders auto-created.", Required = true)] string path,
            [CliArg("pattern", "Procedural pattern to render (solid/gradient/checker/border-frame).", Required = true)] string pattern,
            [CliArg("width", "Texture width in pixels.", Required = true)] int width,
            [CliArg("height", "Texture height in pixels.", Required = true)] int height,
            [CliArg("color", "Primary color (solid fill / checker square A / frame border). Default opaque white.")] Rgba color = null,
            [CliArg("color2", "Secondary color (checker square B / border-frame fill). Default transparent.")] Rgba color2 = null,
            [CliArg("gradientType", "Gradient kind when pattern=gradient: linear or radial (default linear).")] string gradientType = "linear",
            [CliArg("gradientAngle", "Linear gradient angle in degrees (default 0).")] float gradientAngle = 0f,
            [CliArg("colorStops", "Gradient palette (2+ colors). Falls back to [color, color2] when omitted.")] Rgba[] colorStops = null,
            [CliArg("cellSize", "Checker square size in pixels (default 16).")] int cellSize = 16,
            [CliArg("borderThickness", "Border-frame thickness in pixels (default 4).")] int borderThickness = 4,
            [CliArg("pixelsPerUnit", "Sprite pixels-per-unit.")] float? pixelsPerUnit = null,
            [CliArg("pivot", "Sprite pivot {x,y} in 0..1.")] Vec2 pivot = null,
            [CliArg("spriteBorder", "9-slice border in pixels {left,top,right,bottom}.")] SpriteBorder spriteBorder = null,
            [CliArg("filterMode", "Texture filter mode (Point/Bilinear/Trilinear).")] string filterMode = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("path must be project-relative and under Assets/");
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("path must end in .png");
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("pattern is required");
            if (width <= 0 || height <= 0)
                throw new ArgumentException("width and height must be positive");
            if (width > 4096 || height > 4096)
                throw new ArgumentException("width/height capped at 4096");

            var primary = color != null ? color.ToColor() : new Color(1f, 1f, 1f, 1f);
            var secondary = color2 != null ? color2.ToColor(0f) : new Color(0f, 0f, 0f, 0f);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            switch (pattern)
            {
                case "solid":
                    Fill(tex, primary);
                    break;
                case "gradient":
                    RenderGradient(tex, colorStops, gradientType, gradientAngle, primary, secondary);
                    break;
                case "checker":
                    RenderChecker(tex, cellSize, primary, secondary);
                    break;
                case "border-frame":
                    RenderBorderFrame(tex, borderThickness, primary, secondary);
                    break;
                default:
                    UnityEngine.Object.DestroyImmediate(tex);
                    throw new ArgumentException($"unknown pattern: {pattern}");
            }
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            if (png == null)
                throw new InvalidOperationException("EncodeToPNG returned null");

            // Ensure the target folder exists, then write the bytes and import.
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // Apply the sprite import config (same surface as sprite_import).
            var settings = SpriteImportUtil.Apply(path, pixelsPerUnit, pivot, spriteBorder, filterMode);

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["pattern"] = pattern,
                ["width"] = width,
                ["height"] = height,
                ["settings"] = settings,
            };
        }

        // --- Pattern renderers ---

        /// <summary>Fill the whole texture with one color.</summary>
        private static void Fill(Texture2D tex, Color c)
        {
            var pixels = new Color[tex.width * tex.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels(pixels);
        }

        /// <summary>Render a linear or radial gradient across a color palette.</summary>
        private static void RenderGradient(Texture2D tex, Rgba[] colorStops, string type, float angle, Color color, Color color2)
        {
            var palette = ParsePalette(colorStops) ?? new List<Color> { color, color2 };
            if (string.IsNullOrEmpty(type)) type = "linear";

            int w = tex.width, h = tex.height;
            if (type == "radial")
            {
                float cx = w / 2f, cy = h / 2f;
                float maxDist = Mathf.Sqrt(cx * cx + cy * cy);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDist);
                        tex.SetPixel(x, y, LerpPalette(palette, t));
                    }
            }
            else
            {
                float rad = angle * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float denomX = Mathf.Max(1, w - 1), denomY = Mathf.Max(1, h - 1);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float t = Vector2.Dot(new Vector2(x / denomX, y / denomY), dir);
                        t = Mathf.Clamp01((t + 1f) / 2f);
                        tex.SetPixel(x, y, LerpPalette(palette, t));
                    }
            }
        }

        /// <summary>Render a two-color checkerboard of square `cellSize`.</summary>
        private static void RenderChecker(Texture2D tex, int cellSize, Color a, Color b)
        {
            int cell = Mathf.Max(1, cellSize);
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    bool even = ((x / cell) + (y / cell)) % 2 == 0;
                    tex.SetPixel(x, y, even ? a : b);
                }
        }

        /// <summary>Render a `borderThickness` frame in `color` over a `color2` fill.</summary>
        private static void RenderBorderFrame(Texture2D tex, int borderThickness, Color border, Color fill)
        {
            int t = Mathf.Max(1, borderThickness);
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool onBorder = x < t || y < t || x >= w - t || y >= h - t;
                    tex.SetPixel(x, y, onBorder ? border : fill);
                }
        }

        // --- Helpers ---

        /// <summary>Linearly interpolate a color across an N-stop palette by t in 0..1.</summary>
        private static Color LerpPalette(List<Color> palette, float t)
        {
            if (palette.Count == 1) return palette[0];
            if (t <= 0) return palette[0];
            if (t >= 1) return palette[palette.Count - 1];
            float scaled = t * (palette.Count - 1);
            int i = Mathf.FloorToInt(scaled);
            if (i >= palette.Count - 1) return palette[palette.Count - 1];
            return Color.Lerp(palette[i], palette[i + 1], scaled - i);
        }

        /// <summary>Convert the colorStops argument into a palette, or null if absent/empty.</summary>
        private static List<Color> ParsePalette(Rgba[] stops)
        {
            if (stops == null || stops.Length == 0) return null;
            var list = new List<Color>();
            foreach (var s in stops)
                if (s != null) list.Add(s.ToColor());
            return list.Count > 0 ? list : null;
        }

        /// <summary>Recursively create the AssetDatabase folder chain for a project path.</summary>
        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder == "Assets") return;
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
