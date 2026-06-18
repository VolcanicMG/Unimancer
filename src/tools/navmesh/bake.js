/**
 * Tool: `navmesh_bake` — bake the scene navmesh using the built-in (legacy)
 * editor API. Forwards to the Unity C# bridge, which calls
 * UnityEditor.AI.NavMeshBuilder.BuildNavMesh() synchronously.
 *
 * Note: projects using the AI Navigation package (NavMeshSurface components)
 * bake differently (per-surface, not via NavMeshBuilder); this tool covers the
 * built-in/legacy navmesh workflow.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const navmeshBake = {
  name: "navmesh_bake",
  description:
    "Bake the scene navmesh using the built-in (legacy) NavMeshBuilder API. Synchronous. Returns { baked: true }. Note: projects using the AI Navigation package (NavMeshSurface) bake differently. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("navmesh_bake", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
