using System;
using System.Linq;

namespace Unimancer
{
    /// <summary>
    /// Resolve a component <see cref="Type"/> from a loose type name (short like
    /// "Rigidbody" or fully qualified like "UnityEngine.BoxCollider"). Tries
    /// Type.GetType first, then scans all loaded assemblies for a matching type.
    /// </summary>
    public static class ComponentTypeResolve
    {
        /// <summary>
        /// Find a Type whose full name or short name matches <paramref name="typeName"/>.
        /// </summary>
        /// <param name="typeName">Short or fully qualified type name.</param>
        /// <returns>The matched Type, or null if none was found.</returns>
        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // Direct hit (assembly-qualified or already-loaded full names).
            var direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            // Common Unity namespaces tried explicitly for short names.
            foreach (var ns in new[] { "UnityEngine", "UnityEngine.UI", "UnityEngine.EventSystems" })
            {
                var t = Type.GetType($"{ns}.{typeName}, UnityEngine");
                if (t != null) return t;
            }

            // Full scan: match on FullName or simple Name, exact then case-insensitive.
            var all = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => typeof(UnityEngine.Component).IsAssignableFrom(t))
                .ToArray();

            return all.FirstOrDefault(t => t.FullName == typeName)
                ?? all.FirstOrDefault(t => t.Name == typeName)
                ?? all.FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
