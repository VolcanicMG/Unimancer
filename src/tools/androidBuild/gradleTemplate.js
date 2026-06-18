/**
 * Tool: `android_gradle_template` — inspect/enable Unity's custom Gradle
 * templates under Assets/Plugins/Android/. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const androidGradleTemplate = {
  name: "android_gradle_template",
  description:
    "Manage custom Android Gradle templates (mainTemplate, settingsTemplate, gradleProperties, baseProjectTemplate) under Assets/Plugins/Android/. action='status' reports which exist; 'read' returns the file contents; 'enable' copies Unity's default template into place if missing. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    template: z
      .enum(["mainTemplate", "settingsTemplate", "gradleProperties", "baseProjectTemplate"])
      .describe("Which Gradle template to act on."),
    action: z.enum(["status", "read", "enable"]).describe("status reports existence, read returns contents, enable installs the default."),
  },
  /**
   * @param {object} args - template plus action.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("android_gradle_template", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
