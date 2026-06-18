/**
 * Tool: `console_clear` — clear the Editor console. Forwards to the Unity C#
 * bridge, which calls internal LogEntries.Clear via reflection.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const consoleClear = {
  name: "console_clear",
  description: "Clear all entries from the Unity Editor console. Returns { cleared: true }.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("console_clear", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
