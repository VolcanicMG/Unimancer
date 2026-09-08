# Unimancer — code map

For contributors: where things live, what calls what, and how to add a tool.

## Layout

```
unimancer/
├── package.json              Node server manifest (deps pinned exact)
├── scripts/setup.mjs         zero-dep CLI: prints MCP client config + Unity steps
├── src/                      ── the Node/JS MCP server (stdio, 21 tools) ──
│   ├── index.js              entry: builds McpServer, ctx, registers allTools, serves
│   ├── core/
│   │   ├── types.js          ToolDefinition typedef + ok()/err() result helpers
│   │   ├── adb.js            adb/emulator shell-out helpers
│   │   ├── unityCli.js       unityCommand(name, args, {projectPath}) — shells out to
│   │   │                     `unity command <name> --format json --no-banner
│   │   │                     --non-interactive [--key value ...]`. The ONLY place the
│   │   │                     Node server talks to Unity (only html_to_unity's build
│   │   │                     step uses it, to call ui_build_from_manifest)
│   │   └── registry.js       registerTools(server, tools, ctx) — wraps each handler
│   └── tools/
│       ├── index.js          barrel: spreads every group into `allTools`
│       ├── adb/              Android device control (Node-only, 12 tools)
│       ├── emulator/         AVD lifecycle + SDK env (Node-only, 5 tools)
│       └── bridge/           HTML→Unity art pipeline (Playwright, 4 tools). Files:
│                               decompose.js   — shared browser-side DOM walk: component detection
│                                                (data-ui → <button> auto-segment → body fallback) + layer
│                                                classify (kind/format[glowing inline-svg→png]/9-slice/anchor); peels EVERY visual layer
│                                                (text/icon/nested sprite), flattens only unstyled groups; own-text peel
│                               playwright.js  — lazy withPage() launcher (Chromium, deviceScaleFactor/viewport)
│                               layers.js      — shared captureLayerPng(): isolate a layer (hide content children + siblings,
│                                                clear ancestor backdrops, keep inset borders, drop outset rings) → transparent
│                                                shape-accurate crop; pad ICONS by their drop-shadow glow (frames stay unpadded so
│                                                their 9-slice stays aligned); drop near-empty captures via a zlib PNG-alpha check
│                               htmlInventory.js — html_inventory: dry-run decompose, no files (Node-only)
│                               htmlPreview.js — html_preview: full crop + ONE crop per separated layer (frame, sub-sprites,
│                                                icons) rasterized in isolation via layers.js, 9-slice drawn on sprites that
│                                                have one — inline OR outDir + preview-index.json for the popout (Node-only)
│                               htmlExport.js  — html_export: write per-component PNG/SVG layers + manifest.json
│                                                (PNG via captureLayerPng → transparent shape + glow; SVG xmlns + CSS-var resolve)
│                               html_to_unity.js — one-shot orchestrator: html_export → `unityCommand("ui_build_from_manifest", …)`
│                                                per manifest (export Node-only; build needs Unity via the CLI)
└── unity/                    ── the Unity UPM package (C#) — no longer a bridge ──
    ├── package.json          UPM manifest (com.unimancer.mcp), depends on com.unity.pipeline 0.6.0-exp.1
    └── Editor/
        ├── Setup/
        │   └── UnimancerSetupWindow.cs  Window → Unimancer → Setup (status + config)
        ├── Chat/              in-Editor chat that drives the local `claude` CLI (no API key);
        │                      writes an mcp-config registering BOTH `unity` and `unimancer` servers
        │   ├── ClaudeCliSession.cs    spawns `claude -p` stream-json, parses events
        │   ├── UnimancerChatWindow.cs Window → Unimancer → Chat (IMGUI panel)
        │   └── BridgePreviewWindow.cs HTML Preview popout: shows html_preview per-component crops
        └── Commands/          the ONLY tool-registration surface left in C# — ten
                                [CliCommand] static methods, discovered by com.unity.pipeline
                                and surfaced through `unity mcp` alongside its ~150 built-ins
            ├── UiCommands.cs        tag "ui": ui_create, rect_transform_set, ui_dump, ui_build_from_manifest, ui_click
            ├── SpriteCommands.cs    tag "sprites": sprite_import, sprite_generate
            ├── GameObjectCommands.cs tag "gameobjects": component_list (lists components, no serialized-property dump)
            ├── AnimationCommands.cs  tag "animation": animator_set_parameter (float/int/bool/Trigger, Play-Mode caveat)
            ├── AndroidCommands.cs    tag "android": android_player_settings (get-or-set; never touches keystore passwords)
            └── GoResolve.cs, CommandInputs.cs   shared helpers (resolve a target, parse structured args)
```

