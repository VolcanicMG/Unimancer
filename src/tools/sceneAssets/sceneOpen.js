/**
 * Tool: `scene_open` — open a scene in the Editor, either replacing the current
 * scene (Single) or loading it alongside the open scenes (Additive). Forwards to
 * the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const sceneOpen = {
  name: "scene_open",
  description:
    "Open a scene asset in the Editor. additive=true loads it alongside the currently open scenes; otherwise it replaces them (Single mode). Returns the opened scene path.",
  inputSchema: {
    path: z.string().describe("Scene asset path, e.g. 'Assets/Scenes/Main.unity'."),
    additive: z.boolean().optional().describe("Load additively instead of replacing the open scene(s)."),
  },
  /**
   * @param {object} args - { path, additive? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("scene_open", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
