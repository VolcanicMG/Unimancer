/**
 * Tool: `prefab_instantiate` — instantiate a prefab into the active scene,
 * optionally parenting and positioning it. The new object is registered for
 * Undo. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const prefabInstantiate = {
  name: "prefab_instantiate",
  description:
    "Instantiate a prefab via PrefabUtility.InstantiatePrefab into the active scene. Optionally set a parent (parentPath) and local position. Returns { instanceID, path }.",
  inputSchema: {
    prefabPath: z.string().describe("Prefab asset path to instantiate."),
    parentPath: z
      .string()
      .optional()
      .describe("Hierarchy path of the parent GameObject; omit for a root object."),
    position: z
      .object({ x: z.number(), y: z.number(), z: z.number() })
      .optional()
      .describe("Local position to set on the new instance."),
  },
  /**
   * @param {object} args - { prefabPath, parentPath?, position? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("prefab_instantiate", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
