/**
 * Tool: `animator_get` — introspect the Animator on a GameObject. Forwards to
 * the Unity C# bridge, which reads the runtime Animator and (when the controller
 * is an AnimatorController asset) its parameters and per-layer state names.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const animatorGet = {
  name: "animator_get",
  description:
    "Read the Animator on a GameObject (resolved by hierarchy path or instanceID). Returns { hasAnimator, controller, parameters:[{name,type,defaultValue}], states:[...] }. If there is no Animator, returns { hasAnimator:false }. State/parameter introspection requires the controller to be an AnimatorController asset.",
  inputSchema: {
    target: z.string().describe("GameObject hierarchy path or instanceID."),
  },
  /**
   * @param {object} args - { target }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("animator_get", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
