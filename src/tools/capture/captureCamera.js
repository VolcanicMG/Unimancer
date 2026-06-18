/**
 * Tool: `capture_camera` — render an arbitrary Camera in the scene (resolved by
 * GameObject target or by camera name) to a PNG on the Editor side, then read it
 * back and return it as an MCP image content block.
 */
import { z } from "zod";
import { readFile } from "node:fs/promises";
import { err } from "../../core/types.js";
import { image } from "../../core/image.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const captureCamera = {
  name: "capture_camera",
  description:
    "Render a specific Camera in the scene to a PNG and return it as an image. Identify the camera by 'target' (a GameObject hierarchy path or instanceID of an object with a Camera) or by 'cameraName'. width/height default to 1280x720. Errors if the camera cannot be resolved. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    outputPath: z.string().describe("Absolute output file path for the PNG."),
    target: z
      .string()
      .optional()
      .describe("GameObject hierarchy path or instanceID of an object that has a Camera."),
    cameraName: z.string().optional().describe("Name of a Camera GameObject to render."),
    width: z.number().optional().describe("Render width in pixels; defaults to 1280."),
    height: z.number().optional().describe("Render height in pixels; defaults to 720."),
  },
  /**
   * @param {object} args - capture options (outputPath, target?/cameraName?, width?, height?).
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const r = await ctx.unity.request("capture_camera", args);
      const b64 = (await readFile(r.path)).toString("base64");
      return image(b64, "image/png", `Saved ${r.path} (${r.width}x${r.height})`);
    } catch (e) {
      return err(e.message);
    }
  },
};
