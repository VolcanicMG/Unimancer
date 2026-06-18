/**
 * Thin, safe wrapper around the Android Debug Bridge (`adb`) and the emulator
 * CLI. All invocations use `execFile` with an argument array (never a shell
 * string) so device serials, package names, and paths can't be interpreted as
 * shell syntax — important because these values often come from LLM-supplied
 * tool arguments.
 */
import { execFile } from "node:child_process";

/**
 * @typedef {Object} ExecResult
 * @property {string} stdout
 * @property {string} stderr
 * @property {number|null} code - exit code, or null if killed by a signal.
 */

/** Path to the `adb` binary. Override with the `ADB_PATH` env var. */
const ADB_BIN = process.env.ADB_PATH || "adb";
/** Path to the `emulator` binary. Override with the `EMULATOR_PATH` env var. */
const EMULATOR_BIN = process.env.EMULATOR_PATH || "emulator";
/** Default kill timeout (ms) for non-streaming commands. */
const DEFAULT_TIMEOUT_MS = 60_000;

/**
 * Run a binary with an argument array. Never throws on non-zero exit.
 * @param {string} bin - executable path.
 * @param {string[]} args - argument list (passed verbatim, no shell parsing).
 * @param {number} [timeoutMs] - kill after this many ms.
 * @returns {Promise<ExecResult>}
 */
function run(bin, args, timeoutMs = DEFAULT_TIMEOUT_MS) {
  return new Promise((resolve) => {
    execFile(bin, args, { timeout: timeoutMs, maxBuffer: 16 * 1024 * 1024 }, (error, stdout, stderr) => {
      const code = error && typeof error.code === "number" ? error.code : error ? 1 : 0;
      resolve({ stdout: stdout?.toString() ?? "", stderr: stderr?.toString() ?? "", code });
    });
  });
}

/**
 * Run an `adb` command, optionally targeting a device serial.
 * @param {string[]} args - adb sub-arguments (e.g. `["install", "app.apk"]`).
 * @param {string} [serial] - device serial; injected as `-s <serial>`.
 * @param {number} [timeoutMs] - command timeout.
 * @returns {Promise<ExecResult>}
 */
export function adb(args, serial, timeoutMs) {
  const full = serial ? ["-s", serial, ...args] : args;
  return run(ADB_BIN, full, timeoutMs);
}

/**
 * Run an `emulator` CLI command.
 * @param {string[]} args - emulator arguments (e.g. `["-list-avds"]`).
 * @param {number} [timeoutMs] - command timeout.
 * @returns {Promise<ExecResult>}
 */
export function emulator(args, timeoutMs) {
  return run(EMULATOR_BIN, args, timeoutMs);
}
