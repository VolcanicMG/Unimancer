/**
 * Tool: `prefab_create` — save a scene GameObject as a prefab asset, optionally
 * connecting the source object to the new prefab. Forwards to the Unity C#
 * bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const prefabCreate = {
  name: "prefab_create",
  description:
    "Save a scene GameObject as a prefab via PrefabUtility.SaveAsPrefabAsset (connect=false) or SaveAsPrefabAssetAndConnect (connect=true, default). Returns the prefab path and success flag.",
  inputSchema: {
    gameObjectPath: z.string().describe("Hierarchy path of the source scene GameObject, e.g. 'Parent/Child'."),
    prefabPath: z.string().describe("Destination prefab asset path, e.g. 'Assets/Prefabs/Foo.prefab'."),
    connect: z
      .boolean()
      .optional()
      .describe("Connect the source object to the new prefab; defaults to true."),
  },
  /**
   * @param {object} args - { gameObjectPath, prefabPath, connect? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("prefab_create", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
