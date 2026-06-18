/**
 * Tool: `adb_screenrecord` — record the device screen to a local MP4.
 *
 * Records device-side for a bounded duration, pulls the result locally, then
 * removes the temp file. The command timeout is set slightly longer than the
 * record duration so the device-side recorder finishes before we give up.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** Device-side scratch path for the recording. */
const REMOTE_TMP = "/sdcard/_unimancer_rec.mp4";
/** Hard cap on record duration (seconds); screenrecord itself caps at 180. */
const MAX_SECONDS = 180;

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbScreenrecord = {
  name: "adb_screenrecord",
  description:
    "Record a video (MP4) of the device screen for a fixed number of seconds and save it to a local path. Duration is capped at 180 seconds. Blocks until recording completes.",
  inputSchema: {
    outputPath: z.string().describe("Local filesystem path to write the .mp4 to."),
    seconds: z.number().optional().describe("Record duration in seconds (default 10, max 180)."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{outputPath: string, seconds?: number, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const seconds = Math.min(Math.max(args.seconds ?? 10, 1), MAX_SECONDS);
    // Allow a few extra seconds beyond the record window for flush/finalize.
    const timeoutMs = seconds * 1000 + 10_000;

    const rec = await adb(
      ["shell", "screenrecord", "--time-limit", String(seconds), REMOTE_TMP],
      args.serial,
      timeoutMs
    );
    if (rec.code !== 0) return err(`screenrecord failed (code ${rec.code}): ${rec.stderr || rec.stdout}`);

    const pull = await adb(["pull", REMOTE_TMP, args.outputPath], args.serial);
    await adb(["shell", "rm", REMOTE_TMP], args.serial);

    if (pull.code !== 0) return err(`pull failed (code ${pull.code}): ${pull.stderr || pull.stdout}`);
    return ok(`Recorded ${seconds}s to ${args.outputPath}.`);
  },
};
