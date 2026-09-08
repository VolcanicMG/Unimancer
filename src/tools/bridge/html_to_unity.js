/**
 * Tool: `html_to_unity` — ONE-SHOT orchestrator for the HTML->Unity bridge.
 *
 * "Take this HTML and build it in Unity" in a single MCP call. It chains the two
 * halves of the bridge that the user would otherwise call by hand:
 *
 *   1. `html_export`            (Node/Playwright) — decompose the mockup into
 *                               per-component layer assets + a manifest.json each.
 *   2. `ui_build_from_manifest` (Unity CLI round-trip) — for EACH exported manifest,
 *                               assemble the GameObject tree in the Editor. That is
 *                               one of unimancer's own [CliCommand]s, hosted by
 *                               com.unity.pipeline and reached via `unity command`.
 *
 * WHY a dedicated orchestrator (and not just batch_execute): the export step's
 * OUTPUT (the set of written manifest.json paths) is the INPUT to the build step,
 * and there is one build call per component. That data dependency can't be
 * expressed as a static batch — it has to be resolved at runtime by parsing the
 * export result. This tool owns that glue.
 *
 * `dryRun` short-circuits to `html_inventory` (no files, no Unity) so Claude can
 * preview the decomposition and add data-* tags before committing.
 *
 * Per-component build failures are collected, not fatal: one bad manifest must
 * not abort the rest of the batch (partial success is still useful).
 *
 * The build step needs a live Unity Editor, reached through the `unity` CLI.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";
import { htmlInventory } from "./htmlInventory.js";
import { htmlExport } from "./htmlExport.js";
import { unityCommand } from "../../core/unityCli.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const htmlToUnity = {
  name: "html_to_unity",
  description:
    "ONE-SHOT: take a Claude Design HTML mockup and BUILD it in Unity in a single call. " +
    "Chains html_export (decompose the mockup into per-component layer assets + a manifest.json each, via Playwright) " +
    "then the ui_build_from_manifest pipeline command once PER exported component (assemble each GameObject tree in the Editor, via the `unity` CLI). " +
    "Set dryRun:true to preview the decomposition via html_inventory WITHOUT writing files or touching Unity. " +
    "Per-component build failures are collected (the batch is not aborted). Returns {componentsExported, built:[...], outDir}. " +
    "Requires Playwright installed AND a live Unity Editor reachable via the `unity` CLI for the build step.",
  inputSchema: {
    htmlPath: z.string().describe("Filesystem path to the self-contained HTML mockup file."),
    parentPath: z
      .string()
      .optional()
      .describe("Hierarchy path or instanceID to parent each assembled component under (defaults to a Canvas)."),
    outDir: z
      .string()
      .optional()
      .describe("Base output dir for assets+manifests (default 'Assets/UI'; each component gets a '<componentName>' subfolder)."),
    designWidth: z.number().positive().optional().describe("Normalize geometry to this design width in px."),
    designHeight: z.number().positive().optional().describe("Normalize geometry to this design height in px."),
    exportScale: z
      .number()
      .positive()
      .optional()
      .describe("deviceScaleFactor for PNG crispness (default 3 = 3x supersampled rasters)."),
    dryRun: z
      .boolean()
      .optional()
      .describe("If true, only run html_inventory (preview the decomposition) — write nothing, touch no Unity."),
  },
  /**
   * @param {{htmlPath:string, parentPath?:string, outDir?:string, designWidth?:number, designHeight?:number, exportScale?:number, dryRun?:boolean}} args - orchestrator options.
   * @param {import("../../core/types.js").ToolContext} ctx - shared tool context (passed through to the export step).
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      // --- dry run: just preview, never write or touch Unity ---
      if (args.dryRun) {
        // Delegate to html_inventory unchanged; its result IS the preview.
        return htmlInventory.handler(
          {
            htmlPath: args.htmlPath,
            designWidth: args.designWidth,
            designHeight: args.designHeight,
          },
          ctx
        );
      }

      // --- step 1: export assets + manifests (Node/Playwright, no Unity) ---
      const exportResult = await htmlExport.handler(
        {
          htmlPath: args.htmlPath,
          outDir: args.outDir,
          designWidth: args.designWidth,
          designHeight: args.designHeight,
          exportScale: args.exportScale,
        },
        ctx
      );
      // Surface a hard export failure verbatim — there is nothing to build.
      if (exportResult.isError) return exportResult;

      // The export tool returns its summary as JSON text in the first content
      // block; parse it to recover the written manifest paths.
      const exportSummary = JSON.parse(exportResult.content[0].text);
      // One manifest.json per component is what the build step consumes.
      const manifests = (exportSummary.written || []).filter((p) => p.endsWith("manifest.json"));

      // --- step 2: build each component in Unity (one call per manifest) ---
      // Failures are collected per-component so one bad manifest doesn't abort
      // the whole batch (partial success is still useful to the caller).
      const built = [];
      for (const manifestPath of manifests) {
        try {
          const result = await unityCommand("ui_build_from_manifest", {
            manifestPath,
            parentPath: args.parentPath,
          });
          built.push({ manifest: manifestPath, result });
        } catch (e) {
          // Defensive: a thrown (vs. returned) error still must not kill the batch.
          built.push({ manifest: manifestPath, error: e.message });
        }
      }

      return ok(
        JSON.stringify(
          {
            componentsExported: manifests.length,
            built,
            outDir: exportSummary.outDir,
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
