/**
 * Tool: `script_validate` — SYNTAX-level validation of C# source. Provide either
 * `path` (a .cs file under Assets/ to read) or inline `content`. The Unity bridge
 * prefers Roslyn (CSharpSyntaxTree.ParseText) and returns its syntax diagnostics;
 * if Roslyn isn't available it falls back to a basic structural balance check
 * (braces/parens/brackets + unterminated strings) and sets a `note`.
 *
 * NOTE: this is syntax-level only — it does NOT do semantic/type checking
 * (unknown types, missing references, overload resolution, etc. are not caught).
 * Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptValidate = {
  name: "script_validate",
  description:
    "Syntax-validate C# source and return diagnostics. Provide exactly one of `path` (a .cs file under Assets/) or `content` (inline source). Returns { ok, errorCount, diagnostics:[{severity,line,column,message}] }. Syntax-level only (no semantic/type checking). Falls back to a reduced brace/quote balance check if Roslyn is unavailable (indicated by a `note`).",
  inputSchema: {
    path: z
      .string()
      .optional()
      .describe("Project-relative path to a .cs file under Assets/ to read and validate."),
    content: z.string().optional().describe("Inline C# source to validate instead of a file."),
  },
  /**
   * @param {object} args - { path? } or { content? } (exactly one).
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("script_validate", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
