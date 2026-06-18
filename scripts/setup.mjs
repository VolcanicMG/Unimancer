#!/usr/bin/env node
/**
 * Unimancer setup helper.
 *
 * Prints (and optionally writes) everything needed to connect an MCP client to
 * the Unimancer server and to install the Unity-side package. Zero dependencies
 * — runs on a bare Node install before `npm install`.
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
 * The MCP server definition every client embeds (stdio transport).
 * @returns {{command: string, args: string[], env: Record<string,string>}}
 */
function serverDef() {
  return { command: "node", args: [ENTRY], env: {} };
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
  cfg.mcpServers.unimancer = serverDef();
  writeFileSync(target, JSON.stringify(cfg, null, 2) + "\n");
  return target;
}

function main() {
  const args = process.argv.slice(2);

  if (args.includes("--json")) {
    console.log(JSON.stringify({ unimancer: serverDef() }, null, 2));
    return;
  }

  console.log("Unimancer 🔮  setup");
  console.log(`  repo:   ${REPO}`);
  console.log(`  server: node ${ENTRY}`);
  if (!existsSync(join(REPO, "node_modules"))) {
    console.log("\n⚠  Dependencies not installed. Run `guard install` (or `npm install`) in the repo first.");
  }

  // --- MCP client configs ---
  block("Claude Code / Cursor  →  .mcp.json (project root)", { mcpServers: { unimancer: serverDef() } });
  block("Claude Desktop  →  claude_desktop_config.json", { mcpServers: { unimancer: serverDef() } });
  console.log("\nClaude Code one-liner:  claude mcp add unimancer -- node " + ENTRY);

  // --- Unity side ---
  console.log("\n── Unity package (the C# bridge) ──");
  console.log("  The bridge auto-starts on Editor load (InitializeOnLoad) and listens on ws://127.0.0.1:8090.");
  console.log("  Recommended: Package Manager → + → Add package from git URL →");
  console.log(`     ${GIT_REMOTE}?path=/unity   (read-only; best for a first run)`);
  console.log("  Editable (for fixing C#): clone the repo on the SAME OS as Unity, then");
  console.log(`     Add package from disk → <clone>/unity/package.json`);
  console.log(`  Local path here: ${join(UNITY_PKG, "package.json")}`);
  console.log("  NOTE (WSL): do NOT add from disk over a \\\\wsl.localhost\\... path — Unity rejects it; use the git URL or a Windows clone.");
  console.log("  Requires Unity 6000.5+ and com.unity.nuget.newtonsoft-json (auto-resolved).");

  // --- Env knobs ---
  console.log("\n── Optional env vars ──");
  console.log("  UNITY_MCP_URL   bridge URL (default ws://127.0.0.1:8090)");
  console.log("  ADB_PATH        path to adb (default: adb on PATH)");
  console.log("  EMULATOR_PATH   path to the Android emulator binary");

  if (args.includes("--write")) {
    const p = writeProjectConfig();
    console.log(`\n✓ wrote ${p} — restart your MCP client to pick it up.`);
  } else {
    console.log("\nTip: re-run with --write to drop a ready .mcp.json into the current folder.");
  }
}

main();
