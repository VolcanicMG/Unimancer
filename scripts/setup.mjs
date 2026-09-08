#!/usr/bin/env node
/**
 * Unimancer setup helper.
 *
 * Prints (and optionally writes) the TWO MCP servers a Unity project wants —
 * Unity's own `unity mcp` (the pipeline commands, incl. unimancer's ui_ and sprite_ ones)
 * and this Node server (adb / emulator / HTML->Unity bridge) — plus the Unity-side
 * install steps. Zero dependencies — runs on a bare Node install before `npm install`.
 *
 * Usage:
 *   node scripts/setup.mjs              # print config snippets + Unity steps
 *   node scripts/setup.mjs --write      # also write a project-local .mcp.json (Claude Code / Cursor)
 *   node scripts/setup.mjs --json       # print only the raw MCP server JSON
 */
import { fileURLToPath } from "node:url";
import { dirname, join, resolve } from "node:path";
import { writeFileSync, existsSync, readFileSync } from "node:fs";

const __dirname = dirname(fileURLToPath(import.meta.url));
/** Absolute path to the repo root (one level up from scripts/). */
const REPO = resolve(__dirname, "..");
/** Absolute path to the stdio server entry point. */
const ENTRY = join(REPO, "src", "index.js");
/** The Unity package subfolder, installable via UPM. */
const UNITY_PKG = join(REPO, "unity");
const GIT_REMOTE = "https://github.com/VolcanicMG/Unimancer.git";

/**
 * The Unimancer MCP server definition every client embeds (stdio transport).
 * @returns {{command: string, args: string[], env: Record<string,string>}}
 */
function serverDef() {
  return { command: "node", args: [ENTRY], env: {} };
}

/**
 * Unity's own stdio MCP server, exposing every com.unity.pipeline command
 * (including the ui_ and sprite_ commands this repo's UPM package contributes).
 * @param {string} [projectPath] - Unity project root; omit to let the CLI auto-detect.
 * @returns {{command: string, args: string[]}}
 */
function unityServerDef(projectPath) {
  const args = ["mcp"];
  if (projectPath) args.push("--project-path", projectPath);
  return { command: "unity", args };
}

/** Both servers, as an MCP client's `mcpServers` map. @param {string} [projectPath] */
function bothServers(projectPath) {
  return { unity: unityServerDef(projectPath), unimancer: serverDef() };
}

/** Pretty-print a labelled JSON block. @param {string} label @param {unknown} obj */
function block(label, obj) {
  console.log(`\n── ${label} ──`);
  console.log(JSON.stringify(obj, null, 2));
}

/**
 * Write a project-local `.mcp.json` (read by Claude Code and Cursor) into the
 * current working directory, merging if one already exists.
 * @returns {string} the path written
 */
function writeProjectConfig() {
  const target = resolve(process.cwd(), ".mcp.json");
  /** @type {{mcpServers: Record<string, unknown>}} */
  let cfg = { mcpServers: {} };
  if (existsSync(target)) {
    try { cfg = JSON.parse(readFileSync(target, "utf8")); cfg.mcpServers ??= {}; }
    catch { /* overwrite a corrupt file */ }
  }
  Object.assign(cfg.mcpServers, bothServers(process.env.UNITY_PROJECT_PATH));
  writeFileSync(target, JSON.stringify(cfg, null, 2) + "\n");
  return target;
}

function main() {
  const args = process.argv.slice(2);

  const projectPath = process.env.UNITY_PROJECT_PATH;

  if (args.includes("--json")) {
    console.log(JSON.stringify(bothServers(projectPath), null, 2));
    return;
  }

  console.log("Unimancer 🔮  setup");
  console.log(`  repo:   ${REPO}`);
  console.log(`  server: node ${ENTRY}`);
  if (!existsSync(join(REPO, "node_modules"))) {
    console.log("\n⚠  Dependencies not installed. Run `guard install` (or `npm install`) in the repo first.");
  }

  // --- step 1: Unity's own CLI + pipeline package (this is what drives the Editor) ---
  console.log("\n── 1. Unity CLI + pipeline package ──");
  console.log("  Install the `unity` CLI (beta channel), then in your Unity 6.0+ project:");
  console.log("     unity pipeline install --project-path <project>");
  console.log("     unity command editor_status --project-path <project>   # confirm the Editor answers");
  console.log("  That gives an AI client ~149 built-in tools via `unity mcp` (scenes, GameObjects,");
  console.log("  assets, prefabs, scripts, tests, build, capture, eval, …).");

  // --- step 2: this repo's UPM package (ui_*/sprite_* commands + the chat window) ---
  console.log("\n── 2. Unimancer UPM package (adds ui_*/sprite_* pipeline commands) ──");
  console.log("  Package Manager → + → Add package from git URL →");
  console.log(`     ${GIT_REMOTE}?path=/unity   (read-only; best for a first run)`);
  console.log("  Editable (for fixing C#): clone the repo on the SAME OS as Unity, then");
  console.log("     Add package from disk → <clone>/unity/package.json");
  console.log(`  Local path here: ${join(UNITY_PKG, "package.json")}`);
  console.log("  NOTE (WSL): do NOT add from disk over a \\\\wsl.localhost\\... path — Unity rejects it;");
  console.log("  use the git URL or a Windows clone. Requires Unity 6.0+ and com.unity.pipeline.");
  console.log("  Its 6 commands (ui_create, rect_transform_set, ui_dump, ui_build_from_manifest,");
  console.log("  sprite_import, sprite_generate) surface through `unity mcp` automatically.");

  // --- step 3: register both MCP servers ---
  console.log("\n── 3. Register both MCP servers ──");
  console.log("  Unity's server:     unity mcp configure claude-code");
  console.log(`  Unimancer's server: claude mcp add unimancer -- node ${ENTRY}`);
  console.log("  …or write the JSON by hand:");
  block("Claude Code / Cursor  →  .mcp.json (project root)", { mcpServers: bothServers(projectPath) });
  block("Claude Desktop  →  claude_desktop_config.json", { mcpServers: bothServers(projectPath) });

  // --- Env knobs ---
  console.log("\n── Optional env vars ──");
  console.log("  UNITY_CLI            path to the `unity` binary (default: unity on PATH)");
  console.log("  UNITY_PROJECT_PATH   Unity project root passed to `unity command` / `unity mcp`");
  console.log("  ADB_PATH             path to adb (default: adb on PATH)");
  console.log("  EMULATOR_PATH        path to the Android emulator binary");

  if (args.includes("--write")) {
    const p = writeProjectConfig();
    console.log(`\n✓ wrote ${p} — restart your MCP client to pick it up.`);
  } else {
    console.log("\nTip: re-run with --write to drop a ready .mcp.json into the current folder.");
  }
}

main();
