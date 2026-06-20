# `bridge/` — HTML → Unity-sprite bridge

Turn a self-contained **Claude Design HTML mockup** (HTML/CSS, *not* an image)
into reusable Unity UI **components**: each detected widget is decomposed into
separate layer assets (the frame *without* its text, icons separately, live text
kept as text) plus a `manifest.json` that the Unity side reassembles into a
proper GameObject tree.

These two tools run **entirely in Node** via headless Chromium (Playwright) — they
do **not** touch the Unity Editor. The reassembly half lives in `ui/` as
`ui_build_from_manifest` (a Unity-bridge tool).

| Tool | Runs in | Writes files? | Purpose |
|---|---|---|---|
| `html_inventory` | Node (Playwright) | no | **Dry run.** Classify export candidates and return a per-component layer tree (kind/format/geometry/9-slice/anchor) so you can preview + refine with `data-*` tags. |
| `html_export` | Node (Playwright) | yes | Decompose + **export**: per-element PNG/SVG layer assets + a `manifest.json` per component. |
| `ui_build_from_manifest` (in `ui/`) | Unity (C# bridge) | creates GameObjects | Assemble the component from the manifest. |

## Component-only

Each detected widget = one reusable component → its own folder of layer assets +
one `manifest.json`. There is **no whole-screen export mode**; you assemble the
full screen in Unity by hand from components.

## Decomposition rules (Claude Design HTML lacks our tags)

`html_inventory` and `html_export` share one DOM walk (`decompose.js`). For each
element it infers:

- **kind**: `text` (leaf text → live TMP), `icon` (leaf `<img>` / inline `<svg>`),
  `sprite` (element with background/border/box-shadow that frames content),
  `group` (styleless container).
- **format**: `svg` for inline `<svg>` (written verbatim), else `png`.
- **geometry**: bounding box in **design px**, relative to the component root.
- **9-slice** (sprite/panel/button frames only — never icons/text): per side =
  `ceil(border-radius + border-width + max(0, outer box-shadow blur+spread))`,
  clamped so the center slice stays positive.
- **anchor**: full-bleed → `stretch`; pinned edge → that edge; centered →
  `center`; text honors `text-align`.

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

A *raw* Claude Design file with **no tags still exports** — it just relies fully
on inference. Tags only refine the result.

## Playwright dependency (must be approved)

Playwright is **not** a pinned dependency yet (repo rule: no `npm install`
without approval). The tools ship complete with a **lazy** import: invoking them
without Playwright returns a clear, actionable error instead of crashing the MCP
server at startup.

To enable (with approval):

```sh
guard install playwright@1.56.0   # pin exactly; verify no ^/~ in package.json
npx playwright install chromium
```

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
