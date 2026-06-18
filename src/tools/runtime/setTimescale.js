/**
 * Tool: `runtime_set_timescale` — set Time.timeScale on the RUNNING game.
 * Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeSetTimescale = {
  name: "runtime_set_timescale",
  description:
    "Set Time.timeScale on the RUNNING game. 0 = pause, 1 = normal, >1 = fast-forward. Returns { timeScale }.",
  inputSchema: {
    timeScale: z.number().min(0).describe("New time scale (>= 0). 0 pauses, 1 is normal, >1 is fast."),
  },
  /**
   * @param {object} args - { timeScale }.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_set_timescale", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
