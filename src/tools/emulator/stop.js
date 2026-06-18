/**
 * Tool: `emulator_stop` — shut down a running emulator (or all of them) via the
 * `adb emu kill` console command.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/**
 * Extract emulator serials (emulator-XXXX) from `adb devices` output.
 * @param {string} stdout - raw stdout from `adb devices`.
 * @returns {string[]} list of emulator serials.
 */
function parseEmulatorSerials(stdout) {
  return stdout
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l && !/^List of devices attached/i.test(l))
    .map((l) => l.split(/\s+/)[0])
    .filter((s) => s.startsWith("emulator-"));
}

/** @type {import("../../core/types.js").ToolDefinition} */
export const emulatorStop = {
  name: "emulator_stop",
  description:
    "Shut down a running emulator via 'adb emu kill'. Pass a serial (e.g. emulator-5554) to stop one instance, or omit it to stop every running emulator-XXXX instance.",
  inputSchema: {
    serial: z.string().optional().describe("Emulator serial to stop (e.g. emulator-5554); omit to stop all."),
  },
  /**
   * @param {{serial?: string}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    if (args.serial) {
      const { stdout, stderr, code } = await adb(["emu", "kill"], args.serial);
      if (code !== 0) return err(`adb emu kill ${args.serial} failed (code ${code}): ${stderr || stdout}`);
      return ok(`Stopped emulator ${args.serial}.`);
    }

    const listed = await adb(["devices"]);
    if (listed.code !== 0) {
      return err(
        `adb devices failed (code ${listed.code}): ${listed.stderr || listed.stdout || "adb not found; set ADB_PATH or add platform-tools to PATH"}`,
      );
    }
    const serials = parseEmulatorSerials(listed.stdout);
    if (serials.length === 0) return ok("No running emulators to stop.");

    const results = [];
    for (const serial of serials) {
      const { stderr, stdout, code } = await adb(["emu", "kill"], serial);
      results.push(code === 0 ? `stopped ${serial}` : `failed ${serial}: ${stderr || stdout}`);
    }
    return ok(`Attempted to stop ${serials.length} emulator(s):\n${results.map((r) => `- ${r}`).join("\n")}`);
  },
};
