/**
 * Tool: `lighting_bake` — bake the scene lightmaps. Forwards to the Unity C#
 * bridge, which starts Lightmapping.BakeAsync() and polls Lightmapping.isRunning
 * across many Editor frames until the bake finishes. This is long-running; the
 * connection request timeout applies.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const lightingBake = {
  name: "lighting_bake",
  description:
    "Bake the scene lightmaps via Lightmapping.BakeAsync(), waiting for the multi-frame bake to complete. Returns { baked: true } on success, or { baked: false, error } if the bake could not start. Requires the Unity Editor open with the Unimancer package; bakes may take minutes.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("lighting_bake", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
