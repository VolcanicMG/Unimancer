/**
 * Tool: `runtime_log_tail` — tail recent log lines from the RUNNING game.
 * Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeLogTail = {
  name: "runtime_log_tail",
  description:
    "Tail recent log lines (Debug.Log/Warning/Error) from the RUNNING game. Returns { logs } (newest last).",
  inputSchema: {
    count: z.number().int().optional().describe("How many recent lines to return; defaults to 50."),
  },
  /**
   * @param {object} args - { count? }.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_log_tail", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
