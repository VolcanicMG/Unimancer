/**
 * Tool: `selection_get` — read the current Editor selection. Forwards to the
 * Unity C# bridge, which reads Selection.objects.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const selectionGet = {
  name: "selection_get",
  description:
    "Get the current Unity Editor selection. Returns an array of { name, path, instanceID, type }.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("selection_get", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
