# `bridge/` — HTML → Unity-sprite bridge

Turn a self-contained **Claude Design HTML mockup** (HTML/CSS, *not* an image)
into reusable Unity UI **components**: each detected widget is decomposed into
separate layer assets (the frame *without* its text, icons separately, live text
kept as text) plus a `manifest.json` that the Unity side reassembles into a
proper GameObject tree.

The first two tools run **entirely in Node** via headless Chromium (Playwright) —
they do **not** touch the Unity Editor. The reassembly half lives in `ui/` as
`ui_build_from_manifest` (a Unity-bridge tool). `html_to_unity` is a **one-shot
orchestrator** that chains export → build so "take this HTML and build it in
Unity" is a single call.

| Tool | Runs in | Writes files? | Purpose |
|---|---|---|---|
| `html_inventory` | Node (Playwright) | no | **Dry run.** Classify export candidates and return a per-component layer tree (kind/format/geometry/9-slice/anchor) so you can preview + refine with `data-*` tags. |
| `html_export` | Node (Playwright) | yes | Decompose + **export**: per-element PNG/SVG layer assets + a `manifest.json` per component. |
| `html_to_unity` | Node (Playwright) + Unity | yes + GameObjects | **One-shot.** Runs `html_export`, then `ui_build_from_manifest` once per exported manifest. `dryRun:true` short-circuits to `html_inventory`. Per-component build failures are collected, not fatal. |
| `ui_build_from_manifest` (in `ui/`) | Unity (C# bridge) | creates GameObjects | Assemble the component from the manifest. |

### `html_to_unity` — how it chains

`{ htmlPath, parentPath?, outDir?, designWidth?, designHeight?, exportScale?, dryRun? }`

1. `dryRun:true` → returns `html_inventory`'s result (no files, no Unity).
2. Otherwise call `html_export.handler` with the geometry/scale args → parse its
   JSON result for the written `manifest.json` paths (one per component).
3. For **each** manifest, call `ui_build_from_manifest.handler` with the **same
   `ctx`** (so the build reaches the live Editor over TCP) and the shared
   `parentPath`. Build errors are caught per-component.
4. Returns `{ componentsExported, built: [{ manifest, result|error }], outDir }`.

It needs a **live Unity connection** for the build step (the export step does
not). The data dependency (export's manifest paths feed the build) is why this is
a dedicated tool and not a static `batch_execute`.

## Component-only

Each detected widget = one reusable component → its own folder of layer assets +
one `manifest.json`. There is **no whole-screen export mode**; you assemble the
full screen in Unity by hand from components.

## Component detection (auto-segmentation)

`decompose.js` finds component roots in priority order:

1. **Explicit `data-ui` tags win.** Each tagged element (except `data-ui="screen"`,
   which is looked *into*) is a component root.
2. **Zero-tag Claude Design HTML → auto-detect buttons + content panels:**
   - **Every visible `<button>`** becomes one component — an unambiguous, reusable
     widget boundary.
   - **Content-bearing panels** (info bars, resource counters) become components
     too. A panel qualifies only when it is visibly **styled** (background/border/
     shadow) **and carries real content** (≥4 text chars **or** an icon) **and** is a
     sensible size (≥48×20, under 70% of the page) **and** does **not** wrap a
     `<button>` (those buttons are the widgets, so a button row is skipped, not the
     panel). Only the **outermost** qualifying panel in a nest is kept. This admits
     real panels while rejecting Claude Design's decorative corner-bracket / accent
     shapes (styled but empty). Override anything with `data-ui` when inference is wrong.
3. **Fallback:** styled direct children of `<body>`, else `<body>` itself.

Component **names are unique across components** (each gets a distinct output
folder): a button is named from its label slug (e.g. `PLAY` → `play`), else the
archetype, with `_2`, `_3`… suffixes on collision. Layer names reset per component.

## Decomposition rules (Claude Design HTML lacks our tags)

`html_inventory` and `html_export` share one DOM walk (`decompose.js`). For each
element it infers:

- **kind**: `text` (leaf text → live TMP), `icon` (leaf `<img>` / inline `<svg>`),
  `sprite` (element with background/border/box-shadow that frames content),
  `group` (styleless container). A **styled box that holds only its own text** (a
  neon tab/button label) is treated as a `sprite` frame **with a text child**, not
  pure text — so the background is kept as a sprite and the label peeled out.
- **format**: `svg` for inline `<svg>` (written verbatim), else `png`.
- **geometry**: bounding box in **design px**, relative to the component root.
- **9-slice** (sprite/panel/button frames only — never icons/text): per side =
  `ceil(border-radius + border-width + max(0, outer box-shadow blur+spread))`,
  clamped so the center slice stays positive.
- **anchor**: full-bleed → `stretch`; pinned edge → that edge; centered →
  `center`; text honors `text-align`.

### Layer peeling & nested-frame flattening

A component is **one frame sprite + its text/icon layers**. When walking a
component subtree:

- **Text and icon** children are peeled into their own layers.
- **Own inline text** on a styled frame (a label sharing the frame's selector) is
  peeled into a live TMP child flagged `ownText`. The PNG export **dims it to
  transparent** during the frame screenshot (it can't be visibility-hidden
  without blanking the frame), so the label never bakes into the frame raster.
- **Nested styled frames/groups are flattened** — their own frame bakes into the
  parent raster and their text/icon leaves are hoisted up. This avoids spurious
  half-covered sub-sprites. Override per element with `data-layer="sprite"` to keep
  a nested frame as its own layer (e.g. a progress-bar fill).

### Asset export specifics (tested)

- **PNG frames** are screenshot per-element with sibling text/icon layers hidden,
  `omitBackground:true` (transparent alpha where the page is transparent),
  `animations:"disabled"` (finishes + freezes CSS animations/transitions so
  pulsing glows/beams don't fail Playwright's "element is stable" wait with a
  timeout), and `caret:"hide"`.
- **SVG icons** are written **verbatim** (never rasterized) with two fixes so they
  render standalone: the `xmlns` (and `xmlns:xlink` when needed) namespace is
  added (an inline `<svg>` inherits it from the HTML parser and omits it in
  `outerHTML`; a standalone `.svg` file needs it or importers treat it as plain
  XML), and every referenced CSS **custom property** (`var(--td-accent)`, glow
  `var(--td-glow)`, etc.) is **resolved from computed style and pinned inline** on
  the `<svg>` root — otherwise the page-level `:root` vars are gone and the icon
  renders colorless.

### Override tags (Claude adds these to refine)

| Attribute | Effect |
|---|---|
| `data-ui="component\|button\|panel\|image"` | Marks a **component root** + archetype. (`screen` is looked *into* for child components — never exported whole.) |
| `data-name` | Names the component / layer (else inferred). |
| `data-layer="sprite\|icon\|text\|skip"` | Forces a layer's kind; `skip` excludes it. |
| `data-format="png\|svg"` | Forces the export format. |
| `data-9slice="auto\|\"L T R B\"\|none"` | Overrides the computed 9-slice. |
| `data-anchor="center\|left\|right\|top\|bottom\|stretch"` | Overrides the inferred anchor. |

## Annotation workflow

1. **Inventory** the raw HTML: `html_inventory { htmlPath }`. No files written.
2. **Read the tree.** Where the inference is wrong (a frame mis-typed as a group,
   an icon that should be skipped, a missing 9-slice), add `data-*` tags to the
   HTML.
3. **Re-inventory** until the tree matches intent.
4. **Export**: `html_export { htmlPath, outDir?, exportScale? }` → writes
   `Assets/UI/<component>/<component>__<layer>.png|svg` + `manifest.json`.
5. In Unity, **build**: `ui_build_from_manifest { manifestPath }`.

Or skip the hand-off and do steps 4–5 in one call:
`html_to_unity { htmlPath, parentPath? }` (use `dryRun:true` for step 1's preview).

A *raw* Claude Design file with **no tags still exports** — it just relies fully
on inference. Tags only refine the result.

## Playwright dependency (pinned runtime dep)

The bridge needs Playwright **live at runtime**, so it is a pinned runtime
dependency: `playwright@1.61.0` (exact — no `^`/`~`). The npm dep ships the driver
but **not** a browser engine.

First-time setup:

```sh
guard install            # or npm install — pulls playwright@1.61.0
npx playwright install chromium   # the engine (must match build 1228 → pin 1.61.0)
```

On **Linux/WSL** Chromium also needs system libraries:

```sh
sudo apt-get install -y libnspr4 libnss3 libdbus-1-3 libatk1.0-0t64 \
  libatk-bridge2.0-0t64 libcups2t64 libdrm2 libxkbcommon0 libatspi2.0-0t64 \
  libxcomposite1 libxdamage1 libxfixes3 libxrandr2 libgbm1 libpango-1.0-0 \
  libcairo2 libasound2t64
# (or: sudo npx playwright install-deps chromium)
```

> **Guard cooldown note:** `playwright@1.61.0` was published recently, so depguard's
> 14-day cooldown may hide it (`guard install` fails with `ETARGET`). If so, install
> via `npm install --save-exact playwright@1.61.0` and run
> `guard approve playwright@1.61.0` (or `guard allow`) to satisfy the
> pre-commit/PR hook. **Do not downgrade** — the version must match the installed
> Chromium build (1228).

> **Unity note:** SVG layers import as Sprites only with **`com.unity.vectorgraphics`**
> installed; without it those layers degrade with a clear message (PNG layers
> always work).

## Manifest schema (per component)

```jsonc
{
  "meta": { "design": { "width": 1080, "height": 1920 }, "exportScale": 3, "source": "/abs/path.html" },
  "nodes": [
    {
      "name": "healthbar", "type": "sprite",
      "rect": { "x": 0, "y": 0, "w": 320, "h": 48 }, "anchor": "center",
      "kind": "sprite", "format": "png", "asset": "Assets/UI/healthbar/healthbar__frame.png",
      "nineSlice": [12, 12, 12, 12],
      "children": [
        { "name": "icon", "type": "icon", "kind": "icon", "format": "svg",
          "asset": "Assets/UI/healthbar/healthbar__icon.svg", "rect": {…}, "anchor": "left", "nineSlice": null },
        { "name": "label", "type": "text", "rect": {…}, "anchor": "left",
          "text": "100/100", "font": "Inter", "size": 24, "weight": "700", "color": "#FFFFFF", "align": "left" }
      ]
    }
  ]
}
```

Rects are **design px relative to the parent**. `html_export` is **idempotent** —
re-running overwrites the same paths.
