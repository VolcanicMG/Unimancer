/**
 * Tool: `emulator_list_avds` — list the names of all defined Android Virtual
 * Devices (AVDs) known to the emulator CLI. Backed by `emulator -list-avds`.
 */
import { ok, err } from "../../core/types.js";
import { emulator } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const emulatorListAvds = {
  name: "emulator_list_avds",
  description:
    "List the names of all defined Android Virtual Devices (AVDs) available to the emulator. These are the names you pass to emulator_start. Lists *defined* AVDs, not running ones (use avd_list_running for that).",
  inputSchema: {},
  /**
   * @param {{}} _args - no parameters.
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(_args, _ctx) {
    const { stdout, stderr, code } = await emulator(["-list-avds"]);
    if (code !== 0) {
      return err(
        `emulator -list-avds failed (code ${code}): ${stderr || stdout || "emulator not found; set EMULATOR_PATH or add the Android SDK emulator dir to PATH"}`,
      );
    }
    const names = stdout
      .split(/\r?\n/)
      .map((l) => l.trim())
      .filter(Boolean);
    if (names.length === 0) return ok("No AVDs defined. Create one with avdmanager or Android Studio.");
    return ok(`Defined AVDs (${names.length}):\n${names.map((n) => `- ${n}`).join("\n")}`);
  },
};
