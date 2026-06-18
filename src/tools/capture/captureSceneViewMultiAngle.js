/**
 * Tool: `capture_scene_view_multi_angle` — frame the whole active scene and
 * render it from several canonical angles (front/back/left/right/top + a 3/4
 * perspective) via a temporary camera on the Editor side. Each angle is saved as
 * a PNG; the handler reads them all back and returns one image content block per
 * angle so the model can inspect the scene from every side at once.
 */
import { z } from "zod";
import { readFile } from "node:fs/promises";
import { basename } from "node:path";
import { err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const captureSceneViewMultiAngle = {
  name: "capture_scene_view_multi_angle",
  description:
    "Frame every renderer in the active scene and render it from several angles (front, back, left, right, top, and a 3/4 perspective) using a temporary camera. Saves one PNG per angle into outputDir (filenames '<prefix>_<angle>.png') and returns them all as images. width/height default to 1024x1024; prefix defaults to 'view'. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    outputDir: z.string().describe("Absolute directory to write the per-angle PNGs into."),
    prefix: z.string().optional().describe("Filename prefix for each angle PNG; defaults to 'view'."),
    width: z.number().optional().describe("Render width in pixels; defaults to 1024."),
    height: z.number().optional().describe("Render height in pixels; defaults to 1024."),
  },
  /**
   * @param {object} args - capture options (outputDir, prefix?, width?, height?).
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const r = await ctx.unity.request("capture_scene_view_multi_angle", args);
      /** @type {any[]} */
      const content = [];
      for (const p of r.paths) {
        const b64 = (await readFile(p)).toString("base64");
        content.push({ type: "image", data: b64, mimeType: "image/png" });
        // Caption each image with its angle (derived from the filename) so the
        // model can tell the views apart.
        content.push({ type: "text", text: basename(p) });
      }
      content.push({ type: "text", text: `Saved ${r.count} angle(s).` });
      return { content };
    } catch (e) {
      return err(e.message);
    }
  },
};
