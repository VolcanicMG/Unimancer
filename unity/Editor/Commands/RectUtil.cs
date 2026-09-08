using System.Collections.Generic;
using UnityEngine;

namespace Unimancer.Commands
{
    /// <summary>
    /// Shared serialization helper for the UI commands: describing a
    /// RectTransform's full layout as a plain, JSON-serializable object.
    /// </summary>
    public static class RectUtil
    {
        /// <summary>Serialize a Vector2 as a {x,y} map.</summary>
        /// <param name="v">The vector to serialize.</param>
        /// <returns>A {x,y} dictionary.</returns>
        public static Dictionary<string, object> FromVector2(Vector2 v) =>
            new Dictionary<string, object> { ["x"] = v.x, ["y"] = v.y };

        /// <summary>
        /// Serialize a RectTransform's full layout: anchors, pivot, anchored
        /// position, size delta, offsets, and the computed local rect.
        /// </summary>
        /// <param name="rt">The RectTransform to describe.</param>
        /// <returns>A map with anchorMin/Max, pivot, anchoredPosition, sizeDelta, offsetMin/Max, rect.</returns>
        public static Dictionary<string, object> Describe(RectTransform rt)
        {
            return new Dictionary<string, object>
            {
                ["anchorMin"] = FromVector2(rt.anchorMin),
                ["anchorMax"] = FromVector2(rt.anchorMax),
                ["pivot"] = FromVector2(rt.pivot),
                ["anchoredPosition"] = FromVector2(rt.anchoredPosition),
                ["sizeDelta"] = FromVector2(rt.sizeDelta),
                ["offsetMin"] = FromVector2(rt.offsetMin),
                ["offsetMax"] = FromVector2(rt.offsetMax),
                ["rect"] = new Dictionary<string, object>
                {
                    ["x"] = rt.rect.x,
                    ["y"] = rt.rect.y,
                    ["width"] = rt.rect.width,
                    ["height"] = rt.rect.height,
                },
            };
        }
    }
}
