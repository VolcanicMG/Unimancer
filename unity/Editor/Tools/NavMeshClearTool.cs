using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Clears baked navmesh data by calling <c>RemoveData()</c> on every
    /// <c>NavMeshSurface</c> (AI Navigation package) in the active scene(s).
    /// Modern, non-deprecated replacement for the obsolete
    /// <c>UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes()</c>. The package is
    /// reached via reflection (see <see cref="NavMeshSurfaceSupport"/>); runs
    /// synchronously on the Unity main thread.
    /// </summary>
    public class NavMeshClearTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "navmesh_clear";

        /// <inheritdoc />
        public override string Description =>
            "Clear baked navmesh data by calling RemoveData() on every NavMeshSurface (AI Navigation package) in the active scene. Returns { cleared: <surfaceCount> }.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Remove navmesh data from each NavMeshSurface in the scene and mark it dirty.
        /// </summary>
        /// <param name="parameters">No parameters.</param>
        /// <returns>{ cleared: count } on success, or { error } otherwise.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var surfaceType = NavMeshSurfaceSupport.ResolveSurfaceType();
                if (surfaceType == null)
                    return new JObject { ["error"] = NavMeshSurfaceSupport.MissingPackageMessage };

                var surfaces = NavMeshSurfaceSupport.FindSurfaces(surfaceType);
                if (surfaces.Length == 0)
                    return new JObject { ["error"] = "No NavMeshSurface components found in the active scene." };

                var remove = surfaceType.GetMethod("RemoveData", Type.EmptyTypes);
                if (remove == null)
                    return new JObject { ["error"] = "NavMeshSurface.RemoveData() not found — unexpected AI Navigation package version." };

                int cleared = 0;
                foreach (var surface in surfaces)
                {
                    remove.Invoke(surface, null);
                    if (surface is Component comp)
                    {
                        EditorUtility.SetDirty(comp);
                        EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
                    }
                    cleared++;
                }
                return new JObject { ["cleared"] = cleared };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
