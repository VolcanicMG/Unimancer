using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Frames every Renderer in the active scene and renders it from several
    /// canonical angles (front, back, left, right, top, and a 3/4 perspective)
    /// using a temporary throwaway camera, saving one PNG per angle. The temp
    /// camera is always destroyed (DestroyImmediate) before returning. Runs
    /// synchronously on the Unity main thread.
    /// </summary>
    public class CaptureSceneViewMultiAngleTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "capture_scene_view_multi_angle";

        /// <inheritdoc />
        public override string Description =>
            "Frame all scene renderers and render front/back/left/right/top/3-4 angles to PNGs in outputDir.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>A named viewing direction: the direction FROM the bounds center TO the camera.</summary>
        private static readonly (string name, Vector3 dir)[] Angles =
        {
            ("front", new Vector3(0f, 0f, -1f)),
            ("back", new Vector3(0f, 0f, 1f)),
            ("left", new Vector3(-1f, 0f, 0f)),
            ("right", new Vector3(1f, 0f, 0f)),
            ("top", new Vector3(0f, 1f, 0.0001f)),
            ("threequarter", new Vector3(1f, 0.8f, -1f)),
        };

        /// <summary>
        /// Compute scene bounds, render each angle through a temporary camera, and
        /// write the PNGs.
        /// </summary>
        /// <param name="parameters">outputDir (required), prefix (default "view"), width (default 1024), height (default 1024).</param>
        /// <returns>{ paths, count }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var outputDir = parameters["outputDir"]?.ToString();
                var guard = CaptureUtil.ValidateOutputPath(outputDir);
                if (guard != null)
                    return guard;

                var prefix = parameters["prefix"]?.ToString();
                if (string.IsNullOrEmpty(prefix))
                    prefix = "view";

                var width = CaptureUtil.IntOr(parameters, "width", 1024);
                var height = CaptureUtil.IntOr(parameters, "height", 1024);

                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                var bounds = ComputeSceneBounds();
                if (bounds == null)
                    return new JObject { ["error"] = "no renderers found in the active scene to frame" };

                var b = bounds.Value;
                // Distance needed to fit the bounding sphere in view, with margin.
                var radius = b.extents.magnitude;
                if (radius < 0.0001f)
                    radius = 1f;

                var paths = new List<string>();

                // Create one throwaway camera and reuse it for every angle.
                var camGo = new GameObject("__UnimancerMultiAngleCam");
                camGo.hideFlags = HideFlags.HideAndDontSave;
                var cam = camGo.AddComponent<Camera>();
                try
                {
                    cam.fieldOfView = 60f;
                    cam.nearClipPlane = 0.01f;
                    cam.farClipPlane = radius * 10f + 1000f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.15f, 0.15f, 0.17f, 1f);
                    cam.aspect = (float)width / height;

                    // Distance so the sphere fits the (smaller) vertical FOV with margin.
                    var halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                    var distance = (radius / Mathf.Sin(halfFov)) * 1.3f;

                    foreach (var (name, dir) in Angles)
                    {
                        var d = dir.normalized;
                        cam.transform.position = b.center + d * distance;
                        cam.transform.LookAt(b.center, Vector3.up);

                        var path = Path.Combine(outputDir, $"{prefix}_{name}.png");
                        CaptureUtil.RenderCameraToPng(cam, width, height, path);
                        paths.Add(path);
                    }
                }
                finally
                {
                    // Always remove the temp camera, even if a render throws.
                    UnityEngine.Object.DestroyImmediate(camGo);
                }

                return new JObject
                {
                    ["paths"] = new JArray(paths.Cast<object>().ToArray()),
                    ["count"] = paths.Count,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Compute the combined world-space bounds of every active Renderer in the
        /// scene. Returns null if there are no renderers.
        /// </summary>
        /// <returns>The encapsulating bounds, or null if the scene has no renderers.</returns>
        private static Bounds? ComputeSceneBounds()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (renderers == null || renderers.Length == 0)
                return null;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
