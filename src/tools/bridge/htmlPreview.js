/**
 * Tool: `html_preview` — PER-COMPONENT, PER-LAYER visual preview for approval.
 *
 * Same parse + classification as `html_inventory` (shared `decompose.js`), but it
 * rasterizes each component AND every separated layer inside it, so the user can see
 * exactly how a widget decomposes BEFORE any export/build:
 *
 *   - FULL  — the component as Chromium renders it (frame + every element), and
 *   - one image PER visual layer — the frame on its own, plus each peeled sub-sprite
 *     (progress-bar fill, inset plate, badge…) and each icon, rasterized in
 *     ISOLATION (its own descendants hidden, exactly what `html_export` writes), with
 *     the 9-slice guide lines (magenta L/T/R/B) drawn on a sprite layer that has one.
 *     Layers WITHOUT a 9-slice (most icons, simple bars) are shown plainly.
 *
 * This is what makes the decomposition reviewable: nothing is baked into the frame —
 * every icon / sub-element is its own correctly-scaled sprite. Component & layer NAMES
 * are returned as data (not baked into the images) so the viewer labels each crop.
 *
 * Delivery modes:
 *  - inline:true  (default) → returns all crops as inline image blocks.
 *  - inline:false           → returns ONLY a text summary and writes files to outDir
 *      (used by the Unity "HTML Preview" popout so the images don't flood the chat).
 *
 * When `outDir` is set it writes `<component>.png` (full) + `<component>__<layer>.png`
 * (each layer) per component PLUS a `preview-index.json` the popout reads.
 *
 * Runs entirely in Node via Playwright — it does NOT touch the Unity Editor.
 */
