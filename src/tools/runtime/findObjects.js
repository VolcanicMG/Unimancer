/**
 * Tool: `runtime_find_objects` — locate GameObjects by name in the RUNNING game.
 * Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeFindObjects = {
  name: "runtime_find_objects",
  description:
    "Find GameObjects in the RUNNING game by name. Returns up to 200 {name, path, instanceID, active}; instanceID is a string (may exceed JS int range).",
  inputSchema: {
    query: z.string().describe("Name to search for (substring unless exact=true)."),
    exact: z.boolean().optional().describe("Require an exact name match; defaults to false (substring)."),
    includeInactive: z.boolean().optional().describe("Include inactive objects; defaults to true."),
  },
  /**
   * @param {object} args - { query, exact?, includeInactive? }.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_find_objects", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
