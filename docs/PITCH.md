# Why Unimancer (alongside Unity's own MCP)?

Unity ships an official CLI and MCP server now (`unity mcp`, via
`com.unity.pipeline`) — ~150 built-in tools covering scenes, GameObjects,
components, assets, prefabs, scripts, animation, tests, build, capture, and
arbitrary C# (`eval`). That's the Editor bridge, solved, by Unity itself.
Unimancer no longer competes with it — it's the **companion** that covers what
it doesn't: **Android devices**, an **HTML→Unity art pipeline**, and **UGUI/
sprite authoring**, all riding on Unity's own transport where it makes sense.

---

## The gap Unity's own MCP leaves open

`unity mcp` is deep on Editor-state and desktop workflows, but:
- **No device-level Android tooling.** No `adb install`, no on-device
  logcat/screenshot, no push/pull. Mobile is where a huge share of Unity
  games ship — and it's still a blind spot.
- **No emulator/AVD lifecycle.** Listing, starting, and stopping Android
  Virtual Devices, or checking an SDK install, isn't in scope for an
  Editor-bridge tool.
- **No path from an HTML mockup to native UI.** Authoring UGUI still means
  hand-building RectTransform trees or writing C#, even though AI clients are
  fluent in HTML/CSS.

---

## What Unimancer adds

### 1. Android is still first-class (nobody else has this)
17 tools: `adb` device control (install, launch, **logcat**, **screenshot**,
push/pull) and emulator/SDK management. Ship-to-phone tooling that doesn't
depend on any Unity Editor being open at all.

### 2. An HTML→Unity art pipeline (nobody else has this)
Hand it a **Claude Design HTML mockup** and it becomes real Unity UI. The
`bridge` group drives headless Chromium (Playwright) to **decompose** each
widget into separate layers — the frame sprite *without* its text, icons
peeled out, live text kept as TMP — auto-detecting `<button>` components,
computing 9-slices from CSS, emitting transparent PNGs and standalone SVGs
(with namespaces + CSS-var colors resolved) — then hands the manifest to
Unity's own CLI (`unity command ui_build_from_manifest`) to reassemble it into
a GameObject tree. `html_to_unity` does the whole thing — design → built UI —
in **one call**. The upshot: AI can author UI in HTML (its native medium) and
ship it as native uGUI, without Unimancer needing to own the Editor connection.

### 3. UGUI/sprite/component/animation/Android authoring, contributed upstream through Unity's own transport
Rather than keep a second bridge alive, Unimancer's tools (`ui_create`,
`rect_transform_set`, `ui_dump`, `ui_build_from_manifest`, `ui_click`,
`sprite_import`, `sprite_generate`, `component_list`,
`animator_set_parameter`, `android_player_settings`) are **ten
`[CliCommand]` static methods** that `com.unity.pipeline` discovers and
`unity mcp` surfaces automatically. One transport, one token-gated server,
zero duplicate protocol surface — and these commands get the exact same
undo/dry-run/confirm conventions as Unity's own ~150. `component_list` stays
deliberately lighter than the built-in `get_component_properties` (types/
enabled/instanceID, no serialized-property dump), and
`android_player_settings` never accepts or returns keystore/alias
**passwords** — those stay on `unity build --android-keystore-*`.

### 4. A real in-Editor AI chat — on your subscription
A chat panel **inside Unity** (Window → Unimancer → Chat) drives the `claude`
CLI headless, so it runs on your **Claude subscription, not a pay-per-token API
key**. It registers **both** MCP servers (`unity` + `unimancer`) so the agent
gets the full combined surface — Unity's ~150 built-ins plus Unimancer's 21 —
in one chat. It streams replies with inline **Edit/Write diffs**, takes
**screenshots** (capture / paste / drag) the agent can actually see, renders
the agent's questions as **clickable choices**, lets you **keep typing**
(follow-ups queue while it works), **survives Play mode** (optional) and
recompiles (auto-resume). No external editor or API key required.

### 5. Lean and supply-chain-safe
Three runtime deps (`@modelcontextprotocol/sdk` + `zod`, plus `playwright` for
the HTML→Unity bridge), pinned exact, guarded by [depguard]. No TCP client, no
reflection-based dispatch, no socket lifecycle to babysit — the only thing the
Node server shells out to Unity for is `unity command
ui_build_from_manifest`, at the end of one tool.

---

## At a glance

| | Unimancer (companion) | `unity mcp` (built-in) |
|---|---|---|
| Android device control (adb/logcat/screenshot) | ✅ 12 tools | ❌ |
| Emulator/AVD lifecycle | ✅ 5 tools | ❌ |
| HTML mockup → native Unity UI pipeline | ✅ (Playwright decompose + `unity command` build) | ❌ |
| Scenes / GameObjects / assets / prefabs / scripts / build / tests / capture / `eval` | ❌ (use `unity mcp`) | ✅ ~150 tools |
| UGUI/sprite/component/animation/Android authoring | ✅ 10 tools, contributed as pipeline commands, surfaced via `unity mcp` | via unimancer's contribution |
| In-Editor AI chat (no API key, on subscription) | ✅ — drives both servers at once | — |
| Runtime deps | **3** (pinned, guarded; `playwright` for the HTML bridge) | n/a (Unity CLI) |

---

## Honest limitations

- The `ui_*`/`sprite_*` commands need the Unimancer UPM package installed
  alongside `com.unity.pipeline` — they aren't part of Unity's own package.
- Android tools need a working `adb` / Android SDK on the host.
- `html_to_unity`'s build step needs the `unity` CLI on PATH and an open,
  non-Safe-Mode Editor for the target project.
- It's young and community-built — no commercial support contract (yet).

If your work touches **mobile devices**, an **HTML→Unity art workflow**, or
you want UGUI/sprite authoring alongside Unity's own ~150 built-ins in one
chat — Unimancer is built for exactly that gap.

[depguard]: https://github.com/
