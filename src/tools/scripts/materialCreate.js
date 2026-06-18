/**
 * Tool: `material_create` — create a new Material asset under Assets/ with a
 * given shader. Forwards to the Unity C# bridge, which creates and saves the
 * asset via AssetDatabase.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const materialCreate = {
  name: "material_create",
  description:
    "Create a new Material at an Assets/-relative path ending in .mat. shader defaults to 'Universal Render Pipeline/Lit'; if that shader is not found it falls back to 'Standard' (noted in the result). Returns { created, shader }.",
  inputSchema: {
    path: z.string().describe('Asset path; must start with "Assets/" and end with ".mat".'),
    shader: z
      .string()
      .optional()
      .describe("Shader name; defaults to 'Universal Render Pipeline/Lit'."),
  },
  /**
   * @param {object} args - { path, shader? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("material_create", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
