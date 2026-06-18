/**
 * Tool: `project_info` — report project + build settings. Forwards to the Unity
 * C# bridge, which reads Application / PlayerSettings / EditorUserBuildSettings.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const projectInfo = {
  name: "project_info",
  description:
    "Report Unity project info: unityVersion, productName, companyName, dataPath, activeBuildTarget, scriptingBackend, colorSpace, batchMode, installedPackageCount.",
  inputSchema: {},
  /**
   * @param {object} args - no parameters.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      const result = await ctx.unity.request("project_info", args);
      return ok(JSON.stringify(result, null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
