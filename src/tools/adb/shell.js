/**
 * Tool: `adb_shell` — run an arbitrary device shell command.
 *
 * The command is supplied as a token ARRAY (argv), not a string, and is passed
 * verbatim to `adb shell` — no local shell parsing happens on our side. This is
 * a powerful escape hatch: it can run any command available on the device.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbShell = {
  name: "adb_shell",
  description:
    "Run an ARBITRARY shell command on the device. Provide the command as an array of argv tokens, e.g. [\"pm\",\"list\",\"packages\"] or [\"getprop\",\"ro.build.version.sdk\"]. Powerful and unrestricted: it can execute any on-device command, so prefer a more specific adb tool when one exists.",
  inputSchema: {
    command: z
      .array(z.string())
      .describe('Device command as argv tokens, e.g. ["dumpsys","battery"].'),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{command: string[], serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const { stdout, stderr, code } = await adb(["shell", ...args.command], args.serial);
    if (code !== 0) return err(`adb shell failed (code ${code}): ${stderr || stdout}`);
    return ok(stdout.trim() || "(no output)");
  },
};
