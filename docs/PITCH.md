# Why Unimancer?

There are several good Unity MCP servers. Unimancer is the one to pick when you
want **(1) real Android device + build control, (2) to drive a *running* game, not
just the Editor, and (3) agent-grade ergonomics** — in one lean, supply-chain-safe
package.

---

## The gap in the others

Today's Unity MCPs are **Editor-only and desktop-only**. They're great at moving
GameObjects and editing scenes, but:
- **None ship device-level Android tooling.** No `adb install`, no on-device
  logcat/screenshot, no keystore/AAB/manifest control. Mobile is where a huge share
  of Unity games ship — and it's a blind spot.
- **Most can't touch a *running* game** — only the Editor. The one that does runtime
  doesn't pair it with mobile.
- **Ergonomics vary** — many lack batching, live context resources, or event
  notifications, so an agent burns round-trips and polls for state.

Unimancer was built clean-room after studying the best of CoderGamester, CoplayDev,
and IvanMurzak — then adding the parts none of them combine.

---

## What makes Unimancer different

### 1. Android is first-class (nobody else has this)
23 Android tools: `adb` device control (install, launch, **logcat**, **screenshot**,
push/pull), build & config (APK/**AAB**, player settings, **IL2CPP/ARM64**,
**keystore**, manifest, gradle — tuned for **Unity 6.5 / AGP 9**), and emulator/SDK
management. Ship-to-phone is a first-class workflow, not an afterthought.

### 2. Editor **and** runtime — including on a device
85 Editor tools **plus** 11 runtime tools that drive a *live game*. Pause time, find
live objects, read/poke components, and **call methods on a running game** — in Play
mode, a desktop dev build, or **a Development build on an Android phone** (reachable
via `adb forward`). Editor + runtime + Android together is unique to Unimancer.

### 3. Agent-grade ergonomics
- **batch_execute** — many tool calls in one request (no round-trip tax on multi-step edits).
- **Resources** — `unity://scene/hierarchy`, `/console`, `/selection`, `/project`,
  `/editor/state`: the model pulls live context for free.
- **Notifications** — the bridge pushes compile-finished / play-mode / console-error
  events, so the AI reacts instead of polling.
- **Safe edits** — `script_apply_edits` is sha-guarded (never clobbers concurrent
  changes); `script_validate` uses Unity's bundled **Roslyn** for real syntax checks.
- **The AI can see** — capture tools return the Game/Scene view as images.

### 4. Lean and supply-chain-safe
Three runtime deps (`@modelcontextprotocol/sdk` + `zod`, plus `playwright` for the
HTML→Unity bridge), pinned exact, guarded by
[depguard]. SDK deliberately pinned to dodge known transitive CVEs; every C# file
writer is path-guarded; the runtime bridge **never opens a socket in release builds.**

### 5. It actually connects in Unity
Uses a raw **TCP** line protocol — not WebSocket — because Unity's Mono runtime
can't perform server-side WebSocket upgrades (a trap that bites HttpListener-based
implementations). Domain-reload-safe, auto-reconnecting, one-command setup.

### 6. An HTML→Unity art pipeline (nobody else has this)
Hand it a **Claude Design HTML mockup** and it becomes real Unity UI. The `bridge`
group drives headless Chromium (Playwright) to **decompose** each widget into
separate layers — the frame sprite *without* its text, icons peeled out, live text
kept as TMP — auto-detecting `<button>` components, computing 9-slices from CSS,
emitting transparent PNGs and standalone SVGs (with namespaces + CSS-var colors
resolved), then **reassembles** them into a GameObject tree in the Editor.
`html_to_unity` does the whole thing — design → built UI — in **one call**. The
upshot: AI can author UI in HTML (its native medium) and ship it as native uGUI.

### 7. A real in-Editor AI chat — on your subscription
A chat panel **inside Unity** (Window → Unimancer → Chat) drives the `claude` CLI
headless, so it runs on your **Claude subscription, not a pay-per-token API key**, and
inherits the full tool surface. It streams replies with inline **Edit/Write diffs**,
takes **screenshots** (capture / paste / drag) the agent can actually see, renders the
agent's questions as **clickable choices**, lets you **keep typing** (follow-ups queue
while it works), **survives Play mode** (optional) and recompiles (auto-resume), and
rides a **self-healing bridge**. No external editor or API key required.

---

## At a glance

| | Unimancer | Typical Editor-only MCP | Runtime-capable MCP |
|---|---|---|---|
| Android device control (adb/logcat/screenshot) | ✅ 23 tools | ❌ | ❌ |
| Android build (APK/AAB, keystore, AGP 9) | ✅ | partial | ❌ |
| Drive a *running* game | ✅ (Play + dev build + device) | ❌ | ✅ (no mobile) |
| Total tools | **106** | ~15–86 | ~20 |
| HTML mockup → native Unity UI pipeline | ✅ (Playwright decompose + build) | ❌ | ❌ |
| Batch / resources / notifications | ✅ all three | some | some |
| Image capture returned to the model | ✅ | some | rare |
| In-Editor AI chat (no API key, on subscription) | ✅ | ❌ | ❌ |
| Roslyn syntax validation | ✅ | some | ❌ |
| Runtime deps | **3** (pinned, guarded; `playwright` for the HTML bridge) | varies | varies |

---

## Honest limitations

- The C# side targets **Unity 6.5** specifically and is freshly battle-tested — not
  yet across many Unity versions.
- Android tools need a working `adb` / Android SDK on the host.
- Runtime tools require a Play session or a **Development** build (by design).
- It's young and community-built — no commercial support contract (yet).

If your work is **mobile**, spans **Editor + a live game**, or you want an agent that
batches, sees, and reacts — Unimancer is built for exactly that.

[depguard]: https://github.com/
