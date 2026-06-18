/**
 * Tool: `gameobject_find` — search the active scene for GameObjects by name,
 * optionally filtered by tag and active state. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const gameobjectFind = {
  name: "gameobject_find",
  description:
    "Find GameObjects in the active scene by name. 'exact' requires a full-name match (default false = substring, case-insensitive). Optional 'tag' filters by Unity tag. 'includeInactive' includes disabled objects (default false). Returns an array of {name, path, instanceID, active}.",
  inputSchema: {
    query: z.string().describe("Name or substring to match."),
    exact: z.boolean().optional().describe("Require an exact full-name match; default false."),
    tag: z.string().optional().describe("Only return objects with this Unity tag."),
    includeInactive: z.boolean().optional().describe("Include inactive objects; default false."),
  },
  /**
   * @param {object} args - search options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("gameobject_find", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
