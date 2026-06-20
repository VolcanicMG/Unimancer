/**
 * Tool: `ui_create` — create a uGUI element from an archetype as a proper UI
 * GameObject (RectTransform + CanvasRenderer where the archetype needs it).
 * Auto-creates a Canvas + EventSystem when an interactive element is created
 * with no Canvas ancestor. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** A {x,y} vector raw shape (both components required when present). */
const vec2 = z.object({ x: z.number(), y: z.number() });

/** @type {import("../../core/types.js").ToolDefinition} */
export const uiCreate = {
  name: "ui_create",
  description:
    "Create a uGUI element from an archetype as a proper UI GameObject (RectTransform + CanvasRenderer where needed). " +
    "Archetypes: canvas, panel, image, text (TextMeshProUGUI if TMP is present, else UI.Text), button, rawimage, scrollview, slider, empty-rect. " +
    "If an interactive archetype (button/slider/scrollview) is created with no Canvas ancestor, a Canvas + EventSystem are auto-created. " +
    "Optionally parent under parentPath (hierarchy path/instanceID) and set initial anchoredPosition/sizeDelta. " +
    "Returns {instanceID, path}. The action is undoable.",
  inputSchema: {
    archetype: z
      .enum([
        "canvas",
        "panel",
        "image",
        "text",
        "button",
        "rawimage",
        "scrollview",
        "slider",
        "empty-rect",
      ])
      .describe("UI archetype to instantiate."),
    name: z.string().optional().describe("Name for the new UI GameObject (defaults to the archetype)."),
    parentPath: z
      .string()
      .optional()
      .describe("Hierarchy path or instanceID of an existing parent (ideally a Canvas or another RectTransform)."),
    anchoredPosition: vec2.optional().describe("Initial RectTransform anchoredPosition {x,y}."),
    sizeDelta: vec2.optional().describe("Initial RectTransform sizeDelta {x,y} (width/height when anchors are not stretched)."),
  },
  /**
   * @param {object} args - creation options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("ui_create", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
