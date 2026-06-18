/**
 * Tool: `adb_logcat` — capture a bounded, non-streaming dump of device logs.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbLogcat = {
  name: "adb_logcat",
  description:
    "Dump the most recent device log lines (non-streaming snapshot via `logcat -d -t N`). Use filterSpec to restrict tags, e.g. \"Unity:I *:S\" to show only Unity logs at Info+ and silence everything else.",
  inputSchema: {
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
    lines: z.number().optional().describe("How many trailing log lines to return (default 200)."),
    filterSpec: z
      .string()
      .optional()
      .describe('Logcat tag filter spec, e.g. "Unity:I *:S". Tokens are split on whitespace.'),
  },
  /**
   * @param {{serial?: string, lines?: number, filterSpec?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const lines = args.lines ?? 200;
    const cmd = ["logcat", "-d", "-t", String(lines)];
    if (args.filterSpec) cmd.push(...args.filterSpec.trim().split(/\s+/));
    const { stdout, stderr, code } = await adb(cmd, args.serial);
    if (code !== 0) return err(`adb logcat failed (code ${code}): ${stderr || stdout}`);
    return ok(stdout.trim() || "(no log output)");
  },
};
