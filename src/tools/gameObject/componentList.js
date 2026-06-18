/**
 * Tool: `component_list` — list a GameObject's components and their serialized
 * properties. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const componentList = {
  name: "component_list",
  description:
    "List all components on a GameObject. 'target' is a hierarchy path or instanceID. Each component is returned with its type name and an array of serialized properties ({propertyPath, value}) read via SerializedObject. Returns {path, components:[...]}.",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject."),
  },
  /**
   * @param {object} args - list options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("component_list", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
