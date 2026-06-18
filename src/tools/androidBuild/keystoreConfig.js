/**
 * Tool: `android_keystore_config` — configure Android signing (custom keystore +
 * key alias). Forwards to the Unity C# bridge.
 *
 * SECURITY: secret values (passwords) are sent to the Editor but are NEVER
 * echoed back in the result — the C# side returns only booleans indicating what
 * was set.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const androidKeystoreConfig = {
  name: "android_keystore_config",
  description:
    "Configure Android signing. Set useCustomKeystore plus the keystore path/password and key alias name/password. Passwords are applied but never returned. Requires the Unity Editor open with the Unimancer package.",
  inputSchema: {
    useCustomKeystore: z.boolean().describe("Enable a custom keystore (false uses the Unity debug keystore)."),
    keystorePath: z.string().optional().describe("Path to the .keystore file."),
    keystorePass: z.string().optional().describe("Keystore password (never echoed back)."),
    keyaliasName: z.string().optional().describe("Key alias name within the keystore."),
    keyaliasPass: z.string().optional().describe("Key alias password (never echoed back)."),
  },
  /**
   * @param {object} args - signing configuration.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("android_keystore_config", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
