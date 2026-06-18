/**
 * Tool: `adb_clear_data` — wipe an app's data/cache via `pm clear`.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbClearData = {
  name: "adb_clear_data",
  description:
    "Clear all stored data and cache for an app by package name, resetting it to a freshly-installed state without uninstalling. Destructive: removes saves, logins, and preferences.",
  inputSchema: {
    packageName: z.string().describe("Android package name whose data to clear."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{packageName: string, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const { stdout, stderr, code } = await adb(["shell", "pm", "clear", args.packageName], args.serial);
    if (code !== 0) return err(`adb pm clear failed (code ${code}): ${stderr || stdout}`);
    return ok(`Cleared data for ${args.packageName}.\n${stdout.trim()}`);
  },
};
