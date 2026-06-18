/**
 * Tool: `console_read` — read recent Editor console entries. Forwards to the
 * Unity C# bridge, which uses internal LogEntries reflection.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const consoleRead = {
  name: "console_read",
  description:
    "Read recent Unity Editor console entries. count defaults to 100; types filters to a subset of ['error','warning','log']. Returns an array of { type, message }.",
  inputSchema: {
    count: z.number().int().optional().describe("Max entries to return; defaults to 100."),
    types: z
      .array(z.enum(["error", "warning", "log"]))
      .optional()
      .describe("Filter to these entry types; defaults to all."),
  },
  /**
   * @param {object} args - { count?, types? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("console_read", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
