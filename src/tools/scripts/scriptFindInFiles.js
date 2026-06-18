/**
 * Tool: `script_find_in_files` — search file contents under a project folder for
 * a literal or regex query. Forwards to the Unity C# bridge, which scans files
 * directly (no AssetDatabase needed).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptFindInFiles = {
  name: "script_find_in_files",
  description:
    "Recursively search file contents under a folder (default Assets) for a query. Set regex=true to treat the query as a .NET regex. Filter by extensions (default [\".cs\"]). Returns up to maxResults matches as { path, line, text }.",
  inputSchema: {
    query: z.string().describe("Text or regex pattern to search for."),
    regex: z.boolean().optional().describe("Treat query as a regex; defaults to false."),
    extensions: z
      .array(z.string())
      .optional()
      .describe('File extensions to include, e.g. [".cs"]; defaults to [".cs"].'),
    folder: z.string().optional().describe('Root folder to scan; defaults to "Assets".'),
    maxResults: z.number().int().optional().describe("Maximum matches to return; defaults to 100."),
  },
  /**
   * @param {object} args - { query, regex?, extensions?, folder?, maxResults? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("script_find_in_files", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
