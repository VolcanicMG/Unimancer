/**
 * Tool: `adb_push` — copy a local file to the device via `adb push`.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbPush = {
  name: "adb_push",
  description:
    "Copy a file from the local filesystem to a path on the device (e.g. push a config or asset to /sdcard/...).",
  inputSchema: {
    localPath: z.string().describe("Source path on the local filesystem."),
    remotePath: z.string().describe("Destination path on the device."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{localPath: string, remotePath: string, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const { stdout, stderr, code } = await adb(["push", args.localPath, args.remotePath], args.serial);
    if (code !== 0) return err(`adb push failed (code ${code}): ${stderr || stdout}`);
    return ok(`Pushed ${args.localPath} -> ${args.remotePath}.\n${stdout.trim()}`);
  },
};
