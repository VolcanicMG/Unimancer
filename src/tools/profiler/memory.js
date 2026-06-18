/**
 * Tool: `profiler_memory` — read the Unity Editor's current memory usage via the
 * public UnityEngine.Profiling.Profiler API. No parameters; returns a snapshot
 * of allocated/reserved/mono/graphics memory in megabytes.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const profilerMemory = {
  name: "profiler_memory",
  description:
    "Return a snapshot of current Unity memory usage in MB: totalAllocatedMB, totalReservedMB, monoUsedMB, monoHeapMB, gfxDriverMB. Uses the public UnityEngine.Profiling.Profiler API. No parameters. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {},
  /**
   * @param {object} args - unused (no parameters).
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("profiler_memory", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