import { z } from "zod";
import { ok, okImages, err } from "../../core/types.js";
import { pathToFileURL } from "node:url";
import { resolve, join, isAbsolute } from "node:path";
import { stat, mkdir, writeFile } from "node:fs/promises";
import { withPage } from "./playwright.js";
import { decomposePage } from "./decompose.js";
import { captureLayerPng } from "./layers.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const htmlPreview = {
  name: "html_preview",
  description:
    "VISUAL PREVIEW for up-front approval: render a Claude Design HTML mockup and produce, PER detected component, a FULL crop " +
    "(frame + every element as rendered) plus ONE crop per separated LAYER — the frame alone, each peeled sub-sprite (progress-bar fill, " +
    "inset plate, badge) and each icon, rasterized in isolation (exactly what html_export writes), with 9-slice guide lines drawn on any " +
    "sprite layer that has one (layers without a 9-slice are shown plainly). Names are returned as data, not baked into the images. " +
    "inline:true (default) returns the crops inline; inline:false returns only a text summary and writes files to outDir (for the Unity popout). " +
    "When outDir is set it writes '<component>.png' + '<component>__<layer>.png' each plus 'preview-index.json'. Requires Playwright (a pinned dep).",
  inputSchema: {
    htmlPath: z
      .string()
      .describe("Path to the self-contained HTML mockup. Accepts a Windows path (C:\\\\… or C:/…); it is mapped to the WSL mount."),
    designWidth: z.number().positive().optional().describe("Normalize geometry to this design width in px (defaults to the document layout width)."),
    designHeight: z.number().positive().optional().describe("Normalize geometry to this design height in px (defaults to the document layout height)."),
    outDir: z.string().optional().describe("Directory to write the full + per-layer crops + 'preview-index.json' to. Required when inline:false."),
    inline: z
      .boolean()
      .optional()
      .describe("Return crops as inline images (default true). Set false to return only a text summary and write files to outDir (used by the Unity popout)."),
  },
  /**
   * @param {{htmlPath:string, designWidth?:number, designHeight?:number, outDir?:string, inline?:boolean}} args - preview options.
   * @param {import("../../core/types.js").ToolContext} _ctx - unused (no Unity round-trip).
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    try {
      const inline = args.inline !== false; // default true
      if (!inline && !args.outDir) return err("inline:false requires outDir (nowhere to write the crops).");

      const htmlPath = await resolveHtmlPath(args.htmlPath);
      if (!htmlPath) return err(`HTML file not found: ${args.htmlPath}`);

      const url = pathToFileURL(htmlPath).href;
      const viewport =
        args.designWidth && args.designHeight
          ? { width: Math.round(args.designWidth), height: Math.round(args.designHeight) }
          : undefined;

      const parts = await withPage(
        async (page) => {
          await page.goto(url, { waitUntil: "networkidle" });
          const decomp = await decomposePage(page, args.designWidth, args.designHeight);

          const out = [];
          for (const comp of decomp.components) {
            // (1) FULL component as rendered (frame + every element), no overlay.
            const fullHandle = await page.$(comp.node.selector);
            if (!fullHandle) continue;
            const fullBuf = await fullHandle.screenshot({ animations: "disabled", caret: "hide" });

            // (2) One crop per visual layer (sprite|icon), rasterized in isolation.
            const layers = [];
            for (const ln of collectVisualLayers(comp.node, [])) {
              const drawOverlay =
                ln.kind === "sprite" && Array.isArray(ln.nineSlice)
                  ? async () => { await page.evaluate(DRAW_NODE_FN, { node: ln, design: decomp.design }); }
                  : undefined;
              const buf = await captureLayerPng(page, ln, { drawOverlay });
              if (buf) layers.push({ name: ln.name, kind: ln.kind, border: Array.isArray(ln.nineSlice) ? ln.nineSlice : null, buf });
            }

            out.push({
              name: comp.name,
              archetype: comp.archetype,
              texts: countKind(comp.node, "text"),
              icons: countKind(comp.node, "icon"),
              full: fullBuf,
              layers,
            });
          }
          return { design: decomp.design, components: out };
        },
        { viewport, deviceScaleFactor: 2 } // 2x for crisp crops
      );

      if (parts.components.length === 0) {
        return err("No components were detected in the HTML — nothing to preview.");
      }

      // Persist full + per-layer crops + an index the popout reads, when a dir is given.
      let savedNote = "";
      if (args.outDir) {
        const dir = isAbsolute(args.outDir) ? args.outDir : resolve(args.outDir);
        await mkdir(dir, { recursive: true });
        for (const c of parts.components) {
          await writeFile(join(dir, `${c.name}.png`), c.full);
          for (const l of c.layers) await writeFile(join(dir, `${c.name}__${l.name}.png`), l.buf);
        }
        const index = {
          source: htmlPath,
          design: parts.design,
          components: parts.components.map((c) => ({
            name: c.name,
            archetype: c.archetype,
            full: `${c.name}.png`,
            texts: c.texts,
            icons: c.icons,
            layers: c.layers.map((l) => ({
              name: l.name,
              kind: l.kind,
              border: l.border, // [L,T,R,B] design px, or null (no 9-slice)
              file: `${c.name}__${l.name}.png`,
            })),
          })),
        };
        // Write the index LAST so a watcher only sees it once every crop exists.
        await writeFile(join(dir, "preview-index.json"), JSON.stringify(index, null, 2), "utf8");
        savedNote = `\nWrote ${parts.components.length} component(s) + their layer crops + preview-index.json to: ${dir}`;
      }

      const caption =
        `Per-layer preview — ${parts.components.length} component(s) at ` +
        `${parts.design.width}×${parts.design.height} design px. Each component shows a FULL crop plus every separated ` +
        `layer (frame, sub-sprites, icons) in isolation; magenta lines = 9-slice (layers without one are shown plainly).\n` +
        parts.components
          .map((c) => `- ${c.name} (${c.archetype}): ${c.layers.length} layer(s) [${c.layers.map((l) => `${l.name}:${l.kind}${l.border ? "+9s" : ""}`).join(", ")}], ${c.texts} text`)
          .join("\n") +
        savedNote;

      // inline:false → text only (the popout displays the files); else inline images
      // (full then each layer, per component).
      if (!inline) return ok(caption);
      const images = [];
      for (const c of parts.components) {
        images.push({ base64: c.full.toString("base64"), mimeType: "image/png" });
        for (const l of c.layers) images.push({ base64: l.buf.toString("base64"), mimeType: "image/png" });
      }
      return okImages(images, caption);
    } catch (e) {
      return err(e.message);
    }
  },
};

/**
 * Collect every VISUAL layer (sprite|icon) in a component subtree, root first,
 * depth-first — the layers that get their own rasterized crop.
 *
 * @param {object} node - a layer node.
 * @param {object[]} acc - accumulator.
 * @returns {object[]} the visual layer nodes in render order.
 */
