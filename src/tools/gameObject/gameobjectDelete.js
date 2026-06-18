/**
 * Tool: `gameobject_delete` — delete a GameObject from the scene (undoable).
 * Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const gameobjectDelete = {
  name: "gameobject_delete",
  description:
    "Delete a GameObject from the active scene. 'target' is either a hierarchy path (e.g. 'World/Player') or an integer instanceID. The deletion is undoable. Returns {deleted:true}.",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject to delete."),
  },
  /**
   * @param {object} args - delete options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("gameobject_delete", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
