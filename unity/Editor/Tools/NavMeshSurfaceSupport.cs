using System;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Reflection bridge to the AI Navigation package's <c>NavMeshSurface</c>.
    ///
    /// WHY reflection: the modern (non-deprecated) navmesh workflow lives in the
    /// optional package <c>com.unity.ai.navigation</c>. Referencing its assembly
    /// directly in the asmdef would make Unimancer fail to compile in any project
    /// that hasn't installed the package. Resolving the type at runtime keeps the
    /// package portable — projects without it get a clear, handled error instead.
    ///
    /// This replaces the obsolete <c>UnityEditor.AI.NavMeshBuilder</c> global bake
    /// (CS0618 in Unity 6): since we never reference the obsolete API at compile
    /// time, no deprecation warning is emitted at all.
    /// </summary>
    internal static class NavMeshSurfaceSupport
    {
        /// <summary>Message returned when the AI Navigation package is not installed.</summary>
        public const string MissingPackageMessage =
            "The AI Navigation package (com.unity.ai.navigation) is not installed, so NavMeshSurface is unavailable. " +
            "Install it via Package Manager to bake/clear navmeshes.";

        /// <summary>
        /// Resolve <c>Unity.AI.Navigation.NavMeshSurface</c> across loaded assemblies.
        /// </summary>
        /// <returns>The NavMeshSurface <see cref="Type"/>, or null when the package is absent.</returns>
        public static Type ResolveSurfaceType()
        {
            var t = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (t != null) return t;
            // Fallback: scan every loaded assembly (the editor loads package assemblies).
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("Unity.AI.Navigation.NavMeshSurface");
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// Find every NavMeshSurface instance in the loaded scenes (including
        /// inactive ones), via the non-deprecated two-arg FindObjectsByType overload.
        /// We omit the FindObjectsSortMode argument (deprecated in Unity 6 — it warns
        /// CS0618) because we bake/clear every surface, so result order is irrelevant.
        /// </summary>
        /// <param name="surfaceType">The resolved NavMeshSurface type.</param>
        /// <returns>All NavMeshSurface components currently in the scene(s).</returns>
        public static UnityEngine.Object[] FindSurfaces(Type surfaceType)
        {
            return UnityEngine.Object.FindObjectsByType(surfaceType, FindObjectsInactive.Include);
        }
    }
}
