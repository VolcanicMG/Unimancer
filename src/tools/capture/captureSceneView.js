/**
 * Tool: `capture_scene_view` — render the active Scene view's camera to a PNG on
 * the Editor side, then read it back and return it as an MCP image content block
 * so the model can see the editor's current scene framing.
 */
import { z } from "zod";
import { readFile } from "node:fs/promises";
import { err } from "../../core/types.js";
import { image } from "../../core/image.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const captureSceneView = {
  name: "capture_scene_view",
  description:
    "Render the active Scene view camera (SceneView.lastActiveSceneView) to a PNG at the given outputPath and return it as an image so the model can see it. width/height default to 1280x720. Errors if no Scene view is open. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    outputPath: z.string().describe("Absolute output file path for the PNG."),
    width: z.number().optional().describe("Render width in pixels; defaults to 1280."),
    height: z.number().optional().describe("Render height in pixels; defaults to 720."),
  },
  /**
   * @param {object} args - capture options (outputPath, width?, height?).
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const r = await ctx.unity.request("capture_scene_view", args);
      const b64 = (await readFile(r.path)).toString("base64");
      return image(b64, "image/png", `Saved ${r.path} (${r.width}x${r.height})`);
    } catch (e) {
      return err(e.message);
    }
  },
};
