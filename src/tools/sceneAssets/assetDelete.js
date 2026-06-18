/**
 * Tool: `asset_delete` — delete an asset from the project. Forwards to the Unity
 * C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const assetDelete = {
  name: "asset_delete",
  description: "Delete an asset via AssetDatabase.DeleteAsset. Returns { deleted: bool }.",
  inputSchema: {
    path: z.string().describe("Asset path to delete."),
  },
  /**
   * @param {object} args - { path }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("asset_delete", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
