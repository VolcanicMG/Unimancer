/**
 * Tool: `adb_launch_app` — start an app's launcher activity via monkey.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbLaunchApp = {
  name: "adb_launch_app",
  description:
    "Launch an installed app on the device by package name, as if tapped from the home screen. Uses monkey to fire the app's LAUNCHER intent, so no activity name is required.",
  inputSchema: {
    packageName: z.string().describe("Android package name to launch."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{packageName: string, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const { stdout, stderr, code } = await adb(
      ["shell", "monkey", "-p", args.packageName, "-c", "android.intent.category.LAUNCHER", "1"],
      args.serial
    );
    if (code !== 0) return err(`adb launch failed (code ${code}): ${stderr || stdout}`);
    return ok(`Launched ${args.packageName}.\n${stdout.trim()}`);
  },
};
