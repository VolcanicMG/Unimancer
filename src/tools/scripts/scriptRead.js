/**
 * Tool: `script_read` — read the text content of a C# (or other) file under
 * Assets/. Forwards to the Unity C# bridge, which reads via File.ReadAllText.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptRead = {
  name: "script_read",
  description:
    "Read a file under Assets/ and return its text content. Returns { path, content }.",
  inputSchema: {
    path: z.string().describe('Asset path; must start with "Assets/".'),
  },
  /**
   * @param {object} args - { path }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("script_read", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
