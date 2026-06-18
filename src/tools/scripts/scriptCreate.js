/**
 * Tool: `script_create` — create a new C# script asset under Assets/. Forwards to
 * the Unity C# bridge, which writes the file and imports it via AssetDatabase.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptCreate = {
  name: "script_create",
  description:
    "Create a new C# script at an Assets/-relative path ending in .cs, with the given content. Fails if the file already exists unless overwrite=true. Imports the asset afterward. Returns { created } with the path.",
  inputSchema: {
    path: z.string().describe('Asset path; must start with "Assets/" and end with ".cs".'),
    content: z.string().describe("Full C# source to write."),
    overwrite: z.boolean().optional().describe("Allow overwriting an existing file; defaults to false."),
  },
  /**
   * @param {object} args - { path, content, overwrite? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("script_create", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
