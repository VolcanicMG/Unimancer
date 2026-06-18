/**
 * Tool: `package_remove` — remove a UPM package. Forwards to the Unity C#
 * bridge, which runs an async Client.Remove request.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const packageRemove = {
  name: "package_remove",
  description:
    "Remove a Unity package by name via the Package Manager. Returns { removed } or an error.",
  inputSchema: {
    name: z.string().describe("Package name to remove."),
  },
  /**
   * @param {object} args - { name }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("package_remove", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
