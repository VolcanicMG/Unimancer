using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Renders the active Scene view's camera to a PNG. Uses
    /// SceneView.lastActiveSceneView so the capture reflects the editor's current
    /// framing/navigation. Runs synchronously on the Unity main thread.
    /// </summary>
    public class CaptureSceneViewTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "capture_scene_view";

        /// <inheritdoc />
        public override string Description =>
            "Render the active Scene view camera to a PNG at outputPath.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Resolve the Scene view camera and render it to a PNG.
        /// </summary>
        /// <param name="parameters">outputPath (required), width (default 1280), height (default 720).</param>
        /// <returns>{ path, width, height }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                // outputPath optional — omit to return the image inline (avoids
                // WSL/Windows path mismatches); provide it to also save Editor-side.
                var outputPath = parameters["outputPath"]?.ToString();
                if (!string.IsNullOrEmpty(outputPath))
                {
                    var guard = CaptureUtil.ValidateOutputPath(outputPath);
                    if (guard != null)
                        return guard;
                }

                var width = CaptureUtil.IntOr(parameters, "width", 1280);
                var height = CaptureUtil.IntOr(parameters, "height", 720);

                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null || sceneView.camera == null)
                    return new JObject { ["error"] = "no Scene view is open" };

                var png = CaptureUtil.RenderCameraToPngBytes(sceneView.camera, width, height);
                if (!string.IsNullOrEmpty(outputPath))
                    CaptureUtil.WritePng(outputPath, png);

                return new JObject
                {
                    ["base64"] = Convert.ToBase64String(png),
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
