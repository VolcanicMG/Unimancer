using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Bakes the scene navmesh by building every <c>NavMeshSurface</c> (AI
    /// Navigation package) in the active scene(s). This is the modern, non-
    /// deprecated workflow that replaces the obsolete
    /// <c>UnityEditor.AI.NavMeshBuilder.BuildNavMesh()</c> global bake.
    ///
    /// The package is reached via reflection (see <see cref="NavMeshSurfaceSupport"/>)
    /// so Unimancer stays portable to projects without it. Runs synchronously on
    /// the Unity main thread.
    /// </summary>
    public class NavMeshBakeTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "navmesh_bake";

        /// <inheritdoc />
        public override string Description =>
            "Bake the scene navmesh by building every NavMeshSurface (AI Navigation package) in the active scene. Returns { baked: <surfaceCount> }.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Build the navmesh for each NavMeshSurface in the scene and mark it dirty.
        /// </summary>
        /// <param name="parameters">No parameters.</param>
        /// <returns>{ baked: count } on success, or { error } otherwise.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var surfaceType = NavMeshSurfaceSupport.ResolveSurfaceType();
                if (surfaceType == null)
                    return new JObject { ["error"] = NavMeshSurfaceSupport.MissingPackageMessage };

                var surfaces = NavMeshSurfaceSupport.FindSurfaces(surfaceType);
                if (surfaces.Length == 0)
                    return new JObject { ["error"] = "No NavMeshSurface components found in the active scene. Add one (GameObject > AI > NavMesh Surface) or open the scene that has them." };

                var build = surfaceType.GetMethod("BuildNavMesh", Type.EmptyTypes);
                if (build == null)
                    return new JObject { ["error"] = "NavMeshSurface.BuildNavMesh() not found — unexpected AI Navigation package version." };

                int baked = 0;
                foreach (var surface in surfaces)
                {
                    build.Invoke(surface, null);
                    if (surface is Component comp)
                    {
                        EditorUtility.SetDirty(comp);
                        EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
                    }
                    baked++;
                }
                return new JObject { ["baked"] = baked };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
