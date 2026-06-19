using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Move/rotate/zoom a camera in the RUNNING game so an agent can pan the view
    /// around to inspect or test the scene. Operates directly on the camera transform
    /// (UnityEngine only), so it works regardless of how the game handles input —
    /// unlike simulated drag input, which only reaches EventSystem-based UI.
    /// </summary>
    public class CameraControlTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_camera_control";

        /// <inheritdoc/>
        public override string Description =>
            "Pan/move/rotate/zoom a camera in the RUNNING game to look around the scene. params (all optional): camera (name; default Camera.main), position [x,y,z] (absolute), rotation [x,y,z] euler (absolute), move [x,y,z] (relative, camera-local: +Z forward, +X right, +Y up), rotate [pitch,yaw,roll] (relative degrees), fov (perspective), orthoSize (orthographic). Returns the resulting camera transform.";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var cam = ResolveCamera(parameters["camera"]?.ToString());
                if (cam == null) return new JObject { ["error"] = "no camera found (no Camera.main and no enabled camera)" };

                var t = cam.transform;
                if (TryVec3(parameters, "position", out var pos)) t.position = pos;
                if (TryVec3(parameters, "rotation", out var rot)) t.eulerAngles = rot;
                if (TryVec3(parameters, "move", out var mv)) t.Translate(mv, Space.Self);
                if (TryVec3(parameters, "rotate", out var rr)) t.Rotate(rr, Space.Self);
                if (parameters["fov"] != null) cam.fieldOfView = (float)parameters["fov"];
                if (parameters["orthoSize"] != null) cam.orthographicSize = (float)parameters["orthoSize"];

                return new JObject
                {
                    ["camera"] = RtResolve.Path(cam.gameObject),
                    ["position"] = Vec(t.position),
                    ["rotation"] = Vec(t.eulerAngles),
                    ["orthographic"] = cam.orthographic,
                    ["fov"] = cam.fieldOfView,
                    ["orthoSize"] = cam.orthographicSize,
                };
            }
            catch (Exception e) { return new JObject { ["error"] = e.Message }; }
        }

        /// <summary>Resolve a camera by name, else Camera.main, else the first enabled camera.</summary>
        private static Camera ResolveCamera(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var go = RtResolve.Resolve(name);
                var c = go != null ? go.GetComponent<Camera>() : null;
                if (c != null) return c;
            }
            return Camera.main != null ? Camera.main : Camera.allCameras.FirstOrDefault(x => x.enabled);
        }

        /// <summary>Parse a [x,y,z] JSON array parameter into a Vector3.</summary>
        private static bool TryVec3(JObject p, string key, out Vector3 v)
        {
            v = default;
            if (!(p[key] is JArray a) || a.Count < 3) return false;
            v = new Vector3((float)a[0], (float)a[1], (float)a[2]);
            return true;
        }

        /// <summary>Vector3 → [x,y,z] JSON array.</summary>
        private static JArray Vec(Vector3 v) => new JArray { v.x, v.y, v.z };
    }
}
