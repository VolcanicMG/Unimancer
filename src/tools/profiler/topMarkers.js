/**
 * Tool: `profiler_top_markers` — best-effort top time markers for the most recent
 * profiled frame, via the internal ProfilerDriver raw-frame-data API. This is
 * version-fragile editor internals; if the data isn't available it returns a note
 * with an empty markers list rather than failing.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const profilerTopMarkers = {
  name: "profiler_top_markers",
  description:
    "Best-effort: return the top time markers (up to ~20) for the most recent profiled frame as [{ marker, totalMs, calls }], read via the internal ProfilerDriver raw-frame-data API. Requires the Profiler to be recording. If the data isn't accessible, returns { note, markers: [] } instead of erroring. Version-fragile. Defaults to 1 frame.",
  inputSchema: {
    frames: z
      .number()
      .int()
      .min(1)
      .optional()
      .describe("How many recent frames to aggregate over; defaults to 1."),
  },
  /**
   * @param {object} args - { frames? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("profiler_top_markers", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
