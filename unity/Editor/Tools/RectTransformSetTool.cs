using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Set RectTransform layout in one call: a convenience <c>preset</c> anchor
    /// layout plus any explicit fields (anchorMin/Max, pivot, anchoredPosition,
    /// sizeDelta, offsetMin/Max). The preset is applied first so explicit fields
    /// can override individual values. Registered for Undo.
    /// </summary>
    public class RectTransformSetTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "rect_transform_set";

        /// <inheritdoc />
        public override string Description =>
            "Set RectTransform layout (anchors/pivot/position/size/offsets) in one call, with an optional anchor preset. Returns the resolved layout.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the target's RectTransform and apply preset + fields.</summary>
        /// <param name="parameters">target (required), preset, anchorMin/Max, pivot, anchoredPosition, sizeDelta, offsetMin/Max.</param>
        /// <returns>The resolved RectTransform layout, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var rt = go.GetComponent<RectTransform>();
                if (rt == null)
                    return new JObject { ["error"] = $"GameObject has no RectTransform: {target}" };

                Undo.RecordObject(rt, "Set RectTransform");

                // Apply the preset first; explicit fields below can still override.
                var preset = parameters["preset"]?.ToString();
                if (!string.IsNullOrEmpty(preset))
                {
                    if (!ApplyPreset(rt, preset))
                        return new JObject { ["error"] = $"unknown preset: {preset}" };
                }

                // Order matters: anchors/pivot affect how offsets/positions resolve.
                if (parameters["anchorMin"] is JObject)
                    rt.anchorMin = RectUtil.ToVector2(parameters["anchorMin"], rt.anchorMin);
                if (parameters["anchorMax"] is JObject)
                    rt.anchorMax = RectUtil.ToVector2(parameters["anchorMax"], rt.anchorMax);
                if (parameters["pivot"] is JObject)
                    rt.pivot = RectUtil.ToVector2(parameters["pivot"], rt.pivot);
                if (parameters["anchoredPosition"] is JObject)
                    rt.anchoredPosition = RectUtil.ToVector2(parameters["anchoredPosition"], rt.anchoredPosition);
                if (parameters["sizeDelta"] is JObject)
                    rt.sizeDelta = RectUtil.ToVector2(parameters["sizeDelta"], rt.sizeDelta);
                // offsetMin/offsetMax are set last because they overwrite anchoredPosition/sizeDelta.
                if (parameters["offsetMin"] is JObject)
                    rt.offsetMin = RectUtil.ToVector2(parameters["offsetMin"], rt.offsetMin);
                if (parameters["offsetMax"] is JObject)
                    rt.offsetMax = RectUtil.ToVector2(parameters["offsetMax"], rt.offsetMax);

                EditorUtility.SetDirty(rt);
                return RectUtil.Describe(rt);
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Apply a named anchor preset to a RectTransform. Stretch presets also
        /// zero the offsets; anchored presets set a sensible pivot and zero the
        /// anchored position so the element snaps to the chosen edge/corner.
        /// </summary>
        /// <param name="rt">The RectTransform to configure.</param>
        /// <param name="preset">The preset name.</param>
        /// <returns>True if the preset was recognized.</returns>
        private static bool ApplyPreset(RectTransform rt, string preset)
        {
            switch (preset)
            {
                case "stretch-all":
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    return true;
                case "top-bar":
                    rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(0, rt.offsetMin.y);
                    rt.offsetMax = new Vector2(0, rt.offsetMax.y);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "bottom-bar":
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.offsetMin = new Vector2(0, rt.offsetMin.y);
                    rt.offsetMax = new Vector2(0, rt.offsetMax.y);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "left":
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "right":
                    rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "center":
                    rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    return true;
                case "top-left":
                    SetCorner(rt, 0, 1); return true;
                case "top-right":
                    SetCorner(rt, 1, 1); return true;
                case "bottom-left":
                    SetCorner(rt, 0, 0); return true;
                case "bottom-right":
                    SetCorner(rt, 1, 0); return true;
                default:
                    return false;
            }
        }

        /// <summary>Anchor a RectTransform to a single corner with a matching pivot.</summary>
        /// <param name="rt">The RectTransform to configure.</param>
        /// <param name="x">Corner x anchor (0 left / 1 right).</param>
        /// <param name="y">Corner y anchor (0 bottom / 1 top).</param>
        private static void SetCorner(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(x, y);
            rt.anchorMax = new Vector2(x, y);
            rt.pivot = new Vector2(x, y);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
