/**
 * Tool: `scene_get_hierarchy` — return a nested tree of every loaded scene's
 * GameObjects. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const sceneGetHierarchy = {
  name: "scene_get_hierarchy",
  description:
    "Return a nested hierarchy ({name, path, active, children[]}) of the root GameObjects of every loaded scene. includeInactive defaults to true.",
  inputSchema: {
    includeInactive: z
      .boolean()
      .optional()
      .describe("Include inactive GameObjects; defaults to true."),
  },
  /**
   * @param {object} args - { includeInactive? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("scene_get_hierarchy", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
