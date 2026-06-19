# Unimancer 🔮

**Command the Unity engine with AI.** Unimancer is a Model Context Protocol (MCP)
server that bridges AI assistants (Claude, Cursor, etc.) to the Unity Editor —
with **93 tools** spanning the core editor surface *plus* first-class
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
   - **WSL dev loop (edit here, Unity compiles there):** if this repo lives on the
     WSL filesystem but the package is embedded in a Windows-side Unity project,
     run `./scripts/dev-sync.sh [embedded-package-dir]` in a spare terminal. It
     content-mirrors `unity/` into the embedded copy on a 2 s poll (checksum-based,
     `*.meta` preserved), so your edits reach the Editor without manual copying.

   The bridge **auto-starts** on Editor load. Check **Window → Unimancer → Setup** for live status.
   If it ever shows *not listening* (e.g. the port was held by a stale socket),
   use **Window → Unimancer → Restart Bridge** (or the **Restart** button in the
   Setup window / Chat header). The bridge marks its socket **non-inheritable** (so
   Unity child processes — e.g. the AI Assistant `relay_win.exe` — can't keep port
   8090 open after the Editor dies), disposes the socket on every reload, and retries
   the bind, so a recompile/restart won't normally wedge it. If an *older* session's
   child still squats the port, find it with `netstat -ano | findstr :8090`, end that
   PID, then click Restart Bridge.

## In-Editor chat — no API key 🆕

A chat panel **inside Unity** (**Window → Unimancer → Chat**) that drives your local
`claude` CLI in headless streaming mode. It runs on your **Claude subscription, not a
pay-per-token API key**, and inherits the full Unimancer MCP tool surface — the agent
loop, tool dispatch, and MCP-client behaviour all live in Claude Code itself.

- Needs the `claude` CLI installed & logged in on the machine. **WSL is fine** — the
  window WSL-wraps the spawn (toggle in **Setup**). Keep `ANTHROPIC_API_KEY` unset so
  Claude Code uses your subscription.
- Reuses the Node server path you set in **Window → Unimancer → Setup**.
- Multi-turn via `--resume`; tool calls are shown inline. *Phase 2* adds @-referencing
  of Editor objects/assets.

## Docs

- [USAGE.md](docs/USAGE.md) — setup, tool groups, resources, notifications, runtime, troubleshooting
- [PITCH.md](docs/PITCH.md) — why Unimancer over other Unity MCPs
- [docs/CODEMAP.md](docs/CODEMAP.md) — architecture & how to add a tool

## Tools (93)

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
