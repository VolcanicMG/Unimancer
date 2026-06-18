/**
 * Tool: `animation_create_clip` — create a new AnimationClip asset on disk.
 * Forwards to the Unity C# bridge, which constructs an AnimationClip with the
 * requested frame rate (and optional legacy flag) and writes it with
 * AssetDatabase.CreateAsset. The path is PathGuard-validated on the C# side.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const animationCreateClip = {
  name: "animation_create_clip",
  description:
    "Create a new AnimationClip asset at path (must be under Assets/ and end in .anim). frameRate defaults to 60. Set legacy=true for a legacy clip usable by the Animation component. Returns { created:<path> }. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    path: z.string().describe("Asset path for the clip, e.g. Assets/Anim/Walk.anim (under Assets/)."),
    frameRate: z.number().optional().describe("Clip sample frame rate; defaults to 60."),
    legacy: z.boolean().optional().describe("Create a legacy AnimationClip; defaults to false."),
  },
  /**
   * @param {object} args - clip creation options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("animation_create_clip", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
