# Using Unimancer

Unimancer is an MCP server that lets an AI assistant drive the Unity Editor — and
a running game — with **93 tools** across 14 groups, plus live resources and
event notifications. First-class **Android** tooling sets it apart.

---

## 1. How it fits together

```
[ AI client (Claude/Cursor) ]
        │ MCP (stdio)
        ▼
[ Unimancer server (Node) ]──TCP :8090──> [ Unity EDITOR bridge ]   (always, while the Editor is open)
        │      │
        │      └─────────────TCP :8091──> [ Unity RUNTIME bridge ]  (only in Play mode or a Dev build)
        │
        └──shell──> adb / emulator        (Android tools, no Unity needed)
```

- **Editor bridge (`:8090`)** — runs whenever the Unity Editor is open. Powers the
  Editor tools (scenes, GameObjects, assets, build, profiler, …).
- **Runtime bridge (`:8091`)** — runs only while the game is *running* (Editor Play
  mode, or a Development build). Powers the `runtime_*` tools that drive a live game.
- Keep the Unity Editor open while using Editor tools.

---

## 2. Setup

1. **Install server deps** (in the repo):
   ```bash
   guard install      # or: npm install
   ```
2. **Connect your MCP client** — print the exact config:
   ```bash
   node scripts/setup.mjs            # prints config for Claude Code / Desktop / Cursor
   node scripts/setup.mjs --write    # drops a .mcp.json in the current folder
   ```
   Claude Code one-liner:
   ```bash
   claude mcp add unimancer -- node /ABS/PATH/unimancer/src/index.js
   ```
3. **Add the Unity package** to your project (Package Manager → **+**):
   - Add from disk → `unity/package.json` (on the same OS as Unity; for WSL, use a
     Windows clone or the embedded-copy approach), or
   - Add from git URL → `…/Unimancer.git?path=/unity` (public repo only).
   The bridge auto-starts on load. Check **Window → Unimancer → Setup** (it also
   writes a `.mcp.json` for you).
4. **Verify**: the Unity Console shows `[Unimancer] Bridge listening on tcp://127.0.0.1:8090` and `Registered N tools.`

> **WSL note:** if Unity is on Windows and your MCP client runs in WSL, enable WSL
> *mirrored networking* so `127.0.0.1:8090` is shared, or run the Node server on
> Windows. (The Setup window has a "wrap with wsl" config option.)

---

## 3. Tool groups

Tools are named `group_action`. Just ask in natural language — the client picks the tool.

| Group | Count | Examples / what to ask |
|---|---|---|
| **adb** | 12 | "install build.apk on the device", "stream the Unity logcat", "screenshot the phone" |
| **androidBuild** | 6 | "switch to Android", "set the package id and IL2CPP/ARM64", "build an AAB", "configure the keystore" |
| **emulator** | 5 | "list AVDs", "start the Pixel emulator", "check my Android SDK setup" |
| **gameObject** | 10 | "create a Cube at 0,5,0", "add a Rigidbody to Player", "set Player's position" |
| **sceneAssets** | 12 | "open MainScene", "show the hierarchy", "find all materials", "make a prefab from Enemy" |
| **scripts** | 8 | "create a script", "read PlayerController.cs", "find 'using' in Assets" |
| **editor** | 12 | "enter play mode", "read console errors", "run EditMode tests", "what's selected?" |
| **capture** | 4 | "screenshot the Game view", "render the scene from 6 angles" (returns images) |
| **scriptEdit** | 3 | "validate this C#", "apply these edits to Foo.cs" (sha-guarded, no clobber) |
| **profiler** | 4 | "what's memory usage?", "sample frame stats for 60 frames" |
| **navmesh** | 4 | "bake the navmesh", "bake lighting" |
| **animation** | 5 | "create a clip", "add a position curve", "inspect the Animator on Boss" |
| **batch** | 1 | run many tools in one call (see §6) |
| **runtime** | 7 | drive a *running* game (see §5) |

---

## 4. Resources (read-only context)

The client can read live Editor state as resources without spending a tool call:

| URI | Contents |
|---|---|
| `unity://scene/hierarchy` | active scene tree |
| `unity://console` | recent console entries |
| `unity://selection` | current selection |
| `unity://project` | project + build settings |
| `unity://editor/state` | play/pause/compile state |

---

## 5. Notifications (events)

The bridge pushes events to the client as MCP logging notifications, so the AI can
react without polling:
- `compilation_finished` — scripts recompiled
- `play_mode` — entered/exited play mode
- `console_error` — an error/exception/assert was logged

Example: "edit this script, then tell me when it compiles and whether it errored."

---

## 6. Batch execution

`batch_execute` runs several tools in one request — fewer round-trips for multi-step
work. Conceptually:
```json
{ "calls": [
  { "tool": "gameobject_create", "args": { "name": "Enemy", "primitive": "Capsule" } },
  { "tool": "component_add",     "args": { "target": "Enemy", "componentType": "Rigidbody" } },
  { "tool": "gameobject_set_transform", "args": { "target": "Enemy", "position": { "x":0,"y":3,"z":0 } } }
], "stopOnError": true }
```

---

## 7. Runtime / in-player (driving a running game)

