/**
 * Tool: `asset_refresh` — reimport a single asset or refresh the whole asset
 * database. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const assetRefresh = {
  name: "asset_refresh",
  description:
    "Refresh the asset database. If path is given, reimport just that asset (AssetDatabase.ImportAsset); otherwise run a full AssetDatabase.Refresh(). Returns { refreshed: true }.",
  inputSchema: {
    path: z.string().optional().describe("Asset path to reimport; omit for a full refresh."),
  },
  /**
   * @param {object} args - { path? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("asset_refresh", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
