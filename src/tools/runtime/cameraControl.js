/**
 * Tool: `runtime_camera_control` — pan/move/rotate/zoom a camera in the running
 * game to look around the scene. Forwards to the runtime bridge (port 8091).
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

const vec3 = z.array(z.number()).length(3);

/** @type {import("../../core/types.js").ToolDefinition} */
export const runtimeCameraControl = {
  name: "runtime_camera_control",
  description:
    "Pan/move/rotate/zoom a camera in the RUNNING game to look around the scene. All params optional: camera (name; default Camera.main), position/rotation (absolute), move [x,y,z] (relative camera-local: +Z forward, +X right, +Y up), rotate [pitch,yaw,roll] (relative degrees), fov (perspective), orthoSize (orthographic). Returns the resulting camera transform.",
  inputSchema: {
    camera: z.string().optional().describe("Camera GameObject name (default: Camera.main)."),
    position: vec3.optional().describe("Absolute world position [x,y,z]."),
    rotation: vec3.optional().describe("Absolute euler rotation [x,y,z] (degrees)."),
    move: vec3.optional().describe("Relative move in camera-local space [x,y,z] (+Z forward)."),
    rotate: vec3.optional().describe("Relative rotation [pitch,yaw,roll] in degrees."),
    fov: z.number().optional().describe("Field of view (perspective cameras)."),
    orthoSize: z.number().optional().describe("Orthographic size (orthographic cameras)."),
  },
  /**
   * @param {object} args - camera-control options (see inputSchema).
   * @param {import("../../core/types.js").ToolContext} ctx - runtime bridge connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.runtime.request("runtime_camera_control", args), null, 2));
    } catch (e) {
      return err(e.message + " (is the game running in Play mode or a dev build, with the runtime bridge on :8091?)");
    }
  },
};
