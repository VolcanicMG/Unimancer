# Using Unimancer

Unimancer is now a **companion** MCP server that sits next to Unity's own
`unity mcp` — **21 tools** across 3 groups (`adb`, `emulator`, `bridge`),
covering what Unity's ~150 built-ins don't: Android devices, and the
HTML→Unity art pipeline. UGUI/sprite authoring rides on Unity's own transport
as two contributed pipeline commands (`ui_*`, `sprite_*`).

---

## 1. How it fits together

```
[ AI client (Claude/Cursor) ]
        │
        ├── MCP/stdio ──> unity mcp ──> com.unity.pipeline (in the open Editor)
        │                 ~150 built-in tools (scenes, GameObjects, assets,
        │                 prefabs, scripts, tests, build, capture, eval…)
        │                 + unimancer's 10 [CliCommand]s: ui_*, sprite_*,
        │                 component_list, animator_set_parameter, android_player_settings
        │
        └── MCP/stdio ──> unimancer (Node, 21 tools)
                          |-- adb / emulator   (Android, no Unity needed)
                          |-- Playwright       (HTML -> Unity art bridge)
                          `-- `unity command`  (only for ui_build_from_manifest)
```

- **`unity mcp`** — Unity's own stdio MCP server (from the `unity` CLI). Talks
  directly to `com.unity.pipeline` inside a running Editor over a token-gated
  localhost HTTP server. Keep the Editor open while using its tools.
- **`unimancer`** — this Node server. `adb`/`emulator` need no Unity at all;
  `bridge`'s export/preview tools are pure Playwright; only `html_to_unity`'s
  final build step shells out to `unity command`.

See the [README's "what moved where" table](../README.md#what-moved-where) for
exactly which built-in `unity mcp` commands replace each tool group unimancer
used to own.

---

## 2. Setup

1. **Install the Unity CLI** (`unity --version` to check):
   ```bash
   curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash   # macOS/Linux
   ```
   ```powershell
   $env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex   # Windows
   ```
2. **Install the pipeline package into your project**: `unity pipeline install --project-path /path/to/YourProject`.
3. **Add the Unimancer UPM package** (Package Manager → **+**):
   - Add from git URL → `…/Unimancer.git?path=/unity` (public repo), or
   - Add from disk → `unity/package.json` (same OS as Unity; for WSL, use a
     Windows clone or the embedded-copy approach).
4. **Install server deps** (in this repo):
   ```bash
   guard install      # or: npm install
   ```
5. **Configure your MCP client** — `unity mcp configure claude-code --project-path /path/to/YourProject`
   registers the `unity` server; add `unimancer` alongside it:
   ```bash
   claude mcp add unimancer -- node /ABS/PATH/unimancer/src/index.js
   ```
   Or print/write the combined config with `node scripts/setup.mjs [--write]`.
6. **Verify**: `unity status` shows a connected Editor; `unity mcp --project-path <p>`
   over stdio lists ~150+10 tools. The Unimancer tools appear once the client
   reconnects with the `unimancer` server entry present.

> **WSL note:** when Unity runs on Windows and your agent runs in WSL, put a
> small `unity` shim on the WSL PATH that `exec`s the Windows `unity.exe` —
> `unity mcp` then drives the Windows Editor process through it. The
> `unimancer` Node server itself runs natively in WSL.

---

## 3. Tool groups

Tools are named `group_action`. Just ask in natural language — the client picks the tool.

| Group | Count | Needs Unity? | Examples / what to ask |
|---|---|---|---|
| **adb** | 12 | no | "install build.apk on the device", "stream the Unity logcat", "screenshot the phone" |
| **emulator** | 5 | no | "list AVDs", "start the Pixel emulator", "check my Android SDK setup" |
| **bridge** | 4 | export/preview: no (Playwright); build: yes (`unity command`) | "inventory this Claude Design HTML mockup", "preview this HTML" (`html_preview` — full crop + every separated layer per component (frame, sub-sprites, icons), each isolated as a shape-accurate transparent sprite (no black corners) with its own 9-slice or none; inline, or written to outDir for the Unity preview popout (auto-cleaned)), "export this HTML into Unity components", "take this HTML and build it in Unity" (`html_to_unity`, one-shot export+build). Needs `playwright@1.61.0` + `npx playwright install chromium` |

**Everything else** now lives in `unity mcp`'s ~150 built-ins, plus unimancer's
10 contributed pipeline commands:

| Contributed command | Tag | What it does |
|---|---|---|
| `ui_create` | `ui` | create a UI archetype (Button/Panel/Text/…) on a Canvas |
| `rect_transform_set` | `ui` | set RectTransform layout / anchor presets |
| `ui_dump` | `ui` | dump a canvas's UI tree |
| `ui_build_from_manifest` | `ui` | build a GameObject tree from an `html_to_unity`/`html_export` manifest |
| `ui_click` | `ui` | fire pointer enter/down/up/click through `ExecuteEvents` on the EventSystem (plus submit for `Selectable`s) against a hierarchy path or name — no Input System dependency, works in Editor Play Mode |
| `sprite_import` | `sprites` | import an image as a 9-slice sprite |
| `sprite_generate` | `sprites` | procedurally generate a sprite PNG |
| `component_list` | `gameobjects` | list a GameObject's components (type/enabled/instanceID) — lighter than the built-in `get_component_properties`: no serialized-property dump |
| `animator_set_parameter` | `animation` | set an Animator parameter (float/int/bool), or fire a Trigger when no value is given; most meaningful while the Animator is actively evaluating in Play Mode — in edit mode the value generally doesn't persist |
| `android_player_settings` | `android` | get-or-set Android player settings (application id, bundle version code, min/target SDK, target architectures, scripting backend, keystore name/alias); a bare call reads, supplied args are applied. **Never accepts or returns keystore/alias passwords** — those go through `unity build --android-keystore-*` |

For the ~150 Unity built-ins (scenes, GameObjects, components, assets, prefabs,
scripts, animation, baking, build, tests, packages, capture, `eval`, …), see
`unity list` / `unity command` or the `unity-cli` / `unity-pipeline` skills.

---

## 4. In-Editor chat (no API key)

**Window → Unimancer → Chat** opens a chat panel that talks to your local `claude`
CLI in headless streaming mode (`claude -p --output-format stream-json`). Because it
drives Claude Code itself, it:

- runs on your **Claude subscription, not a pay-per-token API key** (it never passes
  `--bare` and unsets `ANTHROPIC_API_KEY`, so Claude Code uses your logged-in auth);
- registers an mcp-config with **both** servers — `unity` (`unity mcp --project-path
  <project>`) and `unimancer` (this Node server) — with `allowedTools`
  `mcp__unity,mcp__unimancer`, so the agent loop in Claude Code drives the full
  combined tool surface.

Requirements & notes:
- The `claude` CLI must be installed and logged in on the machine. On Windows+WSL,
  leave the **WSL wrap** toggle on (Setup window) so the spawn is `wsl bash -lc …`.
- Set the Node server path once in **Window → Unimancer → Setup**; the chat reuses it.
- Conversations are multi-turn (`--resume <session_id>`); tool calls render inline.
- **Settings** (in the Chat window): `claude` command, model override, and the
  `--allowedTools` value (default `mcp__unity,mcp__unimancer`).
- This feature is for machines that have Claude Code; both MCP servers still work
  with any MCP client independently of this window.

**Rich editor features:**
- **Reference objects** — drag GameObjects/assets anywhere onto the chat window (a drop
  overlay appears while dragging), click **@ Reference** to
  pick one, or **Use selection**. Attached objects appear as chips and are expanded into a
  `## Referenced Unity objects` context block (name, type, hierarchy/asset path, components)
  on send, so the agent gets real data.
