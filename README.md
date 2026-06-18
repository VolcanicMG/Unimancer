# Unimancer 🔮

**Command the Unity engine with AI.** Unimancer is a Model Context Protocol (MCP)
server that bridges AI assistants (Claude, Cursor, etc.) to the Unity Editor —
with **69 tools** spanning the core editor surface *plus* first-class
**Android build & device tooling** that other Unity MCP servers don't have.

```
[ AI client ] --MCP/stdio--> [ Unimancer server (Node/JS) ] --TCP(JSON)--> [ Unity Editor package (C#) ]
   Claude / Cursor                 src/                          tcp://127.0.0.1:8090   unity/
                                      |
                                      +--shell--> adb / emulator   (Android tools, no Unity needed)
```

- **`src/`** — the Node/JavaScript MCP server (stdio). Owns tool definitions, the
  Unity WebSocket client, and the `adb`/`emulator` shell helpers.
- **`unity/`** — the Unity UPM package (C#). Hosts a TCP server inside the
  Editor (auto-starts on load) and executes engine-side tools via reflection.

Targets **Unity 6000.5 (6.5)** / **Android Gradle Plugin 9.0** defaults.

## Quick start

1. **Install server deps** (through the supply-chain guard):
   ```bash
   guard install      # or: npm install
   ```
2. **Connect your MCP client** — print ready-to-paste config + Unity steps:
   ```bash
   node scripts/setup.mjs           # prints config for Claude Code / Desktop / Cursor
   node scripts/setup.mjs --write   # also drops a .mcp.json in the current folder
   ```
   Or one-liner for Claude Code:
   ```bash
   claude mcp add unimancer -- node /abs/path/to/unimancer/src/index.js
   ```
3. **Add the Unity package**:
   - **Recommended / first run** — Package Manager → **Add package from git URL** →
     `https://github.com/VolcanicMG/Unimancer.git?path=/unity` (read-only).
   - **Editable (to fix C#)** — clone the repo on the *same OS as Unity*, then
     **Add package from disk** → `<clone>/unity/package.json`.
   - **WSL note:** do not add from disk over a `\\wsl.localhost\...` UNC path — Unity
     rejects it; use the git URL or a Windows clone.

   The bridge **auto-starts** on Editor load. Check **Window → Unimancer → Setup** for live status.

## Tools (69)

| Group | Count | Needs Unity? | Examples |
|---|---|---|---|
| ADB device control | 12 | no (`adb`) | install, launch, logcat, screenshot, push/pull |
| Android build & config | 6 | yes | build APK/AAB, player settings, keystore, manifest, gradle |
| Emulator & SDK | 5 | no | list/start/stop AVDs, sdk_check |
| GameObjects & Components | 10 | yes | create, find, transform, add/remove/set component |
| Scenes, Assets & Prefabs | 12 | yes | open/save/new scene, hierarchy, asset CRUD, prefab create/instantiate |
| Scripts & Materials | 8 | yes | create/read/edit/delete script, find-in-files, material/shader |
| Editor, Console, Packages, Tests | 12 | yes | play/pause, console read/clear, menu, UPM add/remove, run tests, selection |
| Visual capture | 4 | yes | game view, scene view, camera, multi-angle (returned as images) |

## Environment variables

| Var | Default | Purpose |
|---|---|---|
| `UNITY_MCP_URL` | `tcp://127.0.0.1:8090` | Editor bridge URL |
| `ADB_PATH` | `adb` | path to the adb binary |
| `EMULATOR_PATH` | `emulator` | path to the Android emulator binary |

## Supply-chain safety

Guarded by [depguard](https://github.com/) (`guard`). Use `guard install` instead
of `npm install`; commits/pushes run `guard check` automatically.

> **Dependency note:** the official `@modelcontextprotocol/sdk` is pinned to
> **1.24.1** — the last release free of the `hono` subtree (which currently has
> HIGH advisories across *all* versions). The SDK's own two HIGH advisories
> (`GHSA-345p` HTTP-transport reuse, `GHSA-8r9q` UriTemplate ReDoS) are waived in
> `.guard-ignores` because Unimancer is **stdio-only and registers only tools** —
> neither code path is reachable. Revisit if HTTP transport or resource templates
> are ever added.

## Adding a tool

See [`docs/CODEMAP.md`](docs/CODEMAP.md). In short: each tool is one JS file
exporting `{ name, description, inputSchema, handler }` (added to its group
`index.js`); engine-side tools also get a C# `McpToolBase` subclass whose `Name`
equals the JS `name` (auto-discovered by reflection — no registration needed).
