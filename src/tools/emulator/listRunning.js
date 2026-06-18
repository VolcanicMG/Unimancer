/**
 * Tool: `avd_list_running` — list currently running emulator instances (their
 * serials, state, and model) by filtering `adb devices -l` to emulator-XXXX
 * serials. Complements emulator_list_avds, which lists *defined* AVDs.
 */
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const avdListRunning = {
  name: "avd_list_running",
  description:
    "List the emulator instances currently running (serial, connection state, and model), filtered from adb to only emulator-XXXX serials. Use emulator_list_avds for the set of *defined* AVDs instead.",
  inputSchema: {},
  /**
   * @param {{}} _args - no parameters.
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(_args, _ctx) {
    const { stdout, stderr, code } = await adb(["devices", "-l"]);
    if (code !== 0) {
      return err(
        `adb devices failed (code ${code}): ${stderr || stdout || "adb not found; set ADB_PATH or add platform-tools to PATH"}`,
      );
    }
    const lines = stdout
      .split(/\r?\n/)
      .map((l) => l.trim())
      .filter((l) => l && !/^List of devices attached/i.test(l))
      .filter((l) => l.split(/\s+/)[0].startsWith("emulator-"));
    if (lines.length === 0) return ok("No running emulator instances.");
    const rows = lines.map((line) => {
      const [serial, state, ...rest] = line.split(/\s+/);
      const modelTok = rest.find((t) => t.startsWith("model:"));
      const model = modelTok ? modelTok.slice("model:".length) : "unknown";
      return `${serial}\t${state}\tmodel=${model}`;
    });
    return ok(`Running emulators (${rows.length}):\n${rows.join("\n")}`);
  },
};
