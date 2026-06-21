# Unimancer — code map

For contributors: where things live, what calls what, and how to add a tool.

## Layout

```
unimancer/
├── package.json              Node server manifest (deps pinned exact)
├── scripts/setup.mjs         zero-dep CLI: prints MCP client config + Unity steps
├── src/                      ── the Node/JS MCP server (stdio) ──
│   ├── index.js              entry: builds McpServer, ctx, registers allTools, serves
│   ├── core/
│   │   ├── types.js          ToolDefinition typedef + ok()/err() result helpers
│   │   ├── image.js          image() helper — return captures as MCP image blocks
│   │   ├── unityConnection.js TCP client to the Editor (request/response by id)
│   │   └── registry.js       registerTools(server, tools, ctx) — wraps each handler
│   └── tools/
│       ├── index.js          barrel: spreads every group into `allTools`
│       ├── adb/              Tier 1 — ADB device control (Node-only)
│       ├── androidBuild/     Tier 2 — Android build/config (Unity bridge)
│       ├── emulator/         Tier 3 — emulator + SDK env (Node-only)
│       ├── gameObject/       GameObjects & Components (Unity bridge)
│       ├── sceneAssets/      Scenes, Assets, Prefabs (Unity bridge)
│       ├── scripts/          Scripts, Materials, Shaders (Unity bridge)
│       ├── editor/           Editor/Console/Packages/Tests (Unity bridge)
│       ├── capture/          Visual capture → image results (Unity bridge)
│       ├── ui/               uGUI authoring: ui_create / rect_transform_set / ui_dump / ui_build_from_manifest (Unity bridge)
│       ├── sprites/          sprite_import / sprite_generate — TextureImporter + procedural PNGs (Unity bridge)
│       └── bridge/           HTML→Unity art pipeline (Playwright). Files:
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
│                               html_to_unity.js — one-shot orchestrator: html_export → ui_build_from_manifest per
│                                                manifest (export Node-only; build needs Unity)
└── unity/                    ── the Unity UPM package (C#) ──
    ├── package.json          UPM manifest (com.unimancer.mcp)
    └── Editor/
        ├── Core/
        │   ├── McpToolBase.cs base class: Name/Description/IsAsync/Execute/ExecuteAsync
        │   └── McpBridge.cs   [InitializeOnLoad] TCP server; reflects over McpToolBase
        ├── Setup/
        │   └── UnimancerSetupWindow.cs  Window → Unimancer → Setup (status + config)
        ├── Chat/              in-Editor chat that drives the local `claude` CLI (no API key)
        │   ├── ClaudeCliSession.cs    spawns `claude -p` stream-json, parses events
        │   ├── UnimancerChatWindow.cs Window → Unimancer → Chat (IMGUI panel)
        │   └── BridgePreviewWindow.cs HTML Preview popout: shows html_preview per-component crops
        └── Tools/             one C# class per engine-side tool (+ shared helpers)
```

## Call flow

```
AI client → (stdio) → src/index.js
  → registry.registerTools(): for each tool, server.registerTool(name, schema, wrap(handler))
  → tool.handler(args, ctx):
      • Node-only (adb/emulator/*) → shell out via core/adb.js, return ok()/err()
      • engine tool → ctx.unity.request(name, args)  (core/unityConnection.js)
            → TCP(JSON) → McpBridge.Dispatch() → Tools/<Name>Tool.Execute(JObject)
            → result JObject → back over WS → handler wraps as ok()/image()
```

Request/response is correlated by a monotonic `id`; engine `Execute` runs on the
Unity main thread (the bridge marshals it). Tools that span multiple frames
(package manager, test runner) set `IsAsync => true` and override `ExecuteAsync`.

## Shared C# helpers (`unity/Editor/Tools/`)

| File | Role |
|---|---|
| `GoResolve.cs` | resolve a `target` (hierarchy path or instanceID) → GameObject; build path |
| `ComponentTypeResolve.cs` | resolve a component type name across loaded assemblies |
| `PathGuard.cs` | reject paths with `..` / outside `Assets/` for file-writing tools |

## Where to make which change

| You want to… | Edit |
|---|---|
| add a Node-only tool (adb/emulator) | new file in the group + its `index.js` |
| add an engine tool | new JS file (group + `index.js`) **and** a `Tools/<Name>Tool.cs` (Name == JS name) |
| change the wire protocol | `core/unityConnection.js` **and** `Core/McpBridge.cs` (keep them in sync) |
| change result shapes | `core/types.js` / `core/image.js` |
| add a new tool group | new `src/tools/<group>/index.js` + import it in `src/tools/index.js` |
| compose other tools (orchestrator) | import their tool objects, call `tool.handler(args, ctx)` directly, pass the **same `ctx`** through (see `bridge/html_to_unity.js`) |

## Conventions

- Node = plain JS (ESM), JSDoc on every function. C# = `///` summaries.
- `inputSchema` is a Zod **raw shape** (`{ field: z.string() }`), not `z.object(...)`.
- Exact-pinned deps; supply-chain via `guard`. No tests unless asked.
