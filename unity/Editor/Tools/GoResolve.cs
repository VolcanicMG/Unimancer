using System;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Shared helpers for the GameObject &amp; Component tools: resolving a
    /// GameObject from a loose "target" argument and building a hierarchy path.
    /// A target may be an integer instanceID (string or number) or a hierarchy
    /// path such as "Parent/Child".
    /// </summary>
    public static class GoResolve
    {
        /// <summary>
        /// Resolve a GameObject from a target value. The value is first tried as
        /// an integer instanceID, then as a slash-delimited hierarchy path.
        /// </summary>
        /// <param name="target">An instanceID (int) or a hierarchy path string.</param>
        /// <returns>The matching GameObject, or null if none was found.</returns>
        public static GameObject Resolve(string target)
        {
            if (string.IsNullOrEmpty(target))
                return null;

            // Try instanceID first (handles "12345" and bare integers).
            if (ulong.TryParse(target, out var id))
            {
                // Unity 6.5: instance IDs are EntityId handles (no int conversion).
                var obj = UnityEditor.EditorUtility.EntityIdToObject(EntityId.FromULong(id)) as GameObject;
                if (obj != null)
                    return obj;
            }

            return FindByPath(target);
        }

        /// <summary>
        /// Find a GameObject by walking a slash-delimited hierarchy path from a
        /// scene root, e.g. "World/Player/Hand". Matches active and inactive
        /// objects; returns the first match.
        /// </summary>
        /// <param name="path">Slash-delimited hierarchy path.</param>
        /// <returns>The matching GameObject, or null.</returns>
        public static GameObject FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('/');

            // Search every loaded GameObject for one whose computed path matches.
            // Resources.FindObjectsOfTypeAll includes inactive objects.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                // Skip assets/prefabs not in a scene.
                if (string.IsNullOrEmpty(go.scene.name))
                    continue;
                if (go.name != parts[parts.Length - 1])
                    continue;
                if (Path(go) == path)
                    return go;
            }
            return null;
        }

        /// <summary>
        /// Build the full hierarchy path ("Parent/Child/Leaf") for a transform.
        /// </summary>
        /// <param name="go">The GameObject to describe.</param>
        /// <returns>The slash-delimited path from the scene root.</returns>
        public static string Path(GameObject go)
        {
            if (go == null)
                return null;
            var t = go.transform;
            var p = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                p = t.name + "/" + p;
            }
            return p;
        }

        /// <summary>
        /// Read an {x,y,z} JSON object into a Vector3, using the supplied
        /// fallback for any missing component.
        /// </summary>
        /// <param name="obj">The JSON object (may be null).</param>
        /// <param name="fallback">Values to use when the object or a field is absent.</param>
        /// <returns>The parsed Vector3.</returns>
        public static Vector3 ToVector3(Newtonsoft.Json.Linq.JToken obj, Vector3 fallback)
        {
            if (obj is not Newtonsoft.Json.Linq.JObject o)
                return fallback;
            return new Vector3(
                o["x"] != null ? (float)o["x"] : fallback.x,
                o["y"] != null ? (float)o["y"] : fallback.y,
                o["z"] != null ? (float)o["z"] : fallback.z);
        }
    }
}
