# Unimancer 🔮

**Unity now ships its own MCP server.** Unity's official CLI (`unity`) plus the
`com.unity.pipeline` package expose **~150 built-in tools** covering scenes,
GameObjects, components, assets, prefabs, scripts, animation, tests, build,
capture, and arbitrary C# (`eval`) — over `unity mcp`, stdio, driven straight
from the Editor. Unimancer no longer tries to be that bridge.

Unimancer is now a **thin companion**: a **21-tool** Node MCP server covering
what Unity's CLI doesn't — **Android device/emulator control**, the
**HTML→Unity art pipeline**, and **UGUI/sprite/component/animation/Android
authoring** contributed as pipeline commands. Point an AI client at both
servers together.

```
[ AI client ] --MCP/stdio--> unity mcp  ──> com.unity.pipeline (in Editor)
                             ~150 built-in tools: scenes, GameObjects, components,
                             assets, prefabs, scripts, tests, build, capture, eval…
                             + unimancer's 10 [CliCommand]s (ui_*, sprite_*, gameobjects, animation, android)

[ AI client ] --MCP/stdio--> unimancer (Node, 21 tools)
                             |-- adb / emulator   (Android devices, no Unity needed)
                             |-- Playwright       (HTML -> Unity art bridge)
                             `-- `unity command`  (only for the ui_build_from_manifest step)
```

- **`src/`** — the Node/JS MCP server (stdio): `adb` (12), `emulator` (5), `bridge` (4).
  No TCP client, no reflection — the only thing it shells out to Unity for is
  `unity command ui_build_from_manifest` at the end of `html_to_unity`.
- **`unity/`** — the Unity UPM package (C#). No longer a bridge: it's the
  in-Editor **Claude chat window** plus **ten `[CliCommand]` static methods**
  (`ui_*`, `sprite_*`, `component_list`, `animator_set_parameter`,
  `android_player_settings`, `ui_click`) that `com.unity.pipeline` discovers
  and `unity mcp` surfaces automatically alongside its ~150 built-ins.

Targets **Unity 6000.5 (6.5)+** with `com.unity.pipeline` **0.6.0-exp.1** /
Unity CLI **1.0.0-beta.8**, and **Android Gradle Plugin 9.0** defaults (now
Unity's own build tools' concern, not Unimancer's).

## What moved where

Every removed unimancer tool group is now a **built-in `unity mcp` command**
(real names below — see [USAGE.md §3](docs/USAGE.md#3-tool-groups) for the full
21 that remain).

| Removed unimancer group | Now covered by (`unity mcp` built-ins) |
|---|---|
| `gameObject` | `create_gameobject(s)`, `find_gameobjects`, `add_component`, `remove_component`, `get_component_properties`, `set_component_properties`, `set_transform`, `set_parent`, `set_active`, `set_layer`, `set_tag`, `rename_gameobject`, `delete_gameobject` — **except** listing a GameObject's components, which is kept as unimancer's own `component_list` `[CliCommand]` (lighter than `get_component_properties`: types/enabled/instanceID, no serialized-property dump) |
| `sceneAssets` | scenes: `create_scene`, `open_scene`, `save_scene`, `save_all`, `list_open_scenes`, `set_active_scene`, `get_scene_hierarchy`, `add_scene_to_build`, `remove_scene_from_build`; assets: `create_asset`, `create_folder`, `copy_asset`, `move_asset`, `rename_asset`, `delete_asset`, `find_assets`, `import_asset`, `get/set_import_settings`; prefabs: `create_prefab`, `create_prefab_variant`, `instantiate_prefab`, `apply_prefab_overrides`, `revert_prefab_overrides`, `unpack_prefab`, `save_prefab_contents` |
| `scripts` / `scriptEdit` | `create_script`, `attach_script`, `read_text_file`, `write_text_file`, `recompile`, `recompile_status`, `get_serialized_fields`, `set_serialized_field`, `eval`, `eval_file`, `run_script`, `reload_file*` (hot reload), plus materials: `get/set_material_properties`, `get_shader_properties`, `list_shaders` |
| `editor` | `editor_status`, `editor_play`, `editor_stop`, `editor_pause`, `editor_focus`, `menu`, `run_tests`, `list_tests`, `test_status`, `cancel_tests`, `package_add/remove/list/search/resolve/status`, `get_console_logs`, `clear_console`, `console`, `get/set_selection`, `search` |
| `capture` | `capture_game_view`, `capture_scene_view`, `screenshot` |
| `animation` | `add_animator_layer/parameter/state/transition`, `create/get_animator_controller`, `create/get_animation_clip`, `set/remove_animation_curve`, `create/get_timeline`, `add_timeline_track/clip` — **except** driving an Animator parameter at runtime, kept as unimancer's own `animator_set_parameter` `[CliCommand]` |
| `navmesh` (+ lighting) | `bake_navmesh`, `navmesh_bake_status`, `cancel_navmesh_bake`, `clear_navmesh`, `bake_navmesh_surfaces`, `get/set_navmesh_settings`, `bake_lighting`, `lighting_bake_status`, `cancel_lighting_bake`, `clear_baked_lighting`, `get/set_lighting_settings`, `bake_occlusion_culling`, `occlusion_bake_status`, `cancel_occlusion_bake`, `clear_occlusion_culling` |
| `profiler` | `get_performance_stats`, `audit`, `audit_status`, `report_evals` |
| `androidBuild` | `build`, `build_status`, `get/set_build_settings`, `list_build_targets`, `switch_build_target(_status)`, `list_build_profiles`, `get/set_player_settings`, plus quality/graphics/physics/audio/input/tags_layers/time settings — **except** Android-specific player settings (application id, SDK levels, architectures, scripting backend, keystore name/alias), kept as unimancer's own `android_player_settings` `[CliCommand]` (get-or-set; never accepts or returns keystore/alias **passwords** — those stay on `unity build --android-keystore-*`) |
| `runtime` | the Player-side commands: `eval`, `simulate_pointer`, `simulate_key`, `set_timescale`, `console` (same tags work against a live Player, not just the Editor) |
| `batch` | `batch` — same idea (transactional multi-op with `$N.path` refs), now a built-in |

`ui` and `sprites` are **not** replaced — they didn't exist upstream, so
unimancer contributes them back as pipeline commands (see below), including
`ui_click` (simulated pointer click through `ExecuteEvents`/`Selectable`,
kept as a unimancer `[CliCommand]` since Unity's built-ins don't drive UGUI
input events).

## Quick start

1. **Install the Unity CLI** (skip if `unity --version` already works):
   ```bash
   # macOS / Linux
   curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
   ```
   ```powershell
   # Windows (PowerShell)
   $env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
   ```
   Open a new shell so `unity` is on PATH, then `unity --version`.
2. **Install the pipeline package into your project**:
   ```bash
   unity pipeline install --project-path /path/to/YourProject
   ```
3. **Add the Unimancer UPM package** (contributes the `ui_*`/`sprite_*` commands
   and the in-Editor chat window) — Package Manager → **Add package from git URL**
   → `https://github.com/VolcanicMG/Unimancer.git?path=/unity`, or **Add package
   from disk** → `<clone>/unity/package.json` on the same OS as Unity.
