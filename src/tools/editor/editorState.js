/**
 * Tool: `editor_state` — report current Editor runtime state. Forwards to the
 * Unity C# bridge, which reads EditorApplication / scene / selection state.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const editorState = {
  name: "editor_state",
  description:
    "Report the current Unity Editor state: isPlaying, isPaused, isCompiling, activeScene, selectionCount, unityVersion.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("editor_state", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
