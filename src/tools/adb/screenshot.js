/**
 * Tool: `adb_screenshot` — capture the device screen to a local PNG.
 *
 * Captures device-side to a temp file (avoids exec-out binary-on-stdout
 * pitfalls), pulls it to the requested local path, then removes the temp file.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** Device-side scratch path for the captured screenshot. */
const REMOTE_TMP = "/sdcard/_unimancer_shot.png";

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbScreenshot = {
  name: "adb_screenshot",
  description:
    "Capture a PNG screenshot of the device's current screen and save it to a local filesystem path. Useful for visually verifying app/game state.",
  inputSchema: {
    outputPath: z.string().describe("Local filesystem path to write the .png screenshot to."),
    serial: z.string().optional().describe("Target device serial; omit if only one device."),
  },
  /**
   * @param {{outputPath: string, serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const cap = await adb(["shell", "screencap", "-p", REMOTE_TMP], args.serial);
    if (cap.code !== 0) return err(`screencap failed (code ${cap.code}): ${cap.stderr || cap.stdout}`);

    const pull = await adb(["pull", REMOTE_TMP, args.outputPath], args.serial);
    // Best-effort cleanup regardless of pull outcome.
    await adb(["shell", "rm", REMOTE_TMP], args.serial);

    if (pull.code !== 0) return err(`pull failed (code ${pull.code}): ${pull.stderr || pull.stdout}`);
    return ok(`Screenshot saved to ${args.outputPath}.`);
  },
};