4. **Configure your MCP client**:
   ```bash
   unity mcp configure claude-code --project-path /path/to/YourProject
   ```
   This registers the `unity` server. Add the `unimancer` Node server manually
   (`claude mcp add unimancer -- node /abs/path/to/unimancer/src/index.js`) so
   the client has both:
   ```json
   {
     "mcpServers": {
       "unity": { "command": "unity", "args": ["mcp", "--project-path", "/path/to/YourProject"] },
       "unimancer": { "command": "node", "args": ["/abs/path/to/unimancer/src/index.js"] }
     }
   }
   ```
5. **Install server deps** (through the supply-chain guard):
   ```bash
   guard install      # or: npm install
   ```
   The **HTML→Unity bridge** uses [Playwright](https://playwright.dev) (pinned
   `playwright@1.61.0`) to rasterize/decompose mockups headlessly. The npm dep
   ships the driver but **not** a browser engine — install Chromium once, and on
   Linux/WSL its system libraries too:
   ```bash
   npx playwright install chromium
   # Linux/WSL system libs Chromium needs at runtime:
   sudo apt-get install -y libnspr4 libnss3 libdbus-1-3 libatk1.0-0t64 \
     libatk-bridge2.0-0t64 libcups2t64 libdrm2 libxkbcommon0 libatspi2.0-0t64 \
     libxcomposite1 libxdamage1 libxfixes3 libxrandr2 libgbm1 libpango-1.0-0 \
     libcairo2 libasound2t64
   # (or, all at once: sudo npx playwright install-deps chromium)
   ```
   SVG icons exported by the bridge import into Unity as Sprites only with the
   **`com.unity.vectorgraphics`** package installed (otherwise SVG layers degrade
   with a clear message; PNG layers always work).

> **WSL note:** when Unity runs on Windows and your agent runs in WSL, run the
> Windows `unity.exe` through a small shim on the WSL PATH (a one-line script
> that `exec`s the Windows binary) — the Unity CLI has no native Linux build,
> so `unity mcp` is really driving the Windows Editor process. The Node
> `unimancer` server runs natively in WSL; only the `unity` calls it shells out
> to (`ui_build_from_manifest`) cross into Windows via the shim.
>
> Developing the C# package from WSL: Unity compiles an *embedded copy* under
> the project's `Packages/`. `./scripts/dev-sync.sh --once --delete` mirrors
> `unity/` into that copy and asks the Editor to recompile through the CLI
> (`--delete` removes files you deleted here; omit `--once` to keep polling).
> Or skip the copy: keep the repo on the Windows filesystem and reference it as
> `"file:C:/path/to/unimancer/unity"` in `Packages/manifest.json`.

## In-Editor chat — no API key 🆕

