/**
 * Tool: `runtime_scene_info` — snapshot the RUNNING game's scene state. Forwards
 * to the in-build/Play-mode runtime bridge (port 8091), not the Editor bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeSceneInfo = {
  name: "runtime_scene_info",
  description:
    "Snapshot of the RUNNING game (Play mode or a dev build): active/loaded scenes, root object count, timeScale, time, frameCount, pause state, target frame rate, and platform.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_scene_info", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
