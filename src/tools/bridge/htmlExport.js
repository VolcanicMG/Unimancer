/**
 * Tool: `html_export` — EXPORT pass of the HTML->Unity-sprite bridge.
 *
 * Same parse + decomposition as `html_inventory` (shared `decompose.js`), but it
 * actually writes assets and a manifest the Unity side reassembles into a proper
 * GameObject tree:
 *
 *   - sprite/icon layer, format=png  -> hide the OTHER (sibling/descendant text
 *       & icon) layers, then Playwright-screenshots that element's box with
 *       omitBackground:true at deviceScaleFactor=exportScale -> a transparent PNG
 *       of JUST that frame/icon.
 *   - sprite/icon layer, format=svg  -> extracts the element's inline <svg>
 *       outerHTML and writes it VERBATIM as .svg (never rasterized).
 *   - text layer                     -> NOT rasterized; recorded in the manifest
 *       as a TMP node (text, font, size, weight, color, align).
 *
 * Assets land under outDir (default `Assets/UI/<componentName>/`), named
 * `<component>__<layer>.png|svg`. A `manifest.json` per component captures the
 * reassembly tree. Idempotent: the same inputs overwrite the same paths.
 *
 * Component-only: each detected widget becomes one reusable component with its
 * own assets + manifest. There is no whole-screen export mode.
 *
 * Runs entirely in Node via Playwright — it does NOT touch the Unity Editor.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { pathToFileURL } from "node:url";
import { resolve, join, isAbsolute } from "node:path";
import { stat, mkdir, writeFile } from "node:fs/promises";
import { withPage } from "./playwright.js";
import { decomposePage } from "./decompose.js";
import { captureLayerPng } from "./layers.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const htmlExport = {
  name: "html_export",
  description:
    "EXPORT a Claude Design HTML mockup into reusable Unity components. Decomposes each detected widget into layer assets: " +
    "sprite/icon PNG layers are screenshot per-element with siblings hidden and omitBackground (transparent) at deviceScaleFactor=exportScale; " +
    "inline <svg> layers are written verbatim as .svg; text layers are recorded (not rasterized) as TMP nodes. " +
    "Writes <component>__<layer>.png|svg plus a manifest.json under outDir (default 'Assets/UI/<componentName>/'). " +
    "Idempotent (overwrites same paths). Feed the manifest to ui_build_from_manifest in Unity. " +
    "Requires Playwright (a pinned dep — must be approved/installed).",
  inputSchema: {
    htmlPath: z.string().describe("Filesystem path to the self-contained HTML mockup file."),
    outDir: z
      .string()
      .optional()
      .describe("Base output dir for assets+manifest (default 'Assets/UI'; each component gets a '<componentName>' subfolder)."),
    designWidth: z.number().positive().optional().describe("Normalize geometry to this design width in px."),
    designHeight: z.number().positive().optional().describe("Normalize geometry to this design height in px."),
    exportScale: z
      .number()
      .positive()
      .optional()
      .describe("deviceScaleFactor for PNG crispness (default 3 = 3x supersampled rasters)."),
  },
  /**
   * @param {{htmlPath:string, outDir?:string, designWidth?:number, designHeight?:number, exportScale?:number}} args
   * @param {import("../../core/types.js").ToolContext} _ctx - unused (no Unity round-trip).
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    try {
      const htmlPath = resolve(args.htmlPath);
      try {
        await stat(htmlPath);
      } catch {
        return err(`HTML file not found: ${htmlPath}`);
      }

      const exportScale = args.exportScale ?? 3;
      const baseOut = args.outDir ?? "Assets/UI";
      // Resolve baseOut relative to CWD when not absolute; keep the original
      // (likely 'Assets/...') string for the manifest so Unity gets a
      // project-relative path it can AssetDatabase-import.
      const absBaseOut = isAbsolute(baseOut) ? baseOut : resolve(baseOut);

      const url = pathToFileURL(htmlPath).href;
      const viewport =
        args.designWidth && args.designHeight
          ? { width: Math.round(args.designWidth), height: Math.round(args.designHeight) }
          : undefined;

      const written = [];

      await withPage(
        async (page) => {
          await page.goto(url, { waitUntil: "networkidle" });
          const decomp = await decomposePage(page, args.designWidth, args.designHeight);

          // Export each component independently.
          for (const comp of decomp.components) {
            const compDir = join(absBaseOut, comp.name);
            await mkdir(compDir, { recursive: true });
            // Project-relative asset prefix used INSIDE the manifest (Unity side).
            const relPrefix = `${baseOut.replace(/\\/g, "/")}/${comp.name}`;

            // Build the manifest node tree while exporting each layer's asset.
            const rootNode = await exportLayer(page, comp.node, comp.name, compDir, relPrefix, exportScale, written);

            const manifest = {
              meta: {
                design: decomp.design,
                exportScale,
                source: htmlPath,
              },
              // Top-level "nodes" is the component's own tree as a single entry.
              nodes: [rootNode],
            };
            const manifestPath = join(compDir, "manifest.json");
            await writeFile(manifestPath, JSON.stringify(manifest, null, 2), "utf8");
            written.push(manifestPath);
          }
        },
        { deviceScaleFactor: exportScale, viewport }
      );

      return ok(
        JSON.stringify(
          {
            source: htmlPath,
            outDir: baseOut,
            exportScale,
            componentCount: written.filter((p) => p.endsWith("manifest.json")).length,
            written,
          },
          null,
          2
        )
      );
    } catch (e) {
      return err(e.message);
    }
  },
};

/**
 * Recursively export a layer node to disk and return its manifest descriptor.
 * Mirrors `decompose.js` layer kinds: text is recorded only; icon/sprite is
 * rasterized (png) or copied verbatim (svg); group is structural.
 *
 * @param {import("playwright").Page} page - the loaded page.
 * @param {object} node - a layer node from decomposePage (has kind, selector, rect, ...).
 * @param {string} component - the component name (used for asset filenames).
 * @param {string} compDir - absolute output dir for this component.
 * @param {string} relPrefix - project-relative path prefix for manifest asset paths.
 * @param {number} exportScale - deviceScaleFactor in effect (informational here).
 * @param {string[]} written - accumulator of written file paths.
 * @returns {Promise<object>} the manifest descriptor for this node (with children).
 */
