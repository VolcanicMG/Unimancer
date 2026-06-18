/**
 * Tool: `adb_uninstall` — remove an installed app via `adb uninstall`.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbUninstall = {
  name: "adb_uninstall",
  description:
    "Uninstall an app from an Android device by its package name (e.g. com.example.game). Removes the app and its data.",
  inputSchema: {
    packageName: z.string().describe("Android package name to uninstall."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{packageName: string, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const { stdout, stderr, code } = await adb(["uninstall", args.packageName], args.serial);
    if (code !== 0) return err(`adb uninstall failed (code ${code}): ${stderr || stdout}`);
    return ok(`Uninstalled ${args.packageName}.\n${stdout.trim()}`);
  },
};
