/**
 * Tool: `script_apply_edits` — apply precise, non-clobbering edits to a C# file
 * under Assets/. Supports two edit shapes:
 *   - line-range: { startLine, endLine, replacement } (1-based, inclusive).
 *   - anchored find/replace: { find, replace, all? } (literal string match).
 * If `expectedSha` is supplied and the file's current SHA-256 differs, the edit
 * is rejected and nothing is written (guards against clobbering concurrent
 * changes). The Unity bridge applies line-range edits descending by startLine so
 * earlier edits don't shift later line numbers, then applies find/replace edits.
 * Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/**
 * A single line-range edit: replaces lines [startLine, endLine] (1-based,
 * inclusive) with `replacement` (may be multi-line, or empty to delete).
 */
const lineRangeEdit = z.object({
  startLine: z.number().int().min(1).describe("First line to replace (1-based, inclusive)."),
  endLine: z.number().int().min(1).describe("Last line to replace (1-based, inclusive)."),
  replacement: z.string().describe("Replacement text; may be multi-line, or empty to delete the range."),
});

/**
 * A single anchored find/replace edit: literal (non-regex) string match.
 */
const findReplaceEdit = z.object({
  find: z.string().min(1).describe("Literal substring to find (not a regex)."),
  replace: z.string().describe("Replacement text."),
  all: z.boolean().optional().describe("Replace all occurrences; defaults to first only."),
});

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptApplyEdits = {
  name: "script_apply_edits",
  description:
    "Apply precise, non-clobbering edits to a .cs file under Assets/. Each edit is either a line-range {startLine,endLine,replacement} (1-based inclusive) or an anchored {find,replace,all?} literal substitution. Line-range edits are applied descending so line numbers stay stable; find/replace edits run after. Pass expectedSha (from script_get_sha) to abort if the file changed. Overlapping line ranges are rejected. Reimports the asset on success.",
  inputSchema: {
    path: z.string().describe("Project-relative path to a .cs file under Assets/."),
    expectedSha: z
      .string()
      .optional()
      .describe("If set, the edit aborts unless the file's current SHA-256 (hex) matches this."),
    edits: z
      .array(z.union([lineRangeEdit, findReplaceEdit]))
      .min(1)
      .describe("One or more edits; each is a line-range or a find/replace edit."),
  },
  /**
   * @param {object} args - { path, expectedSha?, edits }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("script_apply_edits", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
