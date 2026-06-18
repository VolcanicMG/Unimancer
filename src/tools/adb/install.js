/**
 * Tool: `adb_install` — install an APK onto a device via `adb install`.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbInstall = {
  name: "adb_install",
  description:
    "Install an APK file (by local filesystem path) onto an Android device. Use reinstall to keep app data when upgrading an already-installed app, and grantPermissions to auto-grant all runtime permissions at install time.",
  inputSchema: {
    apkPath: z.string().describe("Local filesystem path to the .apk to install."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
    reinstall: z.boolean().optional().describe("Reinstall keeping data (adb -r)."),
    grantPermissions: z.boolean().optional().describe("Grant all runtime permissions (adb -g)."),
  },
  /**
   * @param {{apkPath: string, serial?: string, reinstall?: boolean, grantPermissions?: boolean}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const flags = [];
    if (args.reinstall) flags.push("-r");
    if (args.grantPermissions) flags.push("-g");
    const { stdout, stderr, code } = await adb(["install", ...flags, args.apkPath], args.serial);
    if (code !== 0) return err(`adb install failed (code ${code}): ${stderr || stdout}`);
    return ok(`Installed ${args.apkPath}.\n${stdout.trim()}`);
  },
};
