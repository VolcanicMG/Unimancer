/**
 * Tool: `material_set_properties` — set properties on an existing Material.
 * Forwards to the Unity C# bridge, which infers each property's type from the
 * value (color/vector/float/texture) and saves the asset.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const materialSetProperties = {
  name: "material_set_properties",
  description:
    "Set shader properties on a Material at the given Assets/ path. Each value's type is inferred: array length 4 -> Color (or Vector), length 2/3 -> Vector, number -> Float, string that is an asset path -> Texture. Returns the list of applied property keys.",
  inputSchema: {
    path: z.string().describe('Material asset path; must start with "Assets/".'),
    properties: z
      .record(z.string(), z.any())
      .describe("Map of property name -> value (number, array of numbers, or texture asset path)."),
  },
  /**
   * @param {object} args - { path, properties }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("material_set_properties", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
