/**
 * Tool: `runtime_set_component_property` — set a public field/property on a
 * component of a running GameObject. Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeSetComponentProperty = {
  name: "runtime_set_component_property",
  description:
    "Set a public field or property on a component of a running GameObject. value is coerced to the member type (int/float/bool/string/enum/Vector3 via {x,y,z}). Returns { set, member, type }. Mutates live game state.",
  inputSchema: {
    target: z.string().describe("GameObject name or hierarchy path."),
    componentType: z.string().describe("Component type name (simple or fully-qualified)."),
    member: z.string().describe("Public field or property name to set."),
    value: z.any().describe("New value; coerced to the member's type."),
  },
  /**
   * @param {object} args - { target, componentType, member, value }.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_set_component_property", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
