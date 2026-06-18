/**
 * Tool: `scene_save` — save the active scene (or a named scene) to disk. If no
 * path is given the active scene's existing path is used; an untitled scene
 * requires an explicit path. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const sceneSave = {
  name: "scene_save",
  description:
    "Save the active scene via EditorSceneManager.SaveScene. Defaults to the active scene's current path; an untitled scene requires an explicit path. Returns the saved path.",
  inputSchema: {
    path: z
      .string()
      .optional()
      .describe("Target scene path. Required only if the active scene is untitled; otherwise defaults to its current path."),
  },
  /**
   * @param {object} args - { path? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("scene_save", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
