/**
 * Thin shell-out to Unity's official `unity` CLI.
 *
 * Unimancer no longer speaks its own protocol to the Editor — `com.unity.pipeline`
 * hosts the server and the `unity` binary is the client. This is the ONLY place in
 * the Node server that talks to Unity; everything else here is adb/emulator/Playwright.
 *
 *   unity command <name> --format json --no-banner --non-interactive [--key value ...]
 */
import { execFile } from "node:child_process";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);

/** How long (ms) to wait for a Unity command before killing it. */
const TIMEOUT_MS = 120_000;

/**
 * Run one pipeline command in a live Unity Editor and return its result payload.
 *
 * Scalars are passed as `--key value`; objects and arrays are JSON-stringified
 * (the CLI's documented form for structured args, e.g. `--args '[3, "Green"]'`).
 * `undefined`/`null` args are omitted so optional params keep their Unity-side defaults.
 *
 * @param {string} name - pipeline command name (e.g. "ui_build_from_manifest").
 * @param {Record<string, unknown>} [args] - command parameters.
 * @param {{projectPath?: string}} [opts] - target project; falls back to `UNITY_PROJECT_PATH`.
 * @returns {Promise<any>} the command's `result` payload.
 * @throws {Error} when the CLI fails, emits unparseable output, or reports `success:false`.
 */
export async function unityCommand(name, args = {}, { projectPath } = {}) {
  const argv = ["command", name, "--format", "json", "--no-banner", "--non-interactive"];

  const project = projectPath ?? process.env.UNITY_PROJECT_PATH;
  if (project) argv.push("--project-path", project);

  for (const [key, value] of Object.entries(args)) {
    if (value === undefined || value === null) continue;
    argv.push(`--${key}`, typeof value === "object" ? JSON.stringify(value) : String(value));
  }

  const bin = process.env.UNITY_CLI || "unity";
  let stdout;
  try {
    ({ stdout } = await execFileAsync(bin, argv, { timeout: TIMEOUT_MS, maxBuffer: 64 * 1024 * 1024 }));
  } catch (e) {
    // A failed command still prints its JSON envelope on stdout; prefer that over the raw exit.
    if (!e.stdout) throw new Error(`unity ${argv.join(" ")} failed: ${e.stderr || e.message}`);
    stdout = e.stdout;
  }

  let payload;
  try {
    payload = JSON.parse(stdout);
  } catch {
    throw new Error(`unity ${name}: could not parse CLI output as JSON: ${stdout.slice(0, 500)}`);
  }

  // Success envelope: { success, command, data: { result, ... }, errors, warnings }.
  if (payload.success) return payload.data && "result" in payload.data ? payload.data.result : payload.data;
  const message = payload.errors?.[0]?.message ?? payload.message ?? JSON.stringify(payload);
  throw new Error(`unity ${name} failed: ${message}`);
}
