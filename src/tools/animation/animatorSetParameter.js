/**
 * Tool: `animator_set_parameter` — set an Animator parameter on a GameObject.
 * Forwards to the Unity C# bridge, which infers the parameter type from the
 * value (bool / int / float / trigger) and calls the matching Animator setter.
 * Most meaningful in Play mode, where the Animator is actively evaluating.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const animatorSetParameter = {
  name: "animator_set_parameter",
  description:
    "Set an Animator parameter by name. Type is inferred from value: boolean -> SetBool, integer number -> SetInteger, other number -> SetFloat, null or \"trigger\" -> SetTrigger. NOTE: most meaningful in Play mode, where the Animator is evaluating; in edit mode the value may not persist. Returns { set:true, name, type }.",
  inputSchema: {
    target: z.string().describe("GameObject hierarchy path or instanceID."),
    name: z.string().describe("Animator parameter name."),
    value: z
      .union([z.boolean(), z.number(), z.string(), z.null()])
      .optional()
      .describe("Value to set; boolean/number/\"trigger\"/null determines the parameter type."),
  },
  /**
   * @param {object} args - { target, name, value }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("animator_set_parameter", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
