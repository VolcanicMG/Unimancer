/**
 * Tool: `gameobject_rename` — rename a GameObject (undoable). Forwards to the
 * Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const gameobjectRename = {
  name: "gameobject_rename",
  description:
    "Rename a GameObject. 'target' is a hierarchy path or instanceID. The change is undoable. Returns {instanceID, path} (path reflects the new name).",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject."),
    newName: z.string().describe("New name for the GameObject."),
  },
  /**
   * @param {object} args - rename options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("gameobject_rename", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
