/**
 * Tool: `scene_new` — create a new empty or default scene, optionally saving it
 * to a path immediately. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const sceneNew = {
  name: "scene_new",
  description:
    "Create a new scene via EditorSceneManager.NewScene. setup='defaultGameObjects' (default, adds Main Camera + Directional Light) or 'empty'. If path is given the new scene is saved there. Returns the new scene info.",
  inputSchema: {
    setup: z
      .enum(["empty", "defaultGameObjects"])
      .optional()
      .describe("Scene setup; defaults to defaultGameObjects."),
    path: z.string().optional().describe("If provided, save the new scene to this path."),
  },
  /**
   * @param {object} args - { setup?, path? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("scene_new", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
