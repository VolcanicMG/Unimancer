/**
 * Tool: `gameobject_set_active` — toggle a GameObject's active state (undoable).
 * Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const gameobjectSetActive = {
  name: "gameobject_set_active",
  description:
    "Set a GameObject's active (enabled) state via SetActive. 'target' is a hierarchy path or instanceID. The change is undoable. Returns {path, active}.",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject."),
    active: z.boolean().describe("Desired active state."),
  },
  /**
   * @param {object} args - set-active options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("gameobject_set_active", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
