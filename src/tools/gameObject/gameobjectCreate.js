/**
 * Tool: `gameobject_create` — create a new GameObject (empty or from a built-in
 * primitive) in the active scene, optionally parenting it and setting its
 * transform. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** A reusable {x,y,z} vector raw shape (all components required when present). */
const vec3 = z.object({ x: z.number(), y: z.number(), z: z.number() });

/** @type {import("../../core/types.js").ToolDefinition} */
export const gameobjectCreate = {
  name: "gameobject_create",
  description:
    "Create a GameObject in the active scene. If 'primitive' is given, creates that built-in mesh primitive; otherwise an empty GameObject. Optionally set parentPath (hierarchy path of an existing object) and local position/rotation(euler)/scale. Returns {instanceID, path}. The action is undoable.",
  inputSchema: {
    name: z.string().describe("Name for the new GameObject."),
    primitive: z
      .enum(["Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"])
      .optional()
      .describe("Built-in primitive type; omit for an empty GameObject."),
    parentPath: z.string().optional().describe("Hierarchy path of an existing parent GameObject."),
    position: vec3.optional().describe("Local position {x,y,z}."),
    rotation: vec3.optional().describe("Local rotation as euler angles {x,y,z}."),
    scale: vec3.optional().describe("Local scale {x,y,z}."),
  },
  /**
   * @param {object} args - creation options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("gameobject_create", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
