/**
 * Tool: `gameobject_set_transform` — set position/rotation/scale and/or reparent
 * a GameObject (undoable). Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** A reusable {x,y,z} vector raw shape. */
const vec3 = z.object({ x: z.number(), y: z.number(), z: z.number() });

/** @type {import("../../core/types.js").ToolDefinition} */
export const gameobjectSetTransform = {
  name: "gameobject_set_transform",
  description:
    "Set a GameObject's transform and/or reparent it. 'target' is a hierarchy path or instanceID. position/rotation(euler)/scale are optional; only provided fields change. 'parentPath' reparents (empty string moves to scene root). 'worldSpace' (default true) controls whether position/rotation are world or local. Returns the resulting transform.",
  inputSchema: {
    target: z.string().describe("Hierarchy path or instanceID of the GameObject."),
    position: vec3.optional().describe("New position {x,y,z}."),
    rotation: vec3.optional().describe("New rotation as euler angles {x,y,z}."),
    scale: vec3.optional().describe("New local scale {x,y,z}."),
    parentPath: z.string().optional().describe("Reparent target; empty string detaches to scene root."),
    worldSpace: z.boolean().optional().describe("Interpret position/rotation in world space; default true."),
  },
  /**
   * @param {object} args - transform options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("gameobject_set_transform", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
