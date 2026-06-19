/**
 * Tool: `runtime_ui_click` — click a UI element in the running game to test
 * interactions. Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeUiClick = {
  name: "runtime_ui_click",
  description:
    "Click a UI element in the RUNNING game (Play mode/dev build) to test interactions. Dispatches pointer down/up/click to the target's uGUI handlers (Button onClick, Toggle, EventTrigger, IPointerClickHandler). Returns { clicked, target, handlers }. Use runtime_ui_list to discover targets.",
  inputSchema: {
    target: z.string().describe("GameObject name or hierarchy path of the UI element to click."),
  },
  /**
   * @param {object} args - { target }.
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_ui_click", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
