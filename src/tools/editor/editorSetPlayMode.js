/**
 * Tool: `editor_set_play_mode` — enter play/pause/stop. Forwards to the Unity
 * C# bridge, which toggles EditorApplication.isPlaying / isPaused.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const editorSetPlayMode = {
  name: "editor_set_play_mode",
  description:
    "Set the Editor play mode. mode='play' enters play mode, 'pause' pauses, 'stop' exits play mode. Returns the new Editor state.",
  inputSchema: {
    mode: z.enum(["play", "pause", "stop"]).describe("Target play mode."),
  },
  /**
   * @param {object} args - { mode }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("editor_set_play_mode", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
