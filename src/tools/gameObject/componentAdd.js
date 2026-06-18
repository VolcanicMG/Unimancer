/**
 * Tool: `component_add` — add a component to a GameObject by type name
 * (undoable). Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const componentAdd = {
  name: "component_add",
  description:
    "Add a component to a GameObject. 'target' is a hierarchy path or instanceID. 'componentType' is a type name, either short (e.g. 'Rigidbody') or fully qualified (e.g. 'UnityEngine.BoxCollider'); it is resolved across loaded assemblies. The change is undoable. Returns {added:<resolvedTypeName>}.",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject."),
    componentType: z.string().describe("Component type name, short or fully qualified."),
  },
  /**
   * @param {object} args - add-component options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("component_add", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