**Deleted** (now covered by `com.unity.pipeline` / `unity mcp`, see the README's
[what-moved-where table](../README.md#what-moved-where)): the old Unity TCP
client module, MCP resources (`src/core/resources.js`), the Unity-connection
field on the tool context, the tool groups `androidBuild`, `animation`,
`batch`, `capture`, `editor`, `gameObject`, `navmesh`, `profiler`, `runtime`,
`sceneAssets`, `scriptEdit`, `scripts`; and on the C# side the old Editor-side
and in-Player TCP servers, the whole `Runtime/` folder, and every
`Editor/Tools/*` file covered by a built-in pipeline command.

## Call flow

```
AI client → (stdio) → src/index.js
  → registry.registerTools(): for each tool, server.registerTool(name, schema, wrap(handler))
  → tool.handler(args, ctx):
      • adb/emulator → shell out via core/adb.js, return ok()/err()
      • bridge (html_inventory/html_preview/html_export) → Playwright, Node-only
      • bridge (html_to_unity) → html_export, then core/unityCli.js's unityCommand()
            → shells out `unity command ui_build_from_manifest --format json …`
            → com.unity.pipeline's server (in the Editor) dispatches to UiCommands.cs
            → JSON result parsed back into the tool's ok()/err()
```

There's no persistent connection and no request/response correlation to
maintain on the Node side anymore — each `unityCommand()` call is one
`execFile` round-trip to the `unity` CLI, which owns talking to the Editor.

## Where to make which change

| You want to… | Edit |
|---|---|
| add a Node-only tool (adb/emulator/bridge) | new file in the group + its `index.js` |
| add an engine-side command (more UGUI/sprite authoring) | a new `[CliCommand]` static method in `unity/Editor/Commands/` — `unity mcp` picks it up automatically, no Node changes needed |
| change how the Node server calls Unity | `src/core/unityCli.js` only (it's the sole caller) |
| change result shapes | `core/types.js` |
| add a new Node tool group | new `src/tools/<group>/index.js` + import it in `src/tools/index.js` |
| compose other tools (orchestrator) | import their tool objects, call `tool.handler(args, ctx)` directly, pass the **same `ctx`** through (see `bridge/html_to_unity.js`) |

## How to add a tool (two paths)

1. **`[CliCommand]` in `unity/Editor/Commands/`** — for anything engine-side.
   `com.unity.pipeline` discovers it by attribute and `unity mcp` surfaces it
   as a tool automatically; no registration, no Node-side change. Use this for
   more UGUI/sprite work or any other Editor-state authoring.
2. **A Node tool in `src/tools/<group>/`** — only for work that's genuinely
   Node-side: shelling out to another CLI, browser automation (Playwright),
   or `adb`/emulator control. If the work needs to touch a live Editor, it
   belongs in path 1 instead — don't grow `unityCli.js` into a second bridge.

## Conventions

- Node = plain JS (ESM), JSDoc on every function. C# = `///` summaries.
- `inputSchema` is a Zod **raw shape** (`{ field: z.string() }`), not `z.object(...)`.
- Exact-pinned deps; supply-chain via `guard`. No tests unless asked.
