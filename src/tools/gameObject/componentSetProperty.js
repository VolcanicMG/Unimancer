/**
 * Tool: `component_set_property` — set a single serialized property on a
 * component (undoable). Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const componentSetProperty = {
  name: "component_set_property",
  description:
    "Set one serialized property on a component. 'target' is a hierarchy path or instanceID; 'componentType' selects the component (short or fully qualified name); 'propertyPath' is the SerializedProperty path (e.g. 'm_Mass', 'm_IsKinematic'). 'value' is coerced to the property's type: number/boolean/string/enum index, {x,y,z[,w]} for vectors, {r,g,b,a} for color, or an asset path string for an ObjectReference. The change is undoable. Returns {set:propertyPath, type, value}.",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject."),
    componentType: z.string().describe("Component type name, short or fully qualified."),
    propertyPath: z.string().describe("SerializedProperty path to set."),
    value: z
      .any()
      .describe("New value; coerced by the property type (scalar, {x,y,z,w}, {r,g,b,a}, or asset path)."),
  },
  /**
   * @param {object} args - set-property options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("component_set_property", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
