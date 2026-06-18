using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Shared helpers for the Visual Capture tools: a path-safety check for
    /// capture output (captures may legitimately be written outside Assets/, so
    /// this only rejects path traversal via "..", unlike <see cref="PathGuard"/>
    /// which pins writes to Assets/) and the camera-to-PNG render routine used by
    /// every capture tool.
    /// </summary>
    public static class CaptureUtil
    {
        /// <summary>
        /// Validate a capture output path. Unlike asset writes, captures may go to
        /// any location the user chose (e.g. a temp dir), so this only blocks the
        /// "../" traversal pattern; absolute paths are allowed.
        /// </summary>
        /// <param name="path">The candidate output path.</param>
        /// <returns>Null if allowed; otherwise a { error } JObject to return.</returns>
        public static JObject ValidateOutputPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new JObject { ["error"] = "output path is required" };

            // Normalize backslashes so the check cannot be sidestepped on Windows.
            var normalized = path.Replace('\\', '/');
            if (normalized.Contains(".."))
                return new JObject { ["error"] = $"path may not contain '..': {path}" };

            return null;
        }

        /// <summary>
        /// Render a camera into an offscreen RenderTexture at width x height, read
        /// the pixels into a Texture2D, encode to PNG, and write the bytes to disk.
        /// Restores the camera's previous targetTexture and the active RenderTexture,
        /// and cleans up the temporary resources.
        /// </summary>
        /// <param name="cam">The camera to render (must not be null).</param>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <param name="outputPath">Absolute file path for the PNG.</param>
        public static void RenderCameraToPng(Camera cam, int width, int height, string outputPath)
        {
            if (cam == null)
                throw new ArgumentNullException(nameof(cam));
            if (width <= 0 || height <= 0)
                throw new ArgumentException("width and height must be positive.");

            // Ensure the destination directory exists before writing.
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                var png = tex.EncodeToPNG();
                File.WriteAllBytes(outputPath, png);
            }
            finally
            {
                // Always restore state and free GPU/CPU resources, even on failure.
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>Read an int parameter with a default fallback.</summary>
        /// <param name="parameters">The request parameters.</param>
        /// <param name="key">Parameter name.</param>
        /// <param name="fallback">Value to use when absent.</param>
        /// <returns>The parsed int, or the fallback.</returns>
        public static int IntOr(JObject parameters, string key, int fallback)
        {
            return parameters[key] != null ? (int)parameters[key] : fallback;
        }
    }
}
