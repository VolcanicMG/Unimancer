/**
 * Tool: `shader_find` — look up a shader by name (and list its properties) or,
 * with no name, list available shaders. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const shaderFind = {
  name: "shader_find",
  description:
    "If name is given, look up that shader via Shader.Find and return { found, properties }. If no name, return a capped list of available shader names. Returns shader info as JSON.",
  inputSchema: {
    name: z.string().optional().describe("Exact shader name to look up; omit to list shaders."),
  },
  /**
   * @param {object} args - { name? }.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("shader_find", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
