/**
 * Tool: `asset_find` — search the project for assets using AssetDatabase filter
 * syntax. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const assetFind = {
  name: "asset_find",
  description:
    "Find assets via AssetDatabase.FindAssets using filter syntax (e.g. 't:Material wood'). Searches the given folders (default ['Assets']). Returns up to 200 paths plus the total count.",
  inputSchema: {
    filter: z.string().describe("AssetDatabase filter, e.g. 't:Material wood' or 'l:label'."),
    folders: z
      .array(z.string())
      .optional()
      .describe("Folders to search; defaults to ['Assets']."),
  },
  /**
   * @param {object} args - { filter, folders? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("asset_find", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