- **Clickable replies** — when the agent writes `[[unity:<handle>]]`, the window renders a
  `↪ <name>` link that selects + pings the object in the Hierarchy/Project. Handles use
  `GlobalObjectId` (or a hierarchy path), seeded from the objects you attach.
- **Inline images** — screenshots returned by `capture_game_view`/`capture_scene_view` render
  directly in the thread.
- **History** — every conversation is saved under `<Project>/Library/Unimancer/Chats/`
  (excluded from version control) and reloads via the **History** button, resuming the
  underlying Claude Code session. The menu also has **Delete/‹chat›** (per-chat, confirmed)
  and **Delete all chats…**.
- **Project context** — the editable system prompt (Settings) tells the agent things like
  "this project uses Unity Version Control, do not `git init`".
- **Markdown rendering** — replies render headings, bold, lists, inline code, and fenced
  code blocks (each with **Copy** and **Save…** buttons).
- **Version** — the package version (from `package.json`) is shown in the chat header
  next to the title and at the top of **Settings**.
- **Keep typing while it streams** — the input stays live during a reply; pressing **Send**
  (it shows **Queue** while busy) queues a follow-up that auto-sends when the turn ends.
- **Survives recompiles** — the chat reopens the last conversation and seeds `--resume` after
  a script recompile / Play-mode toggle, so it picks up where it left off.