function collectVisualLayers(node, acc) {
  if (node && (node.kind === "sprite" || node.kind === "icon")) acc.push(node);
  if (node && node.children) for (const c of node.children) collectVisualLayers(c, acc);
  return acc;
}

/**
 * Count layers of a given kind in a component subtree (e.g. how many text/icon
 * layers were peeled out of the frame).
 *
 * @param {object} node - a layer node.
 * @param {string} kind - "text" | "icon" | "sprite" | "group".
 * @returns {number} the count.
 */
function countKind(node, kind) {
  let n = node && node.kind === kind ? 1 : 0;
  if (node && node.children) for (const c of node.children) n += countKind(c, kind);
  return n;
}

/**
 * Resolve the HTML path, accepting a Windows path (C:\… / C:/…) by also trying its
 * WSL mount form (/mnt/c/…). Returns the first candidate that exists, else null.
 *
 * @param {string} input - the user/agent-supplied path.
 * @returns {Promise<string|null>} an existing absolute path, or null if none exist.
 */
async function resolveHtmlPath(input) {
  const candidates = [resolve(input)];
  const m = /^([A-Za-z]):[\\/](.*)$/.exec(input);
  if (m) candidates.push(`/mnt/${m[1].toLowerCase()}/${m[2].replace(/\\/g, "/")}`);
  for (const c of candidates) {
    try {
      await stat(c);
      return c;
    } catch {
      /* try next */
    }
  }
  return null;
}

/**
 * In-page overlay for a SINGLE layer node: draws magenta 9-slice guide lines on that
 * node's frame (if it is a sprite with a 9-slice), into a max-z layer so an element
 * screenshot captures them. Runs in the browser via page.evaluate(fn, payload).
 *
 * @param {{node:object, design:{width:number,height:number}}} payload
 * @returns {number} 1 if guides were drawn, else 0.
 */
const DRAW_NODE_FN = (payload) => {
  const { node, design } = payload;
  const prev = document.getElementById("__unimancer_ov");
  if (prev) prev.remove();
  if (!node || node.kind !== "sprite" || !Array.isArray(node.nineSlice)) return 0;

  const dw = design.width || 1, dh = design.height || 1;
  const docW = document.documentElement.clientWidth || window.innerWidth || 1;
  const docH = document.documentElement.clientHeight || window.innerHeight || 1;
  const toCssX = docW / dw, toCssY = docH / dh; // design px -> css px

  const el = document.querySelector(node.selector);
  if (!el) return 0;
  const b = el.getBoundingClientRect();
  const r = { x: b.left + window.scrollX, y: b.top + window.scrollY, w: b.width, h: b.height };

  const layer = document.createElement("div");
  layer.id = "__unimancer_ov";
  layer.style.cssText = "position:absolute;left:0;top:0;pointer-events:none;z-index:2147483647;";
  document.body.appendChild(layer);
  const line = (x, y, len, vertical) => {
    const d = document.createElement("div");
    d.style.cssText =
      `position:absolute;left:${x}px;top:${y}px;background:#ff3df0;` +
      (vertical ? `width:1px;height:${len}px;` : `height:1px;width:${len}px;`);
    layer.appendChild(d);
  };
  const L = node.nineSlice[0] * toCssX, T = node.nineSlice[1] * toCssY;
  const R = node.nineSlice[2] * toCssX, B = node.nineSlice[3] * toCssY;
  if (L > 0) line(r.x + L, r.y, r.h, true);
  if (R > 0) line(r.x + r.w - R, r.y, r.h, true);
  if (T > 0) line(r.x, r.y + T, r.w, false);
  if (B > 0) line(r.x, r.y + r.h - B, r.w, false);
  return 1;
};

