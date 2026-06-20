/**
 * Tool: `sprite_import` — configure an existing PNG asset's TextureImporter as a
 * Sprite (Single mode), with pixelsPerUnit, pivot, 9-slice border, filter mode,
 * then reimport. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** A {x,y} vector raw shape (both components required when present). */
const vec2 = z.object({ x: z.number(), y: z.number() });

/** @type {import("../../core/types.js").ToolDefinition} */
export const spriteImport = {
  name: "sprite_import",
  description:
    "Configure an existing image asset's TextureImporter: textureType=Sprite, spriteImportMode=Single, then reimport. " +
    "Optional: pixelsPerUnit, pivot {x,y} (0..1), 9-slice border (spriteBorder L/T/R/B in pixels), and filterMode (Point/Bilinear/Trilinear). " +
    "`path` is the project-relative asset path (e.g. 'Assets/UI/panel.png'). Returns the applied import settings.",
  inputSchema: {
    path: z.string().describe("Project-relative asset path of the existing image (e.g. 'Assets/UI/panel.png')."),
    pixelsPerUnit: z.number().positive().optional().describe("Sprite pixels-per-unit (default keeps current)."),
    pivot: vec2.optional().describe("Sprite pivot {x,y} in 0..1 (e.g. {x:0.5,y:0.5} for center)."),
    spriteBorder: z
      .object({
        left: z.number().min(0),
        top: z.number().min(0),
        right: z.number().min(0),
        bottom: z.number().min(0),
      })
      .optional()
      .describe("9-slice border in pixels {left,top,right,bottom}."),
    filterMode: z
      .enum(["Point", "Bilinear", "Trilinear"])
      .optional()
      .describe("Texture filter mode."),
  },
  /**
   * @param {object} args - sprite import options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("sprite_import", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
