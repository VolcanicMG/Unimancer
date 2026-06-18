/**
 * Tool: `profiler_enable` — toggle the Editor profiler on/off and optionally deep
 * profiling, via the internal-ish ProfilerDriver editor API. Returns the new
 * state.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const profilerEnable = {
  name: "profiler_enable",
  description:
    "Enable or disable the Unity Editor profiler (ProfilerDriver.enabled) and optionally deep profiling (ProfilerDriver.deepProfiling). Returns the resulting { enabled, deepProfiling } state. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    enabled: z.boolean().describe("Turn the profiler recording on (true) or off (false)."),
    deepProfile: z
      .boolean()
      .optional()
      .describe("If set, also toggle deep profiling to this value."),
  },
  /**
   * @param {object} args - { enabled, deepProfile? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("profiler_enable", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
