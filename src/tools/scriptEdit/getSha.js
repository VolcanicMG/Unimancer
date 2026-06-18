/**
 * Tool: `script_get_sha` — return the SHA-256 (hex) and line count of a C# file
 * under Assets/. Lets an editing client capture a fingerprint of a file's exact
 * bytes, then pass it back to `script_apply_edits` as `expectedSha` to be sure
 * the file hasn't changed underneath it before writing. Forwards to the Unity
 * C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptGetSha = {
  name: "script_get_sha",
  description:
    "Return the SHA-256 (hex) of a C# file's UTF-8 bytes plus its line count. Use the sha as `expectedSha` for script_apply_edits to detect concurrent changes before writing. path must be a .cs file under Assets/.",
  inputSchema: {
    path: z.string().describe("Project-relative path to a .cs file under Assets/."),
  },
  /**
   * @param {object} args - { path }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("script_get_sha", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
