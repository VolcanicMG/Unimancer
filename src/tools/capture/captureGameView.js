/**
 * Tool: `capture_game_view` — render the Unity Game view (the main camera) to a
 * PNG on the Editor side, then read the file back and return it as an MCP image
 * content block so the model can actually SEE what the game is rendering.
 */
import { z } from "zod";
import { readFile } from "node:fs/promises";
import { err } from "../../core/types.js";
import { image } from "../../core/image.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const captureGameView = {
  name: "capture_game_view",
  description:
    "Render the Unity Game view (Camera.main, or the first enabled camera) to a PNG at the given outputPath and return it as an image so the model can see it. width/height default to 1280x720. Errors if no camera exists. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    outputPath: z.string().optional().describe("Optional absolute path to ALSO save the PNG Editor-side. Omit it to just get the image inline (recommended — avoids WSL/Windows path mismatches)."),
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
      const r = await ctx.unity.request("capture_game_view", args);
      // Prefer the inline bytes Unity sends back; fall back to reading a saved file.
      const b64 = r.base64 ?? (await readFile(r.path)).toString("base64");
      const note = r.path ? `Saved ${r.path} (${r.width}x${r.height})` : `${r.width}x${r.height}`;
      return image(b64, "image/png", note);
    } catch (e) {
      return err(e.message);
    }
  },
};
