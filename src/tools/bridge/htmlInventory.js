/**
 * Tool: `html_inventory` — DRY RUN of the HTML->Unity-sprite bridge.
 *
 * Loads a self-contained Claude Design HTML mockup in headless Chromium, walks
 * the DOM, and classifies every export candidate WITHOUT writing any files.
 * Returns, per detected component, a tree of layers annotated with inferred kind
 * (sprite|icon|text|group), inferred format (svg|png), geometry (design-px rect
 * relative to the component root), computed 9-slice border, and inferred anchor.
 *
 * This lets Claude SEE what `html_export` would produce, then add `data-*` tags
 * to the HTML to refine the decomposition before committing assets to disk.
 *
 * Runs entirely in Node via Playwright — it does NOT touch the Unity Editor, so
 * it ignores `ctx.unity`.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { pathToFileURL } from "node:url";
import { resolve } from "node:path";
import { stat } from "node:fs/promises";
import { withPage } from "./playwright.js";
import { decomposePage } from "./decompose.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const htmlInventory = {
  name: "html_inventory",
  description:
    "DRY RUN: load a Claude Design HTML mockup in headless Chromium and classify export candidates WITHOUT writing files. " +
    "Returns, per detected component, a layer tree with: kind (sprite|icon|text|group), format (svg|png), geometry " +
    "(design-px rect relative to the component root), computed 9-slice border, and inferred anchor. " +
    "Use this to preview the decomposition and add data-* tags (data-ui/data-name/data-layer/data-format/data-9slice/data-anchor) " +
    "to refine it before calling html_export. Requires Playwright (a pinned dep — must be approved/installed).",
  inputSchema: {
    htmlPath: z.string().describe("Filesystem path to the self-contained HTML mockup file."),
    designWidth: z
      .number()
      .positive()
      .optional()
      .describe("Normalize geometry to this design width in px (defaults to the document layout width)."),
    designHeight: z
      .number()
      .positive()
      .optional()
      .describe("Normalize geometry to this design height in px (defaults to the document layout height)."),
  },
  /**
   * @param {{htmlPath:string, designWidth?:number, designHeight?:number}} args - dry-run options.
   * @param {import("../../core/types.js").ToolContext} _ctx - unused (no Unity round-trip).
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, _ctx) {
    try {
      const htmlPath = resolve(args.htmlPath);
      // Fail early with a clear message if the file is missing.
      try {
        await stat(htmlPath);
      } catch {
        return err(`HTML file not found: ${htmlPath}`);
      }

      const url = pathToFileURL(htmlPath).href;
      // Viewport defaults to the design size so layout matches what we measure.
      const viewport =
        args.designWidth && args.designHeight
          ? { width: Math.round(args.designWidth), height: Math.round(args.designHeight) }
          : undefined;

      const result = await withPage(
        async (page) => {
          await page.goto(url, { waitUntil: "networkidle" });
          return decomposePage(page, args.designWidth, args.designHeight);
        },
        { viewport }
      );

      return ok(
        JSON.stringify(
          {
            source: htmlPath,
            design: result.design,
            componentCount: result.components.length,
            components: result.components,
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
