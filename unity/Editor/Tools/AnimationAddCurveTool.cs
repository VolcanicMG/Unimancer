using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Add (or replace) an animated curve on an existing <see cref="AnimationClip"/>.
    /// Builds an <see cref="AnimationCurve"/> from {time,value} keyframes and binds
    /// it to a serialized property of a resolved component type via
    /// <c>AnimationClip.SetCurve</c>.
    /// </summary>
    public class AnimationAddCurveTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "animation_add_curve";

        /// <inheritdoc />
        public override string Description =>
            "Add a curve to a clip via SetCurve(relativePath, componentType, property, curve) built from {time,value} keys.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Load the clip, build the curve, bind it, and save.</summary>
        /// <param name="parameters">clipPath, relativePath, componentType, property, keys (all required).</param>
        /// <returns>{ keys:&lt;n&gt; } or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var clipPath = parameters["clipPath"]?.ToString();
                if (string.IsNullOrEmpty(clipPath))
                    return new JObject { ["error"] = "clipPath is required" };

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                    return new JObject { ["error"] = $"AnimationClip not found: {clipPath}" };

                // relativePath may legitimately be "" (the animated root).
                var relativePath = parameters["relativePath"]?.ToString() ?? "";

                var componentType = parameters["componentType"]?.ToString();
                var type = ComponentTypeResolve.Resolve(componentType);
                if (type == null)
                    return new JObject { ["error"] = $"component type not found: {componentType}" };

                var property = parameters["property"]?.ToString();
                if (string.IsNullOrEmpty(property))
                    return new JObject { ["error"] = "property is required" };

                if (parameters["keys"] is not JArray keysArr)
                    return new JObject { ["error"] = "keys array is required" };

                var keyframes = new Keyframe[keysArr.Count];
                for (var i = 0; i < keysArr.Count; i++)
                {
                    var k = keysArr[i];
                    var time = k["time"] != null ? (float)k["time"] : 0f;
                    var value = k["value"] != null ? (float)k["value"] : 0f;
                    keyframes[i] = new Keyframe(time, value);
                }

                var curve = new AnimationCurve(keyframes);
                clip.SetCurve(relativePath, type, property, curve);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                return new JObject { ["keys"] = keyframes.Length };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
