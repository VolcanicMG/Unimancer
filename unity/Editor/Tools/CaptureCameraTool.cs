using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Renders a specific Camera in the scene to a PNG. The camera is resolved
    /// either from a GameObject 'target' (hierarchy path or instanceID, via
    /// <see cref="GoResolve"/>) or from a 'cameraName'. Runs synchronously on the
    /// Unity main thread.
    /// </summary>
    public class CaptureCameraTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "capture_camera";

        /// <inheritdoc />
        public override string Description =>
            "Render a specific Camera (resolved by GameObject target or cameraName) to a PNG at outputPath.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Resolve the target camera and render it to a PNG.
        /// </summary>
        /// <param name="parameters">outputPath (required), target or cameraName, width (default 1280), height (default 720).</param>
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

                var cam = ResolveCamera(parameters);
                if (cam == null)
                    return new JObject { ["error"] = "could not resolve a Camera from 'target' or 'cameraName'" };

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

        /// <summary>
        /// Resolve a Camera from a 'target' (GameObject path/instanceID with a
        /// Camera component) or from a 'cameraName' (a Camera whose GameObject has
        /// that name). Returns null if neither resolves.
        /// </summary>
        /// <param name="parameters">The request parameters.</param>
        /// <returns>The resolved Camera, or null.</returns>
        private static Camera ResolveCamera(JObject parameters)
        {
            var target = parameters["target"]?.ToString();
            if (!string.IsNullOrEmpty(target))
            {
                var go = GoResolve.Resolve(target);
                if (go != null)
                    return go.GetComponent<Camera>();
            }

            var cameraName = parameters["cameraName"]?.ToString();
            if (!string.IsNullOrEmpty(cameraName))
            {
                // Search all loaded cameras (includes inactive) for a name match.
                return Resources.FindObjectsOfTypeAll<Camera>()
                    .FirstOrDefault(c => !string.IsNullOrEmpty(c.gameObject.scene.name)
                                         && c.gameObject.name == cameraName);
            }

            return null;
        }
    }
}
