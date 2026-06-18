/**
 * Tool: `android_build` — build the Android player (APK or AAB). Forwards to the
 * Unity C# bridge, which runs BuildPipeline.BuildPlayer and returns a BuildReport
 * summary. This can take a long time; the connection request timeout applies.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const androidBuild = {
  name: "android_build",
  description:
    "Build the Android player to outputPath. format='apk' (default) or 'aab'; buildType='release' (default) or 'development'. Optionally pass explicit scene paths (defaults to the enabled scenes in Build Settings). Returns the BuildReport summary. Requires the Unity Editor open with the Unimancer package; builds may take minutes.",
  inputSchema: {
    outputPath: z.string().describe("Absolute output file path for the APK/AAB."),
    buildType: z.enum(["development", "release"]).optional().describe("Build configuration; defaults to release."),
    format: z.enum(["apk", "aab"]).optional().describe("Output format; defaults to apk."),
    scenes: z
      .array(z.string())
      .optional()
      .describe("Scene asset paths to include; defaults to enabled Build Settings scenes."),
  },
  /**
   * @param {object} args - build options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("android_build", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
