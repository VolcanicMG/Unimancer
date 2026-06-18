/**
 * Tool: `android_manifest` — list/add/remove permissions in the project's
 * AndroidManifest.xml (Assets/Plugins/Android/AndroidManifest.xml). Forwards to
 * the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const androidManifest = {
  name: "android_manifest",
  description:
    "Manage Android manifest permissions in Assets/Plugins/Android/AndroidManifest.xml. action='list' returns current permissions; 'add'/'remove' apply the given permission strings (e.g. android.permission.INTERNET) and return the resulting list. A minimal manifest is created on 'add' if none exists. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    action: z.enum(["list", "add", "remove"]).describe("Operation to perform on manifest permissions."),
    permissions: z
      .array(z.string())
      .optional()
      .describe("Permission strings for add/remove, e.g. android.permission.INTERNET."),
  },
  /**
   * @param {object} args - action plus optional permissions.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("android_manifest", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
