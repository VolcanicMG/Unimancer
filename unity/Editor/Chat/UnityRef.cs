using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Converts Unity objects to/from a stable text handle the chat agent can echo
    /// back, so object references survive the round-trip through plain text:
    ///
    ///   attach object → <see cref="Describe"/> injects "[[unity:&lt;handle&gt;]] …" into the prompt
    ///   agent replies with "[[unity:&lt;handle&gt;]]" → <see cref="Resolve"/> → select + ping
    ///
    /// Handles use <see cref="GlobalObjectId"/> (works for scene objects AND assets and
    /// is NOT one of the Unity 6.5 obsolete-API traps like GetInstanceID/EntityId).
    /// A hierarchy path is accepted as a fallback so the agent can also point at objects
    /// it discovered itself via the MCP tools.
    /// </summary>
    public static class UnityRef
    {
        /// <summary>Stable handle for an object (GlobalObjectId string).</summary>
        public static string Handle(Object o)
        {
            if (o == null) return "";
            return GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
        }

        /// <summary>Resolve a handle back to an object: GlobalObjectId, else a hierarchy path.</summary>
        public static Object Resolve(string handle)
        {
            if (string.IsNullOrEmpty(handle)) return null;
            if (GlobalObjectId.TryParse(handle, out var gid))
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj != null) return obj;
            }
            // Fallback: a scene hierarchy path like "Root/Child/Player".
            var go = GameObject.Find(handle);
            if (go != null) return go;
            // Fallback: an asset path.
            if (handle.Contains("/"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(handle);
                if (asset != null) return asset;
            }
            return null;
        }

        /// <summary>Short display label for chips / links.</summary>
        public static string Label(Object o) => o != null ? o.name : "(missing)";

        /// <summary>Select an object and flash it in the Hierarchy / Project view.</summary>
        public static void Ping(Object o)
        {
            if (o == null) return;
            Selection.activeObject = o;
            EditorGUIUtility.PingObject(o);
        }

        /// <summary>
        /// One context line for the prompt: handle + name + type + path + components,
        /// giving the agent real data plus the handle to refer back to it.
        /// </summary>
        public static string Describe(Object o)
        {
            if (o == null) return "";
            var sb = new StringBuilder();
            sb.Append("- [[unity:").Append(Handle(o)).Append("]] \"").Append(o.name)
              .Append("\" (").Append(o.GetType().Name).Append(")");

            var assetPath = AssetDatabase.GetAssetPath(o);
            if (!string.IsNullOrEmpty(assetPath)) sb.Append(" — asset: ").Append(assetPath);

            var go = AsGameObject(o);
            if (go != null)
            {
                sb.Append(" — hierarchy: ").Append(HierarchyPath(go));
                var comps = go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name);
                sb.Append(" — components: ").Append(string.Join(", ", comps));
                sb.Append(go.activeInHierarchy ? " — active" : " — inactive");
            }
            return sb.ToString();
        }

        private static GameObject AsGameObject(Object o) => o as GameObject ?? (o as Component)?.gameObject;

        private static string HierarchyPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null) { sb.Insert(0, t.name + "/"); t = t.parent; }
            return sb.ToString();
        }
    }
}
