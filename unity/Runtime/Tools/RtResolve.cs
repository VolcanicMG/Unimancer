using System;
using System.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Shared helpers for runtime tools: resolving GameObjects/Types by name in a
    /// RUNNING game using ONLY UnityEngine APIs (no UnityEditor). Used by tools that
    /// need to locate scene objects, their components, or types via reflection.
    /// </summary>
    public static class RtResolve
    {
        /// <summary>
        /// Find a GameObject by hierarchy path or name. First tries
        /// GameObject.Find (active objects, supports "Parent/Child" paths); if that
        /// misses, scans ALL GameObjects (incl. inactive) for a name match.
        /// </summary>
        /// <param name="target">Hierarchy path or plain name of the object.</param>
        /// <returns>The matching GameObject, or null if none found.</returns>
        public static GameObject Resolve(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;

            var found = GameObject.Find(target);
            if (found != null) return found;

            // GameObject.Find only sees active objects; fall back to a full scan so
            // inactive objects (and bare-name matches) are still reachable.
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(g => g.name == target);
        }

        /// <summary>
        /// Resolve a System.Type by name. Tries Type.GetType first, then scans every
        /// loaded assembly for a type whose Name or FullName matches.
        /// </summary>
        /// <param name="name">Simple name or fully-qualified type name.</param>
        /// <returns>The matching Type, or null if none found.</returns>
        public static Type ResolveType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var t = Type.GetType(name);
            if (t != null) return t;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; } // some assemblies refuse GetTypes(); skip them.

                var match = types.FirstOrDefault(x => x.Name == name || x.FullName == name);
                if (match != null) return match;
            }
            return null;
        }

        /// <summary>
        /// Build the full hierarchy path ("Root/Child/Leaf") of a GameObject.
        /// </summary>
        /// <param name="go">The object to describe.</param>
        /// <returns>Slash-delimited path from the scene root, or "" if null.</returns>
        public static string Path(GameObject go)
        {
            if (go == null) return "";
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }
    }
}
