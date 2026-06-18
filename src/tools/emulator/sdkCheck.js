/**
 * Tool: `android_sdk_check` — diagnose the local Android development
 * environment: SDK location env vars and whether the key CLI tools (adb,
 * emulator, sdkmanager, avdmanager) resolve and run. Returns an LLM-actionable
 * summary so an agent can decide what to install or which env var to set.
 */
import { execFile } from "node:child_process";
import { ok } from "../../core/types.js";

/**
 * Run a binary once and report whether it resolved (i.e. was found and ran).
 * Never throws; ENOENT (binary not found) is reported as missing.
 * @param {string} bin - executable name or path.
 * @param {string[]} args - probe arguments (a version/help flag).
 * @param {number} [timeoutMs] - kill after this many ms.
 * @returns {Promise<{found: boolean, output: string}>} found=true if the binary
 *   exists and executed (any exit code); output is a trimmed first line of
 *   stdout/stderr, or the failure reason.
 */
function probe(bin, args, timeoutMs = 15_000) {
  return new Promise((resolve) => {
    execFile(bin, args, { timeout: timeoutMs, maxBuffer: 4 * 1024 * 1024 }, (error, stdout, stderr) => {
      const out = `${stdout || ""}${stderr || ""}`.toString();
      const firstLine = out.split(/\r?\n/).map((l) => l.trim()).find(Boolean) || "";
      if (error && error.code === "ENOENT") {
        resolve({ found: false, output: "not found on PATH" });
        return;
      }
      // Any other error (non-zero exit, timeout) still means the binary was
      // located and executed, so we count it as found.
      resolve({ found: true, output: firstLine || (error ? String(error.message || error) : "(no output)") });
    });
  });
}

/** Resolve a configurable binary path, mirroring core/adb.js overrides. */
const ADB_BIN = process.env.ADB_PATH || "adb";
/** Resolve a configurable binary path, mirroring core/adb.js overrides. */
const EMULATOR_BIN = process.env.EMULATOR_PATH || "emulator";

/** @type {import("../../core/types.js").ToolDefinition} */
export const androidSdkCheck = {
  name: "android_sdk_check",
  description:
    "Diagnose the local Android SDK / development environment: reports ANDROID_HOME and ANDROID_SDK_ROOT, and whether adb, emulator, sdkmanager, and avdmanager resolve and run (with version where cheap). Call this when Android tools fail unexpectedly to figure out what is missing.",
  inputSchema: {},
  /**
   * @param {{}} _args - no parameters.
   * @param {import("../../core/types.js").ToolContext} _ctx - unused.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(_args, _ctx) {
    const androidHome = process.env.ANDROID_HOME || "(unset)";
    const androidSdkRoot = process.env.ANDROID_SDK_ROOT || "(unset)";

    const [adbProbe, emuProbe, sdkmanProbe, avdmanProbe] = await Promise.all([
      probe(ADB_BIN, ["version"]),
      probe(EMULATOR_BIN, ["-version"]),
      probe("sdkmanager", ["--version"]),
      probe("avdmanager", ["list", "target"]),
    ]);

    /**
     * Format a single probe row.
     * @param {string} label - tool label.
     * @param {{found: boolean, output: string}} p - probe result.
     * @returns {string}
     */
    const row = (label, p) => `  ${label.padEnd(11)} ${p.found ? "FOUND" : "MISSING"} — ${p.output}`;

    const lines = [
      "Android SDK environment check:",
      "",
      "Environment variables:",
      `  ANDROID_HOME      = ${androidHome}`,
      `  ANDROID_SDK_ROOT  = ${androidSdkRoot}`,
      "",
      "CLI tools:",
      row("adb", adbProbe),
      row("emulator", emuProbe),
      row("sdkmanager", sdkmanProbe),
      row("avdmanager", avdmanProbe),
    ];

    const anyMissing = [adbProbe, emuProbe, sdkmanProbe, avdmanProbe].some((p) => !p.found);
    if (anyMissing) {
      lines.push(
        "",
        "Hints: install the Android SDK (Android Studio or command-line tools), then either add",
        "platform-tools, emulator, and cmdline-tools/latest/bin to PATH, or set ANDROID_HOME /",
        "ANDROID_SDK_ROOT (and ADB_PATH / EMULATOR_PATH to override individual binary locations).",
      );
    }
    return ok(lines.join("\n"));
  },
};
