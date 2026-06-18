/**
 * Tool: `package_add` — add/install a UPM package. Forwards to the Unity C#
 * bridge, which runs an async Client.Add request.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const packageAdd = {
  name: "package_add",
  description:
    "Add a Unity package via the Package Manager. identifier can be a package name, name@version, or a git URL. Returns the added package or an error.",
  inputSchema: {
    identifier: z
      .string()
      .describe("Package name, name@version, or git URL to install."),
  },
  /**
   * @param {object} args - { identifier }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("package_add", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