A chat panel **inside Unity** (**Window → Unimancer → Chat**) that drives your local
`claude` CLI in headless streaming mode. It runs on your **Claude subscription, not a
pay-per-token API key**, and registers an mcp-config with **both** MCP servers —
`unity` (`unity mcp --project-path <project>`) and `unimancer` (this Node server) —
with `allowedTools` set to `mcp__unity,mcp__unimancer`.

- Needs the `claude` CLI installed & logged in on the machine. **WSL is fine** — the
  window WSL-wraps the spawn (toggle in **Setup**). Keep `ANTHROPIC_API_KEY` unset so
  Claude Code uses your subscription.
- **Multi-turn & resumable** — continues via `--resume`, reopens the last chat and
  resumes after a recompile. Optional **"Keep chat alive in Play mode"** (Settings)
  skips the domain reload so entering Play doesn't interrupt a turn.
- **Keep typing while it streams** — Send queues follow-ups that auto-send when the turn
  ends; tool calls show inline with their target, and Edit/Write show a red/green **diff**
  like the normal console. A live **"Working… / Worked for Ns"** timer shows turn time.
- **Reference & see your project** — drag GameObjects/assets in, **@ Reference**, or
  **Use selection** (auto-syncable); the agent gets the live scene/selection as context
  and renders clickable `[[unity:…]]` handles back.
- **Screenshots** — attach via Capture (Game/Scene view), file picker, image drag, or
  **Ctrl/Cmd+V**; the agent views them with the Read tool.
- **Choices as buttons** — when the agent offers options it renders clickable buttons
  (you can still type a free reply).

## Docs

- [USAGE.md](docs/USAGE.md) — setup, tool groups, resources, notifications, troubleshooting
- [PITCH.md](docs/PITCH.md) — why Unimancer alongside Unity's own MCP
- [docs/CODEMAP.md](docs/CODEMAP.md) — architecture & how to add a tool

## Tools (21)

| Group | Count | Needs Unity? | Examples |
|---|---|---|---|
| ADB device control | 12 | no (`adb`) | install, launch, logcat, screenshot, push/pull |
| Emulator & SDK | 5 | no | list/start/stop AVDs, sdk_check |
| HTML→Unity bridge | 4 | export/preview: no (Playwright); build: yes (`unity command`) | inventory (dry-run), **per-layer preview** (`html_preview` — full crop + every separated layer per component: frame, sub-sprites (progress fills, plates), icons — each isolated as a shape-accurate transparent sprite (no black corners) with its own 9-slice or none; shown in a dedicated popout; temp crops auto-clean), export a Claude Design HTML mockup into per-component layer assets + a manifest, and `html_to_unity` (one-shot export **and** build via `unity command ui_build_from_manifest`) |

Unity's own `unity mcp` adds **~150 built-in tools** plus unimancer's 10
contributed `[CliCommand]`s (`ui_create`, `rect_transform_set`, `ui_dump`,
`ui_build_from_manifest`, `ui_click`, `sprite_import`, `sprite_generate`,
`component_list`, `animator_set_parameter`, `android_player_settings`) — see
[what moved where](#what-moved-where) above.

## Environment variables

| Var | Default | Purpose |
|---|---|---|
| `UNITY_CLI` | `unity` | path/name of the Unity CLI binary the Node server shells out to |
| `UNITY_PROJECT_PATH` | — | project passed to `unity command` when a tool doesn't specify one |
| `ADB_PATH` | `adb` | path to the adb binary |
| `EMULATOR_PATH` | `emulator` | path to the Android emulator binary |

## Supply-chain safety

Guarded by [depguard](https://github.com/) (`guard`). Use `guard install` instead
of `npm install`; commits/pushes run `guard check` automatically.

> **Dependency note:** `@modelcontextprotocol/sdk` is at **1.30.0** (its earlier
> HIGH advisories, `GHSA-345p` and `GHSA-8r9q`, are fixed there and the `hono`
> subtree it pulls is currently advisory-free). `fast-uri` is pinned to 3.1.6 via
> `overrides` until `ajv` bumps it. Two moderate `qs` advisories remain until
> 6.16.0 clears the 14-day cooldown; they sit in the SDK's HTTP transport, which
> Unimancer (stdio-only) never uses.

> **Playwright note:** the HTML→Unity bridge pins **`playwright@1.61.0`** (exact).
> It was published recently, so depguard's 14-day cooldown may hide it — if
> `guard install` fails with `ETARGET`, install via
> `npm install --save-exact playwright@1.61.0` and run `guard approve
> playwright@1.61.0` (or `guard allow`) to satisfy the pre-commit/PR hook. **Do
> not downgrade** — the version must match the installed Chromium build (1228).

## Adding a tool

Two paths now — see [`docs/CODEMAP.md`](docs/CODEMAP.md) for details:

- **A pipeline command** (engine-side, e.g. more UGUI/sprite authoring): a
  `[CliCommand]` static method in `unity/Editor/Commands/` — `unity mcp`
  discovers and surfaces it automatically, no Node changes.
- **A Node tool** (genuinely Node-side work — shelling out, Playwright, adb):
  one JS file exporting `{ name, description, inputSchema, handler }` in
  `src/tools/<group>/`, added to that group's `index.js`.
