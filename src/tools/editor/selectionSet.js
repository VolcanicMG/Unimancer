/**
 * Tool: `selection_set` — set the Editor selection. Forwards to the Unity C#
 * bridge, which resolves targets to objects and sets Selection.objects.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const selectionSet = {
  name: "selection_set",
  description:
    "Set the Unity Editor selection. targets is an array of asset paths or instance IDs. Returns the count of objects selected.",
  inputSchema: {
    targets: z
      .array(z.union([z.string(), z.number()]))
      .describe("Asset paths or instance IDs to select."),
  },
  /**
   * @param {object} args - { targets }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("selection_set", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
