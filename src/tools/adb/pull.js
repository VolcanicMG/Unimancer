/**
 * Tool: `adb_pull` — copy a file from the device to local via `adb pull`.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbPull = {
  name: "adb_pull",
  description:
    "Copy a file from a path on the device to the local filesystem (e.g. pull a log or save file off /sdcard/...).",
  inputSchema: {
    remotePath: z.string().describe("Source path on the device."),
    localPath: z.string().describe("Destination path on the local filesystem."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{remotePath: string, localPath: string, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const { stdout, stderr, code } = await adb(["pull", args.remotePath, args.localPath], args.serial);
    if (code !== 0) return err(`adb pull failed (code ${code}): ${stderr || stdout}`);
    return ok(`Pulled ${args.remotePath} -> ${args.localPath}.\n${stdout.trim()}`);
  },
};
