/**
 * Tool: `adb_stop_app` — force-stop a running app via `am force-stop`.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbStopApp = {
  name: "adb_stop_app",
  description:
    "Force-stop an app on the device by package name, killing all of its processes. Useful to reset an app to a cold start before relaunching.",
  inputSchema: {
    packageName: z.string().describe("Android package name to force-stop."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{packageName: string, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const { stdout, stderr, code } = await adb(
      ["shell", "am", "force-stop", args.packageName],
      args.serial
    );
    if (code !== 0) return err(`adb force-stop failed (code ${code}): ${stderr || stdout}`);
    return ok(`Force-stopped ${args.packageName}.`);
  },
};
