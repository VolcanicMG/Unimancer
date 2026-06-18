/**
 * Tool: `lighting_clear` — clear baked lightmap data (and the GI disk cache)
 * via the Unity C# bridge, which calls Lightmapping.Clear() and
 * Lightmapping.ClearDiskCache().
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const lightingClear = {
  name: "lighting_clear",
  description:
    "Clear baked lightmap data and the GI disk cache via Lightmapping.Clear() / Lightmapping.ClearDiskCache(). Synchronous. Returns { cleared: true }. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("lighting_clear", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
