/**
 * Tool: `animation_add_curve` — add (or replace) an animated curve on an
 * existing AnimationClip. Forwards to the Unity C# bridge, which builds an
 * AnimationCurve from the supplied keyframes and calls clip.SetCurve with the
 * resolved component Type, then saves the asset.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const animationAddCurve = {
  name: "animation_add_curve",
  description:
    "Add an animated curve to a clip. relativePath is the child path from the animated root (\"\" for the root), componentType is the animated component (e.g. \"Transform\"), property is the serialized property name (e.g. \"m_LocalPosition.x\"), and keys is an array of {time,value} keyframes. Replaces any existing curve for that binding. Returns { keys:<n> }.",
  inputSchema: {
    clipPath: z.string().describe("Asset path of the existing AnimationClip."),
    relativePath: z.string().describe("Child transform path from the animated root; \"\" for the root."),
    componentType: z.string().describe("Animated component type, e.g. Transform or SpriteRenderer."),
    property: z.string().describe("Serialized property name, e.g. m_LocalPosition.x."),
    keys: z
      .array(z.object({ time: z.number(), value: z.number() }))
      .describe("Keyframes as {time, value} pairs."),
  },
  /**
   * @param {object} args - curve options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("animation_add_curve", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
