/**
 * Tool: `android_switch_platform` — switch the active Editor build target to
 * Android. Forwards to the Unity C# bridge, which calls
 * `EditorUserBuildSettings.SwitchActiveBuildTarget`.
 */
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const androidSwitchPlatform = {
  name: "android_switch_platform",
  description:
    "Switch the active Unity build target to Android. Call this before configuring Android player settings or building. Requires the Unity Editor to be open with the Unimancer package.",
  inputSchema: {},
  /**
   * @param {{}} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("android_switch_platform", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
