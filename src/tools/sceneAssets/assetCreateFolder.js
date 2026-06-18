/**
 * Tool: `asset_create_folder` — create a new folder under an existing parent
 * folder in the project. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const assetCreateFolder = {
  name: "asset_create_folder",
  description:
    "Create a new folder via AssetDatabase.CreateFolder. parent must be an existing folder (e.g. 'Assets/Art'). Returns the new folder path.",
  inputSchema: {
    parent: z.string().describe("Existing parent folder, e.g. 'Assets/Art'."),
    name: z.string().describe("Name of the new folder."),
  },
  /**
   * @param {object} args - { parent, name }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("asset_create_folder", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
