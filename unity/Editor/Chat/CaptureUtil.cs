using System;
using System.IO;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Camera-to-PNG render routine used by the chat window's "capture view"
    /// attachment. (The MCP capture tools that also used this now live in
    /// com.unity.pipeline's built-in <c>screenshot</c>/<c>capture_*</c> commands.)
    /// </summary>
    public static class CaptureUtil
    {
        /// <summary>
        /// Render a camera into an offscreen RenderTexture at width x height, read
        /// the pixels into a Texture2D, and return the encoded PNG bytes. Restores
        /// the camera's previous targetTexture and the active RenderTexture and
        /// frees the temporaries.
        /// </summary>
        /// <param name="cam">The camera to render (must not be null).</param>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <returns>The PNG-encoded bytes.</returns>
        public static byte[] RenderCameraToPngBytes(Camera cam, int width, int height)
        {
            if (cam == null)
                throw new ArgumentNullException(nameof(cam));
            if (width <= 0 || height <= 0)
                throw new ArgumentException("width and height must be positive.");

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

                return tex.EncodeToPNG();
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

        /// <summary>Render a camera and write the PNG to disk, creating the directory if needed.</summary>
        /// <param name="cam">The camera to render.</param>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <param name="outputPath">Absolute file path for the PNG.</param>
        public static void RenderCameraToPng(Camera cam, int width, int height, string outputPath)
        {
            var png = RenderCameraToPngBytes(cam, width, height);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(outputPath, png);
        }
    }
}
