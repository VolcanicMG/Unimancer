/**
 * Tool: `emulator_start` — launch an Android emulator for a named AVD as a
 * detached background process. The emulator boots asynchronously, so this tool
 * returns immediately after spawning it; poll adb_devices to watch it come up.
 */
import { spawn } from "node:child_process";
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** Path to the `emulator` binary, mirroring core/adb.js resolution. */
const EMULATOR_BIN = process.env.EMULATOR_PATH || "emulator";

/** @type {import("../../core/types.js").ToolDefinition} */
export const emulatorStart = {
  name: "emulator_start",
  description:
    "Launch an Android emulator for a named AVD as a detached background process. Returns immediately — the emulator boots asynchronously (often 30-60s); poll adb_devices until the new emulator-XXXX serial reports 'device'. Use noWindow for headless/CI, wipeData for a clean factory-reset boot.",
  inputSchema: {
    avdName: z.string().describe("Name of the AVD to launch (see emulator_list_avds)."),
    noWindow: z.boolean().optional().describe("Run headless without a GUI window (-no-window)."),
    wipeData: z.boolean().optional().describe("Wipe user data for a clean boot (-wipe-data)."),
  },
  /**
   * @param {{avdName: string, noWindow?: boolean, wipeData?: boolean}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    const flags = ["-avd", args.avdName];
    if (args.noWindow) flags.push("-no-window");
    if (args.wipeData) flags.push("-wipe-data");
    try {
      // Detached + ignored stdio + unref() so the emulator keeps running after
      // this tool (and the MCP server) move on; the buffered exec helpers in
      // core/adb.js would block on the never-ending process, so we spawn here.
      const child = spawn(EMULATOR_BIN, flags, { detached: true, stdio: "ignore" });
      child.unref();
      return ok(
        `Launched emulator for AVD "${args.avdName}" (pid ${child.pid}) with flags: ${flags.join(" ")}.\n` +
          "It boots asynchronously — poll adb_devices until a new emulator-XXXX serial reports state 'device'.",
      );
    } catch (e) {
      return err(
        `Failed to spawn emulator: ${e && e.message ? e.message : String(e)}. ` +
          "emulator not found; set EMULATOR_PATH or add the Android SDK emulator dir to PATH.",
      );
    }
  },
};
