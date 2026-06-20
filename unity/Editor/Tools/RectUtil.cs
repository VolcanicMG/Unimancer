using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Shared helpers for the UI tools: parsing {x,y} JSON into Vector2 and
    /// serializing a RectTransform's full layout. Kept separate from GoResolve
    /// (Vector3) so the UI group has one place for 2D rect math.
    /// </summary>
    public static class RectUtil
    {
        /// <summary>
        /// Read an {x,y} JSON object into a Vector2, using the supplied fallback
        /// for any missing component.
        /// </summary>
        /// <param name="obj">The JSON object (may be null).</param>
        /// <param name="fallback">Values to use when the object or a field is absent.</param>
        /// <returns>The parsed Vector2.</returns>
        public static Vector2 ToVector2(JToken obj, Vector2 fallback)
        {
            if (obj is not JObject o)
                return fallback;
            return new Vector2(
                o["x"] != null ? (float)o["x"] : fallback.x,
                o["y"] != null ? (float)o["y"] : fallback.y);
        }

        /// <summary>Serialize a Vector2 as a {x,y} JObject.</summary>
        /// <param name="v">The vector to serialize.</param>
        /// <returns>A {x,y} JObject.</returns>
        public static JObject FromVector2(Vector2 v) =>
            new JObject { ["x"] = v.x, ["y"] = v.y };

        /// <summary>
        /// Serialize a RectTransform's full layout: anchors, pivot, anchored
        /// position, size delta, and the computed local rect.
        /// </summary>
        /// <param name="rt">The RectTransform to describe.</param>
        /// <returns>A JObject with anchorMin/Max, pivot, anchoredPosition, sizeDelta, rect.</returns>
        public static JObject Describe(RectTransform rt)
        {
            return new JObject
            {
                ["anchorMin"] = FromVector2(rt.anchorMin),
                ["anchorMax"] = FromVector2(rt.anchorMax),
                ["pivot"] = FromVector2(rt.pivot),
                ["anchoredPosition"] = FromVector2(rt.anchoredPosition),
                ["sizeDelta"] = FromVector2(rt.sizeDelta),
                ["offsetMin"] = FromVector2(rt.offsetMin),
                ["offsetMax"] = FromVector2(rt.offsetMax),
                ["rect"] = new JObject
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
