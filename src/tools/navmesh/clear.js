/**
 * Tool: `navmesh_clear` — clear all baked navmesh data from the scene via the
 * Unity C# bridge, which calls UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes().
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const navmeshClear = {
  name: "navmesh_clear",
  description:
    "Clear all baked navmesh data from the scene via the built-in NavMeshBuilder API. Synchronous. Returns { cleared: true }. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("navmesh_clear", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