- **Editor-aware** — the agent is told it's in the Unimancer Chat panel inside the Unity
  Editor, with the live Unity version / project / active scene as context.
- **Pick-an-answer buttons** — when the agent offers a choice (it emits a `unimancer:ask`
  block), the panel renders the options as clickable buttons; you can still type a free reply.
- **Send screenshots** — attach images via **📷 Capture** (renders the Game/Scene view),
  **🖼 Image** (file picker), or by dragging image files onto the window; the agent views
  them with the Read tool.
- **Edit diffs** — when the agent uses the built-in Edit/Write/MultiEdit tools, the panel
  shows a red/green diff of the change (capped for large writes), like the normal console.
- **Keep chat alive in Play mode** (Settings toggle) — entering Play normally triggers a
  domain reload that interrupts the chat; this disables that reload so a turn keeps running.
  Tradeoff: statics/events aren't reset between Play sessions (a script recompile still
  reloads, and the chat auto-resumes then).
- **Usage/cost** — each turn shows tokens used (in↑/out↓) and per-turn cost on its
  "Worked for" line; a running session total (Σ tokens · $) sits in the header. Parsed
  from the `result` event's `usage`/`total_cost_usd` (resets when the window reloads).
- **Subscription limit badges** — next to the `ctx` bar the header shows a status-colored
  dot + window label + time-to-reset for your Claude **5-hour** (`5h`) and **weekly** (`wk`)
  limits (e.g. `● 5h 2h14m`; hover for the exact reset time). Green = headroom, amber =
  using overage, red = limit reached. Parsed from claude's `rate_limit_event`, which carries
  per-window **status + reset time** — not an exact percentage (that's only in Claude Code's
  own statusline). A window's badge appears once claude has reported it this session.
- **Permission mode** (Settings) — **Auto-approve** (agent acts) or **Plan (propose only)**
  (`--permission-mode plan`: the agent proposes changes without executing — review, then
  switch to Auto and tell it to proceed).
- **Quick actions** (toolbar) — *Describe scene*, *Explain selection* (auto-attaches the
  selection), *Fix last error* (grabs the latest Console error), and a **Sync selection**
  toggle that auto-attaches the current selection to every message.

---

## 5. Environment variables

| Var | Default | Purpose |
|---|---|---|
| `UNITY_CLI` | `unity` | path/name of the Unity CLI binary the Node server shells out to |
| `UNITY_PROJECT_PATH` | — | project passed to `unity command` when a tool doesn't specify one |
| `ADB_PATH` | `adb` | path to adb |
| `EMULATOR_PATH` | `emulator` | path to the Android emulator |

---

## 6. Troubleshooting

| Symptom | Fix |
|---|---|
| `unity status` shows no connected Editor | open the project's Editor and wait for it to finish loading; a Safe Mode project (compile errors) won't expose the pipeline — fix the errors and restart |
| `unity mcp` doesn't list `ui_*`/`sprite_*` | the Unimancer UPM package isn't installed in the project, or the Editor hasn't recompiled since adding it — check the Console for `CS####` errors |
| `html_to_unity`'s build step fails | confirm `unity` is on PATH (or set `UNITY_CLI`) and `UNITY_PROJECT_PATH`/the tool's `projectPath` arg points at an open, non-Safe-Mode Editor |
| `instanceID` looks like a huge number | it's a string (EntityId, exceeds JS int range) — target objects by **path** or pass the id back as a string |
| Changed C# but no effect | Unity recompiles on focus — alt-tab to the Editor, or run `unity command recompile` |
| Package add fails over `\\wsl.localhost\…` | use a Windows clone or embed into `<Project>/Packages/`; private repo blocks the git-URL method |
| Chat window does nothing / errors | `claude` not on PATH in the spawned shell, not logged in, or `ANTHROPIC_API_KEY` is set (unset it for subscription auth); set the Node path in Setup |

---

For architecture/contributing, see [`CODEMAP.md`](CODEMAP.md). For the project overview, see the [README](../README.md).
