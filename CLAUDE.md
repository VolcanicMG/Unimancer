# Unimancer — Claude instructions

Companion to the official **Unity CLI** (`unity mcp` + `com.unity.pipeline`):
a small Node MCP server for what the CLI lacks (**Android** device/emulator
tooling, the HTML→Unity art bridge) plus a UPM package contributing
`[CliCommand]`s (UGUI/sprite/component/animation/Android authoring) that
surface through `unity mcp`.
GitHub: `VolcanicMG/Unimancer`. Parent rules in `~/repos/CLAUDE.md` still apply.

```
[ AI client ] --MCP/stdio--> [ unity mcp (Unity CLI) ] --HTTP+token--> [ Editor: com.unity.pipeline (~150 built-ins)
                                                                         + unity/ package: 10 [CliCommand]s (ui_*, sprite_*, component_list, animator_set_parameter, android_player_settings) ]
[ AI client ] --MCP/stdio--> [ server: src/ (Node, 21 tools) ] --shell--> adb / emulator / Playwright
                                   |--spawn `unity command …`--> (only html_to_unity's build step)
```

## 📌 Keep the docs up to date — every change, unprompted

**This is the standing mandate for this repo.** When you change code, update the
docs **in the same change** — do not ask first, do not defer it.
(This overrides the ask-first "living documentation" rule in `~/repos/CLAUDE.md`
*for the docs listed below only*.)

| Doc | Keep in sync with |
|-----|-------------------|
| `README.md` | tool count, architecture diagram, quick-start, Unity/AGP versions |
| `docs/CODEMAP.md` | `src/` layout, what-calls-what, **how to add a tool** |
| `docs/USAGE.md` | tool count, tool groups, resources, event notifications |
| `docs/PITCH.md` | positioning / differentiators (Android, runtime control) |

**Checklist before you finish any change:**

1. **Tool count** — the canonical number is the live count:
   `grep -rohE "name:\s*['\"][a-z_]+['\"]" src/tools | wc -l`.
   If you add/remove a tool, update **every** doc that states a count
   (README.md *and* docs/USAGE.md — they have drifted before).
2. **New tool group / dir under `src/tools/`** -> update `docs/CODEMAP.md` layout
   and `docs/USAGE.md` group list.
3. **New env var, port, or CLI flag** -> README quick-start + relevant doc.
4. **Version bumps** (Unity, AGP, Node, deps) -> README + `package.json` callouts.
5. Grep for the **old** value when you change a stated fact, so no stale copy
   survives: `grep -rn "<old value>" README.md docs/`.

If a doc and the code disagree, **the code wins** — fix the doc.

## Project facts

- **`src/`** — Node MCP server (stdio). Node-only tools (`adb`, `emulator`,
  `bridge`) in `src/tools/<group>/`; `src/core/unityCli.js` shells out to
  `unity command` when a tool must reach the Editor. No TCP client anymore.
- **`unity/`** — Unity UPM package (C#). `Editor/Commands/` holds static
  `[CliCommand]` methods discovered by `com.unity.pipeline`; everything that a
  built-in pipeline command covers was deleted — check
  `unity command --format json` before adding a tool.
- Targets **Unity 6000.5 (6.5)** / **Android Gradle Plugin 9.0** defaults.
- **21 Node tools** across 3 groups (canonical count = the grep above) + 10 C# `[CliCommand]`s.
- Deps pinned exact: `@modelcontextprotocol/sdk` 1.32.2, `zod` 4.6.7, exact `overrides` for
  transitives (fast-uri, body-parser, proxy-addr, source-map-js, vite, hono, qs, …). Node `>=20`. ESM (`"type": "module"`).
- Supply-chain guarded — use `guard install` / `guard ci` (`.guardrc` present).
- Tests: `vitest` (`npm test`). Per repo rules, **no tests unless asked**.

## Conventions

- Pin exact dependency versions (no `^`/`~`/`latest`).
- JSDoc on functions/methods; comment the *why*.
- Ask before any git action (commit / push / PR).
- Never print env-var **values** in chat — names only.
