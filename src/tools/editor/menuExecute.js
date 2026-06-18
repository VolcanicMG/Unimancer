/**
 * Tool: `menu_execute` — invoke an Editor menu item by path. Forwards to the
 * Unity C# bridge, which calls EditorApplication.ExecuteMenuItem.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const menuExecute = {
  name: "menu_execute",
  description:
    "Execute a Unity Editor menu item by its menu path (e.g. 'Assets/Refresh'). Returns { executed }.",
  inputSchema: {
    menuPath: z.string().describe("Menu path, e.g. 'Assets/Refresh'."),
  },
  /**
   * @param {object} args - { menuPath }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("menu_execute", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
