/**
 * Tool: `android_player_settings` — get or set Android PlayerSettings (app id,
 * version, SDK levels, scripting backend, target architectures). Forwards to the
 * Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const androidPlayerSettings = {
  name: "android_player_settings",
  description:
    "Get or set Android PlayerSettings. action='get' returns current values; action='set' applies only the fields you provide (applicationIdentifier, bundleVersion, bundleVersionCode, minSdkVersion, targetSdkVersion, scriptingBackend, targetArchitectures). Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    action: z.enum(["get", "set"]).describe("'get' to read current settings, 'set' to apply provided fields."),
    applicationIdentifier: z.string().optional().describe("Android package id, e.g. com.company.game."),
    bundleVersion: z.string().optional().describe("Human-readable version name, e.g. 1.2.0."),
    bundleVersionCode: z.number().int().optional().describe("Integer Android versionCode."),
    minSdkVersion: z.number().int().optional().describe("Minimum Android API level."),
    targetSdkVersion: z.number().int().optional().describe("Target Android API level."),
    scriptingBackend: z.enum(["IL2CPP", "Mono2x"]).optional().describe("Scripting backend."),
    targetArchitectures: z
      .array(z.enum(["ARM64", "ARMv7", "X86_64"]))
      .optional()
      .describe("CPU architectures to build for."),
  },
  /**
   * @param {object} args - action plus optional setting fields.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("android_player_settings", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
