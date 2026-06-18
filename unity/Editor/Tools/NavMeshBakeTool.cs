using System;
using Newtonsoft.Json.Linq;
using UnityEditor.AI;

namespace Unimancer
{
    /// <summary>
    /// Bakes the scene navmesh using the built-in (legacy) NavMeshBuilder API.
    /// Runs synchronously on the Unity main thread.
    ///
    /// Note: projects using the AI Navigation package (NavMeshSurface components)
    /// bake per-surface and do NOT use NavMeshBuilder; this tool covers the
    /// built-in/legacy navigation workflow. UnityEditor.AI.NavMeshBuilder is the
    /// long-standing Editor API for that workflow and ships with the Editor.
    /// </summary>
    public class NavMeshBakeTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "navmesh_bake";

        /// <inheritdoc />
        public override string Description =>
            "Bake the scene navmesh using the built-in (legacy) NavMeshBuilder API.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Build the navmesh for the current scene.
        /// </summary>
        /// <param name="parameters">No parameters.</param>
        /// <returns>{ baked: true }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                NavMeshBuilder.BuildNavMesh();
                return new JObject { ["baked"] = true };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
