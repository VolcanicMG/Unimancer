/**
 * Tool: `sprite_generate` — procedurally generate a Texture2D, encode it to PNG,
 * write it into Assets/, then apply a Sprite import config. Patterns: solid,
 * gradient (linear/radial), checker, border-frame. Forwards to the Unity C#
 * bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** A {x,y} vector raw shape (both components required when present). */
const vec2 = z.object({ x: z.number(), y: z.number() });

/** An {r,g,b,a} color raw shape; channels are 0..255, alpha optional (default 255). */
const color = z.object({
  r: z.number().min(0).max(255),
  g: z.number().min(0).max(255),
  b: z.number().min(0).max(255),
  a: z.number().min(0).max(255).optional(),
});

/** @type {import("../../core/types.js").ToolDefinition} */
export const spriteGenerate = {
  name: "sprite_generate",
  description:
    "Procedurally generate a Texture2D, encode to PNG, write into Assets/, then import it as a Sprite. " +
    "Patterns: solid (uses color), gradient (linear/radial across colorStops), checker (color + color2 squares of `cellSize`), " +
    "border-frame (fill color2 with a `borderThickness` frame in color). " +
    "`path` must be a project-relative .png path (e.g. 'Assets/UI/Generated/btn.png'); intermediate folders are created. " +
    "Optional sprite import config: pixelsPerUnit, pivot, spriteBorder (9-slice), filterMode. Returns {path, instanceID(asset), settings}.",
  inputSchema: {
    path: z.string().describe("Project-relative .png output path (e.g. 'Assets/UI/Generated/btn.png'); folders auto-created."),
    pattern: z
      .enum(["solid", "gradient", "checker", "border-frame"])
      .describe("Procedural pattern to render."),
    width: z.number().int().positive().describe("Texture width in pixels."),
    height: z.number().int().positive().describe("Texture height in pixels."),
    color: color.optional().describe("Primary color (solid fill / checker square A / frame border). Default opaque white."),
    color2: color.optional().describe("Secondary color (checker square B / border-frame fill). Default transparent."),
    gradientType: z
      .enum(["linear", "radial"])
      .optional()
      .describe("Gradient kind when pattern=gradient (default linear)."),
    gradientAngle: z.number().optional().describe("Linear gradient angle in degrees (default 0)."),
    colorStops: z
      .array(color)
      .min(2)
      .optional()
      .describe("Gradient palette (2+ colors). Falls back to [color, color2] when omitted."),
    cellSize: z.number().int().positive().optional().describe("Checker square size in pixels (default 16)."),
    borderThickness: z.number().int().positive().optional().describe("Border-frame thickness in pixels (default 4)."),
    // Sprite import config (applied after the PNG is written) — mirrors sprite_import.
    pixelsPerUnit: z.number().positive().optional().describe("Sprite pixels-per-unit."),
    pivot: vec2.optional().describe("Sprite pivot {x,y} in 0..1."),
    spriteBorder: z
      .object({
        left: z.number().min(0),
        top: z.number().min(0),
        right: z.number().min(0),
        bottom: z.number().min(0),
      })
      .optional()
      .describe("9-slice border in pixels {left,top,right,bottom}."),
    filterMode: z.enum(["Point", "Bilinear", "Trilinear"]).optional().describe("Texture filter mode."),
  },
  /**
   * @param {object} args - generation + import options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("sprite_generate", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
