/**
 * Tool: `script_edit` — edit an existing file under Assets/, either by full
 * content replacement or by an ordered list of find/replace edits. Forwards to
 * the Unity C# bridge, which writes and reimports the asset.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptEdit = {
  name: "script_edit",
  description:
    "Edit a file under Assets/. Provide exactly one of: content (full replacement) OR replacements (an ordered array of { find, replace } applied in sequence). Reimports the asset afterward. Returns { updated, bytes }.",
  inputSchema: {
    path: z.string().describe('Asset path; must start with "Assets/".'),
    content: z.string().optional().describe("Full replacement source. Mutually exclusive with replacements."),
    replacements: z
      .array(z.object({ find: z.string(), replace: z.string() }))
      .optional()
      .describe("Ordered find/replace edits. Mutually exclusive with content."),
  },
  /**
   * @param {object} args - { path, content? | replacements? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("script_edit", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
