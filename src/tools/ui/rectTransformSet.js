/**
 * Tool: `rect_transform_set` — set RectTransform layout in one call: any of
 * anchorMin/anchorMax/pivot/anchoredPosition/sizeDelta/offsetMin/offsetMax, plus
 * a convenience `preset` enum for common anchor layouts. Forwards to the Unity
 * C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** A {x,y} vector raw shape (both components required when present). */
const vec2 = z.object({ x: z.number(), y: z.number() });

/** @type {import("../../core/types.js").ToolDefinition} */
export const rectTransformSet = {
  name: "rect_transform_set",
  description:
    "Set RectTransform layout on a UI GameObject in one call. Provide a `preset` for a common anchor layout " +
    "(stretch-all, top-bar, bottom-bar, left, right, center, top-left, top-right, bottom-left, bottom-right) and/or any explicit fields: " +
    "anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, offsetMin, offsetMax. A preset is applied first, then explicit fields override it. " +
    "Target by hierarchy path or instanceID. The change is undoable. Returns the resolved RectTransform layout.",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject (must have a RectTransform)."),
    preset: z
      .enum([
        "stretch-all",
        "top-bar",
        "bottom-bar",
        "left",
        "right",
        "center",
        "top-left",
        "top-right",
        "bottom-left",
        "bottom-right",
      ])
      .optional()
      .describe("Convenience anchor preset applied before any explicit fields."),
    anchorMin: vec2.optional().describe("Anchor min {x,y} in 0..1."),
    anchorMax: vec2.optional().describe("Anchor max {x,y} in 0..1."),
    pivot: vec2.optional().describe("Pivot {x,y} in 0..1."),
    anchoredPosition: vec2.optional().describe("Anchored position {x,y}."),
    sizeDelta: vec2.optional().describe("Size delta {x,y}."),
    offsetMin: vec2.optional().describe("Offset min {x,y} (left/bottom)."),
    offsetMax: vec2.optional().describe("Offset max {x,y} (right/top)."),
  },
  /**
   * @param {object} args - RectTransform layout options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("rect_transform_set", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
