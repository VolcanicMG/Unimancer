using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Procedurally generate a Texture2D, encode it to PNG, write it into Assets/,
    /// then apply a Sprite import config via <see cref="SpriteImportUtil"/>.
    /// Patterns: solid, gradient (linear/radial), checker, border-frame. Pixel
    /// logic ported from CoPlay ManageTexture + TextureOps.
    /// </summary>
    public class SpriteGenerateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "sprite_generate";

        /// <inheritdoc />
        public override string Description =>
            "Procedurally generate a Texture2D (solid/gradient/checker/border-frame), write a PNG into Assets/, and import it as a Sprite. Returns {path, settings}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Generate the texture, write the PNG, and configure the sprite import.</summary>
        /// <param name="parameters">path, pattern, width, height (required) + pattern + sprite-import options.</param>
        /// <returns>{ path, settings } or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    return new JObject { ["error"] = "path must be project-relative and under Assets/" };
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    return new JObject { ["error"] = "path must end in .png" };

                var pattern = parameters["pattern"]?.ToString();
                if (string.IsNullOrEmpty(pattern))
                    return new JObject { ["error"] = "pattern is required" };

                int width = parameters["width"]?.ToObject<int>() ?? 0;
                int height = parameters["height"]?.ToObject<int>() ?? 0;
                if (width <= 0 || height <= 0)
                    return new JObject { ["error"] = "width and height must be positive" };
                if (width > 4096 || height > 4096)
                    return new JObject { ["error"] = "width/height capped at 4096" };

                var color = ToColor(parameters["color"], new Color(1f, 1f, 1f, 1f));
                var color2 = ToColor(parameters["color2"], new Color(0f, 0f, 0f, 0f));

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                switch (pattern)
                {
                    case "solid":
                        Fill(tex, color);
                        break;
                    case "gradient":
                        RenderGradient(tex, parameters, color, color2);
                        break;
                    case "checker":
                        RenderChecker(tex, parameters, color, color2);
                        break;
                    case "border-frame":
                        RenderBorderFrame(tex, parameters, color, color2);
                        break;
                    default:
                        UnityEngine.Object.DestroyImmediate(tex);
                        return new JObject { ["error"] = $"unknown pattern: {pattern}" };
                }
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                if (png == null)
                    return new JObject { ["error"] = "EncodeToPNG returned null" };

                // Ensure the target folder exists, then write the bytes and import.
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                // Apply the sprite import config (same surface as sprite_import).
                var settings = SpriteImportUtil.Apply(path, parameters);

                return new JObject
                {
                    ["path"] = path,
                    ["pattern"] = pattern,
                    ["width"] = width,
                    ["height"] = height,
                    ["settings"] = settings,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
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
        private static void RenderGradient(Texture2D tex, JObject p, Color color, Color color2)
        {
            var palette = ParsePalette(p["colorStops"]) ?? new List<Color> { color, color2 };
            string type = p["gradientType"]?.ToString() ?? "linear";
            float angle = p["gradientAngle"]?.ToObject<float>() ?? 0f;

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
        private static void RenderChecker(Texture2D tex, JObject p, Color a, Color b)
        {
            int cell = Mathf.Max(1, p["cellSize"]?.ToObject<int>() ?? 16);
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    bool even = ((x / cell) + (y / cell)) % 2 == 0;
                    tex.SetPixel(x, y, even ? a : b);
                }
        }

        /// <summary>Render a `borderThickness` frame in `color` over a `color2` fill.</summary>
        private static void RenderBorderFrame(Texture2D tex, JObject p, Color border, Color fill)
        {
            int t = Mathf.Max(1, p["borderThickness"]?.ToObject<int>() ?? 4);
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

        /// <summary>Parse a [{r,g,b,a}] JSON array into a palette, or null if absent/empty.</summary>
        private static List<Color> ParsePalette(JToken token)
        {
            if (token is not JArray arr) return null;
            var list = new List<Color>();
            foreach (var item in arr)
                if (item is JObject) list.Add(ToColor(item, Color.white));
            return list.Count > 0 ? list : null;
        }

        /// <summary>Parse an {r,g,b,a} (0..255) JSON object into a Color, with a fallback.</summary>
        private static Color ToColor(JToken token, Color fallback)
        {
            if (token is not JObject o) return fallback;
            float r = (o["r"]?.ToObject<float>() ?? fallback.r * 255f) / 255f;
            float g = (o["g"]?.ToObject<float>() ?? fallback.g * 255f) / 255f;
            float b = (o["b"]?.ToObject<float>() ?? fallback.b * 255f) / 255f;
            float a = (o["a"]?.ToObject<float>() ?? fallback.a * 255f) / 255f;
            return new Color(r, g, b, a);
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
