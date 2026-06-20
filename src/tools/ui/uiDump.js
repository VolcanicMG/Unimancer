/**
 * Tool: `ui_dump` — EDIT-MODE walk of a Canvas (or every scene Canvas) returning
 * a tree of each node's name, path, components and full RectTransform rect. This
 * is the edit-mode analog of the play-mode `runtime_ui_list`. Forwards to the
 * Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const uiDump = {
  name: "ui_dump",
  description:
    "EDIT-MODE walk of a Canvas (or all scene Canvases) returning a tree. Each node has: name, path, components[], and a full rect " +
    "{anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, rect:{x,y,width,height}}. " +
    "Pass `target` (a Canvas hierarchy path/instanceID) to dump one canvas; omit it to dump every Canvas in every loaded scene. " +
    "This is the edit-mode analog of runtime_ui_list. Returns { canvases:[{ path, tree }] }.",
  inputSchema: {
    target: z
      .string()
      .optional()
      .describe("Hierarchy path or instanceID of a Canvas to dump; omit to dump all scene Canvases."),
    includeInactive: z
      .boolean()
      .optional()
      .describe("Include inactive GameObjects in the walk (default true)."),
  },
  /**
   * @param {object} args - dump options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("ui_dump", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
