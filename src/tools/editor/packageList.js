/**
 * Tool: `package_list` — list installed UPM packages. Forwards to the Unity C#
 * bridge, which runs an async Client.List request.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const packageList = {
  name: "package_list",
  description:
    "List installed Unity Package Manager packages. Returns an array of { name, version, source }.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("package_list", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
