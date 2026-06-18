/**
 * Tool: `adb_devices` — list attached Android devices/emulators with their
 * serial, state, and (when available) model. Backed by `adb devices -l`.
 */
import { ok, err } from "../../core/types.js";
import { adb } from "../../core/adb.js";

/**
 * Parse the `adb devices -l` output into a readable per-device summary.
 * The first line ("List of devices attached") and blanks are skipped.
 * @param {string} stdout - raw stdout from `adb devices -l`.
 * @returns {string} a newline-joined human summary, or a "no devices" notice.
 */
function summarizeDevices(stdout) {
  const lines = stdout
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l && !/^List of devices attached/i.test(l));
  if (lines.length === 0) return "No devices attached.";
  return lines
    .map((line) => {
      const [serial, state, ...rest] = line.split(/\s+/);
      // `model:...` appears among the trailing key:value descriptors with -l.
      const modelTok = rest.find((t) => t.startsWith("model:"));
      const model = modelTok ? modelTok.slice("model:".length) : "unknown";
      return `${serial}\t${state}\tmodel=${model}`;
    })
    .join("\n");
}

/** @type {import("../../core/types.js").ToolDefinition} */
export const adbDevices = {
  name: "adb_devices",
  description:
    "List all Android devices and emulators currently visible to adb, with each one's serial, connection state (device/offline/unauthorized), and model. Call this first to discover serials for other adb tools.",
  inputSchema: {},
  /**
   * @param {{}} _args - no parameters.
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(_args, _ctx) {
    const { stdout, stderr, code } = await adb(["devices", "-l"]);
    if (code !== 0) return err(`adb devices failed (code ${code}): ${stderr || stdout}`);
    return ok(summarizeDevices(stdout));
  },
};