The `runtime_*` tools talk to the **runtime bridge (:8091)**, which only listens
while the game is running:
- **Play mode:** just press Play; the bridge starts automatically.
- **Development build (desktop):** make a *Development Build*; it listens on :8091.
- **Android device:** Development Build with **INTERNET permission** (use
  `android_manifest` to add it), then forward the port from the host:
  ```bash
  adb forward tcp:8091 tcp:8091
  ```
  Release builds never open the socket.

Tools: `runtime_scene_info`, `runtime_find_objects`, `runtime_get_component`,
`runtime_set_component_property`, `runtime_call_method` (invoke a method on a live
component — "drive the game"), `runtime_set_timescale` (pause/slow/speed), `runtime_log_tail`.

Example: "set timeScale to 0.2, find the Player, and call TakeDamage(10) on its Health component."

---

## 8. In-Editor chat (no API key)

**Window → Unimancer → Chat** opens a chat panel that talks to your local `claude`
CLI in headless streaming mode (`claude -p --output-format stream-json`). Because it
drives Claude Code itself, it:

- runs on your **Claude subscription, not a pay-per-token API key** (it never passes
  `--bare` and unsets `ANTHROPIC_API_KEY`, so Claude Code uses your logged-in auth);
- inherits the **full Unimancer MCP tool surface** automatically — the agent loop and
  tool dispatch live in Claude Code, not in this window.

Requirements & notes:
- The `claude` CLI must be installed and logged in on the machine. On Windows+WSL,
  leave the **WSL wrap** toggle on (Setup window) so the spawn is `wsl bash -lc …`.
- Set the Node server path once in **Window → Unimancer → Setup**; the chat reuses it.
- Conversations are multi-turn (`--resume <session_id>`); tool calls render inline.
- **Settings** (in the Chat window): `claude` command, model override, and the
  `--allowedTools` value (default `mcp__unimancer` = allow all Unimancer tools).
- This feature is for machines that have Claude Code; the MCP server itself still works
  with any MCP client independently.

**Rich editor features:**
- **Reference objects** — drag GameObjects/assets into the chat, click **@ Reference** to
  pick one, or **Use selection**. Attached objects appear as chips and are expanded into a
  `## Referenced Unity objects` context block (name, type, hierarchy/asset path, components)
  on send, so the agent gets real data.
- **Clickable replies** — when the agent writes `[[unity:<handle>]]`, the window renders a
  `↪ <name>` link that selects + pings the object in the Hierarchy/Project. Handles use
  `GlobalObjectId` (or a hierarchy path), seeded from the objects you attach.
- **Inline images** — screenshots returned by the capture tools render directly in the thread.
- **History** — every conversation is saved under `<Project>/Library/Unimancer/Chats/`
  (excluded from version control) and reloads via the **History** button, resuming the
  underlying Claude Code session. The menu also has **Delete/‹chat›** (per-chat, confirmed)
  and **Delete all chats…**.
- **Project context** — the editable system prompt (Settings) tells the agent things like
  "this project uses Unity Version Control, do not `git init`".
- **Markdown rendering** — replies render headings, bold, lists, inline code, and fenced
  code blocks (each with **Copy** and **Save…** buttons).
- **Permission mode** (Settings) — **Auto-approve** (agent acts) or **Plan (propose only)**
  (`--permission-mode plan`: the agent proposes changes without executing — review, then
  switch to Auto and tell it to proceed).
- **Quick actions** (toolbar) — *Describe scene*, *Explain selection* (auto-attaches the
  selection), *Fix last error* (grabs the latest Console error), and a **Sync selection**
  toggle that auto-attaches the current selection to every message.

---

## 9. Environment variables

| Var | Default | Purpose |
|---|---|---|
| `UNITY_MCP_URL` | `tcp://127.0.0.1:8090` | Editor bridge endpoint |
| `UNITY_MCP_RUNTIME_URL` | `tcp://127.0.0.1:8091` | Runtime bridge endpoint |
| `ADB_PATH` | `adb` | path to adb |
| `EMULATOR_PATH` | `emulator` | path to the Android emulator |

---

## 10. Troubleshooting

| Symptom | Fix |
|---|---|
| No `Window → Unimancer` menu / no "Registered" log | C# didn't compile — check the Console for `CS####` errors |
| `Unity connection failed at 127.0.0.1:8090` | Editor not open, or (WSL) localhost not shared — enable mirrored networking |
| `runtime_*` tools time out | the game isn't running (enter Play mode / dev build), or forward :8091 on device |
| `instanceID` looks like a huge number | it's a string (EntityId, exceeds JS int range) — target objects by **path** or pass the id back as a string |
| Changed C# but no effect | Unity recompiles on focus — alt-tab to the Editor; restart the MCP client for Node-side changes |
| Package add fails over `\\wsl.localhost\…` | use a Windows clone or embed into `<Project>/Packages/`; private repo blocks the git-URL method |
| Chat window does nothing / errors | `claude` not on PATH in the spawned shell, not logged in, or `ANTHROPIC_API_KEY` is set (unset it for subscription auth); set the Node path in Setup |

---

For architecture/contributing, see [`CODEMAP.md`](CODEMAP.md). For the project overview, see the [README](../README.md).
