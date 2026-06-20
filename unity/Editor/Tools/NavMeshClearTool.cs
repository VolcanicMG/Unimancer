using System;
using Newtonsoft.Json.Linq;
using UnityEditor.AI;

namespace Unimancer
{
    /// <summary>
    /// Clears all baked navmesh data from the scene using the built-in (legacy)
    /// NavMeshBuilder API. Runs synchronously on the Unity main thread.
    /// </summary>
    public class NavMeshClearTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "navmesh_clear";

        /// <inheritdoc />
        public override string Description =>
            "Clear all baked navmesh data from the scene using the built-in NavMeshBuilder API.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Clear all navmeshes from the current scene.
        /// </summary>
        /// <param name="parameters">No parameters.</param>
        /// <returns>{ cleared: true }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                // See NavMeshBakeTool: UnityEditor.AI.NavMeshBuilder is [Obsolete] in Unity 6
                // with no non-deprecated drop-in for the legacy workflow; suppress CS0618.
#pragma warning disable 0618
                NavMeshBuilder.ClearAllNavMeshes();
#pragma warning restore 0618
                return new JObject { ["cleared"] = true };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
