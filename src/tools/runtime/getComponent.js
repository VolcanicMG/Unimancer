/**
 * Tool: `runtime_get_component` — read a component's serialized fields on a
 * running GameObject. Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeGetComponent = {
  name: "runtime_get_component",
  description:
    "Read a component's serialized fields on a running GameObject (JsonUtility dump). Returns { type, json }. Uses the first matching component if several exist.",
  inputSchema: {
    target: z.string().describe("GameObject name or hierarchy path."),
    componentType: z.string().describe("Component type name (simple or fully-qualified)."),
  },
  /**
   * @param {object} args - { target, componentType }.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_get_component", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
