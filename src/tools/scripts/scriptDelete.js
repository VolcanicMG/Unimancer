/**
 * Tool: `script_delete` — delete a file under Assets/ via AssetDatabase.
 * Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const scriptDelete = {
  name: "script_delete",
  description:
    "Delete an asset under Assets/ via AssetDatabase.DeleteAsset. Returns { deleted } (boolean).",
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
      return ok(JSON.stringify(await ctx.unity.request("script_delete", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
