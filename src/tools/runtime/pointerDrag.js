/**
 * Tool: `runtime_pointer_drag` — drag/swipe on a running uGUI element (ScrollRect /
 * IDragHandler) to pan or scroll. Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

const vec2 = z.array(z.number()).length(2);

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimePointerDrag = {
  name: "runtime_pointer_drag",
  description:
    "Drag/swipe on a running uGUI element (ScrollRect / IDragHandler) to pan or scroll. Provide from+to (screen px) or delta; optional steps (default 8). Returns { dragged, target, handlers, from, to }. Only reaches EventSystem UI — not Input-polling games (use runtime_camera_control for those).",
  inputSchema: {
    target: z.string().describe("GameObject name or hierarchy path of the draggable element (e.g. a ScrollRect)."),
    from: vec2.optional().describe("Start screen position [x,y] in pixels (default: screen center)."),
    to: vec2.optional().describe("End screen position [x,y] in pixels."),
    delta: vec2.optional().describe("Total drag delta [dx,dy] in pixels (alternative to from/to)."),
    steps: z.number().optional().describe("Interpolated drag steps (default 8)."),
  },
  /**
   * @param {object} args - drag options (see inputSchema).
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_pointer_drag", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
