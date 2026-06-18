/**
 * Tool: `scene_list` — list the scenes registered in Build Settings and the
 * scenes currently open in the Editor. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const sceneList = {
  name: "scene_list",
  description:
    "List Build Settings scenes (path + enabled) and the currently open scenes (path, isLoaded, isDirty).",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("scene_list", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
