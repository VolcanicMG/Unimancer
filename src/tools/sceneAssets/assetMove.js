/**
 * Tool: `asset_move` — move (or rename) an asset, validating the move first.
 * Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const assetMove = {
  name: "asset_move",
  description:
    "Move or rename an asset via AssetDatabase.ValidateMoveAsset + MoveAsset. Returns the validation/move error string (empty on success).",
  inputSchema: {
    from: z.string().describe("Source asset path."),
    to: z.string().describe("Destination asset path."),
  },
  /**
   * @param {object} args - { from, to }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("asset_move", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
