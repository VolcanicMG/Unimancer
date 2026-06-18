/**
 * Tool: `run_tests` — run EditMode or PlayMode tests. Forwards to the Unity C#
 * bridge, which drives the TestRunnerApi and resolves on RunFinished.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runTests = {
  name: "run_tests",
  description:
    "Run Unity tests. mode='EditMode' or 'PlayMode'; optional filter matches a test name or category. Requires the com.unity.test-framework package. Returns { passed, failed, skipped, durationSeconds, failures }.",
  inputSchema: {
    mode: z.enum(["EditMode", "PlayMode"]).describe("Test mode to run."),
    filter: z
      .string()
      .optional()
      .describe("Optional test name or category filter."),
  },
  /**
   * @param {object} args - { mode, filter? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("run_tests", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
