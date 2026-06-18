using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Renders the Game view to a PNG by rendering Camera.main (falling back to
    /// the first enabled Camera) into an offscreen RenderTexture. Runs
    /// synchronously on the Unity main thread. The Node side reads the saved PNG
    /// and returns it to the model as an image.
    /// </summary>
    public class CaptureGameViewTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "capture_game_view";

        /// <inheritdoc />
        public override string Description =>
            "Render the Game view (Camera.main, or the first enabled camera) to a PNG at outputPath.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Resolve the game camera and render it to a PNG.
        /// </summary>
        /// <param name="parameters">outputPath (required), width (default 1280), height (default 720).</param>
        /// <returns>{ path, width, height }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var outputPath = parameters["outputPath"]?.ToString();
                var guard = CaptureUtil.ValidateOutputPath(outputPath);
                if (guard != null)
                    return guard;

                var width = CaptureUtil.IntOr(parameters, "width", 1280);
                var height = CaptureUtil.IntOr(parameters, "height", 720);

                // Prefer the tagged main camera; otherwise the first enabled camera.
                var cam = Camera.main;
                if (cam == null)
                    cam = Camera.allCameras.FirstOrDefault(c => c.enabled);
                if (cam == null)
                    return new JObject { ["error"] = "no camera found (no Camera.main and no enabled camera in the scene)" };

                CaptureUtil.RenderCameraToPng(cam, width, height, outputPath);

                return new JObject
                {
                    ["path"] = outputPath,
                    ["width"] = width,
                    ["height"] = height,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
