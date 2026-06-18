/**
 * Tool: `animation_list_clips` — list AnimationClip assets under a folder.
 * Forwards to the Unity C# bridge, which uses AssetDatabase.FindAssets to
 * collect clip asset paths (capped to keep responses bounded).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const animationListClips = {
  name: "animation_list_clips",
  description:
    "List AnimationClip asset paths under a folder (defaults to Assets). Results are capped at ~200. Returns { clips:[...], count }. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    folder: z.string().optional().describe("Folder to search under; defaults to Assets."),
  },
  /**
   * @param {object} args - { folder? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("animation_list_clips", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
