/**
 * Covers the only branching logic in core/unityCli.js: how args are turned into
 * CLI flags, and how the `unity` JSON envelope becomes a result or a throw.
 *
 * A stub executable stands in for the real `unity` binary (via UNITY_CLI): it
 * echoes the argv it received inside a success envelope, or emits a canned
 * failure envelope when told to.
 */
import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { mkdtempSync, writeFileSync, rmSync, chmodSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { unityCommand } from "../src/core/unityCli.js";

let dir;
/** Stub that returns its own argv as the command result. */
let echoBin;
/** Stub that returns a failure envelope with one error. */
let failBin;

beforeAll(() => {
  dir = mkdtempSync(join(tmpdir(), "unimancer-cli-"));
  delete process.env.UNITY_PROJECT_PATH; // never inherit a real project from the shell

  echoBin = join(dir, "unity-echo");
  writeFileSync(
    echoBin,
    `#!/usr/bin/env node
console.log(JSON.stringify({ success: true, data: { result: process.argv.slice(2) } }));
`,
  );
  chmodSync(echoBin, 0o755);

  failBin = join(dir, "unity-fail");
  writeFileSync(
    failBin,
    `#!/usr/bin/env node
console.log(JSON.stringify({ success: false, data: null, errors: [{ code: "nope", message: "boom" }] }));
process.exit(6);
`,
  );
  chmodSync(failBin, 0o755);
});

afterAll(() => rmSync(dir, { recursive: true, force: true }));

describe("unityCommand", () => {
  it("builds the machine-readable invocation and passes scalar args as flags", async () => {
    process.env.UNITY_CLI = echoBin;
    const argv = await unityCommand("ui_dump", { target: "Canvas", includeInactive: true }, { projectPath: "/p" });
    expect(argv).toEqual([
      "command",
      "ui_dump",
      "--format",
      "json",
      "--no-banner",
      "--non-interactive",
      "--project-path",
      "/p",
      "--target",
      "Canvas",
      "--includeInactive",
      "true",
    ]);
  });

  it("JSON-stringifies object args and omits undefined ones", async () => {
    process.env.UNITY_CLI = echoBin;
    const argv = await unityCommand("ui_create", { archetype: "panel", parentPath: undefined, sizeDelta: { x: 1, y: 2 } });
    expect(argv).not.toContain("--parentPath");
    expect(argv.slice(-2)).toEqual(["--sizeDelta", '{"x":1,"y":2}']);
  });

  it("throws with the first error message when the envelope reports failure", async () => {
    process.env.UNITY_CLI = failBin;
    await expect(unityCommand("ui_dump", {}, {})).rejects.toThrow(/ui_dump failed: boom/);
  });
});
