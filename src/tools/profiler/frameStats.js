/**
 * Tool: `profiler_frame_stats` — sample named profiler counters over N Editor
 * frames via the public Unity.Profiling.ProfilerRecorder API and return per-stat
 * last/avg/max with units. Async on the C# side: it collects one sample per
 * EditorApplication.update tick for `frames` frames before returning.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const profilerFrameStats = {
  name: "profiler_frame_stats",
  description:
    "Sample named profiler counters (Main Thread, GC.Alloc, Draw Calls, Batches, Triangles, Vertices, etc.) over `frames` Editor frames using ProfilerRecorder, then return per-stat { name, lastValue, avg, max, unit }. Invalid/unavailable counters on this Unity version are skipped and listed under `skipped`. Defaults to 60 frames. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    frames: z
      .number()
      .int()
      .min(1)
      .optional()
      .describe("Number of Editor frames to sample over; defaults to 60."),
  },
  /**
   * @param {object} args - { frames? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("profiler_frame_stats", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
