/**
 * Tool: `runtime_call_method` — THE "drive the game" tool. Reflectively invokes a
 * public instance method on a component of a running GameObject. Forwards to the
 * runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeCallMethod = {
  name: "runtime_call_method",
  description:
    "Drive the RUNNING game: invoke a public instance method on a component (e.g. Jump, TakeDamage, LoadLevel). args are coerced to the method's parameter types. Returns { called, result }. Powerful — can mutate live game state.",
  inputSchema: {
    target: z.string().describe("GameObject name or hierarchy path."),
    componentType: z.string().describe("Component type name (simple or fully-qualified)."),
    method: z.string().describe("Public instance method name to invoke."),
    args: z.array(z.any()).optional().describe("Method arguments, coerced to parameter types; defaults to none."),
  },
  /**
   * @param {object} args - { target, componentType, method, args? }.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_call_method", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
