/**
 * Tool: `runtime_ui_list` — list interactable uGUI elements in the running scene
 * so the agent knows what it can click. Forwards to the runtime bridge (port 8091).
 */
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeUiList = {
  name: "runtime_ui_list",
  description:
    "List interactable uGUI elements (Button/Toggle/Slider/…) in the RUNNING scene so you know what runtime_ui_click can target. Returns { count, elements:[{ name, path, type, interactable, active }] }.",
  inputSchema: {},
  /**
   * @param {object} args - none.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_ui_list", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