async function exportLayer(page, node, component, compDir, relPrefix, exportScale, written) {
  // Common fields carried into the manifest verbatim.
  const out = {
    name: node.name,
    type: manifestType(node.kind),
    rect: node.rect,
    anchor: node.anchor,
  };

  if (node.kind === "text") {
    // Recorded only — Unity creates a live TMP node from these fields.
    out.text = node.text;
    out.font = node.font;
    out.size = node.size;
    out.weight = node.weight;
    out.color = node.color;
    out.align = node.align;
    return out;
  }

  if (node.kind === "group") {
    // Structural container: no asset, just recurse.
    if (node.children?.length) {
      out.children = [];
      for (const child of node.children) {
        out.children.push(await exportLayer(page, child, component, compDir, relPrefix, exportScale, written));
      }
    }
    return out;
  }

  // sprite | icon -> emit an asset.
  out.kind = node.kind;
  out.format = node.format;
  out.nineSlice = node.nineSlice ?? null;

  const ext = node.format === "svg" ? "svg" : "png";
  const fileName = `${component}__${node.name}.${ext}`;
  const absAsset = join(compDir, fileName);
  out.asset = `${relPrefix}/${fileName}`;

  if (node.format === "svg") {
    // Extract the inline <svg> outerHTML. Claude Design SVGs reference page-level
    // CSS custom properties (e.g. fill="var(--td-accent)", filter glows via
    // var(--td-glow)); standalone (as an <img> or a Unity asset) those :root vars
    // are gone, so the icon renders colorless. We resolve every referenced custom
    // property from the SVG's computed style and pin it inline on the <svg> root
    // (custom props inherit, so all descendants' var() then resolve).
    const svg = await page.evaluate((sel) => {
      const el = document.querySelector(sel);
      if (!el) return null;
      // The selector may target the <svg> itself or a wrapper containing one.
      const svgEl = el.tagName.toLowerCase() === "svg" ? el : el.querySelector("svg");
      if (!svgEl) return null;
      // An inline <svg> inherits the SVG/XLink namespaces from the HTML parser,
      // so its outerHTML usually omits them. A STANDALONE .svg file MUST declare
      // them or browsers/importers treat it as plain XML text (renders nothing).
      if (!svgEl.getAttribute("xmlns")) svgEl.setAttribute("xmlns", "http://www.w3.org/2000/svg");
      if (/xlink:/.test(svgEl.outerHTML) && !svgEl.getAttribute("xmlns:xlink")) {
        svgEl.setAttribute("xmlns:xlink", "http://www.w3.org/1999/xlink");
      }
      const cs = getComputedStyle(svgEl);
      // Collect every --custom-prop name referenced anywhere in the subtree.
      const names = Array.from(
        new Set((svgEl.outerHTML.match(/var\((--[\w-]+)/g) || []).map((m) => m.slice(4)))
      );
      const decls = names
        .map((n) => [n, cs.getPropertyValue(n).trim()])
        .filter(([, v]) => v.length > 0)
        .map(([n, v]) => `${n}:${v}`)
        .join(";");
      if (decls) {
        const prev = svgEl.getAttribute("style") || "";
        svgEl.setAttribute("style", prev ? `${prev};${decls}` : decls);
      }
      return svgEl.outerHTML;
    }, node.selector);
    if (svg) {
      await writeFile(absAsset, svg, "utf8");
      written.push(absAsset);
    } else {
      // No inline SVG found despite svg format — record but don't fail the run.
      out.warning = "no inline <svg> found at selector; asset not written";
    }
  } else {
    // PNG: rasterize JUST this layer — content children hidden, siblings hidden,
    // ancestor/own box-shadow rings cleared, and the capture expanded to include any
    // drop-shadow glow (see captureLayerPng). Transparent everywhere it doesn't paint.
    const buf = await captureLayerPng(page, node);
    if (!buf) {
      out.warning = "selector not found; asset not written";
    } else {
      await writeFile(absAsset, buf);
      written.push(absAsset);
    }
  }

  // Recurse into children (e.g. a frame's nested icon/text peeled out separately).
  if (node.children?.length) {
    out.children = [];
    for (const child of node.children) {
      out.children.push(await exportLayer(page, child, component, compDir, relPrefix, exportScale, written));
    }
  }
  return out;
}

/**
 * Map a decompose kind to the manifest `type` field. sprite/icon collapse to
 * their own type tokens; group/text pass through. The Unity builder switches on
 * this plus `kind` for assetful nodes.
 *
 * @param {string} kind - one of sprite|icon|text|group.
 * @returns {string} the manifest node type.
 */
function manifestType(kind) {
  // We keep the type aligned with kind so the C# side has a single switch.
  return kind;
}
