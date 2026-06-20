/**
 * Tool: `ui_build_from_manifest` — assemble a Unity GameObject tree from a
 * bridge manifest.json (produced by `html_export`). Reads the manifest on the
 * Unity side and recursively builds: sprite->Image (Sliced when nineSlice),
 * icon->Image, text->TMP (UI.Text fallback), group->empty RectTransform, placing
 * each node via RectTransform from its rect + anchor. Auto-creates a
 * Canvas+EventSystem when there is none. Forwards to the Unity C# bridge.
 */
import { z } from "zod";
import { ok, err } from "../../core/types.js";

/** @type {import("../../core/types.js").ToolDefinition} */
export const uiBuildFromManifest = {
  name: "ui_build_from_manifest",
  description:
    "Assemble a Unity uGUI GameObject tree from a bridge manifest.json (from html_export). " +
    "Recursively builds sprite->Image (type=Sliced when a 9-slice border is present), icon->Image, " +
    "text->TextMeshProUGUI (falls back to UI.Text), group->empty RectTransform, placing each node by its rect+anchor. " +
    "Auto-creates a Canvas+EventSystem if none exists. `manifestPath` is project-relative (e.g. 'Assets/UI/healthbar/manifest.json'). " +
    "Optionally parent the assembled component under parentPath. SVG assets must already be imported as Sprites " +
    "(needs com.unity.vectorgraphics); otherwise that layer degrades with a clear message. Returns the built root {instanceID, path}.",
  inputSchema: {
    manifestPath: z
      .string()
      .describe("Project-relative path to the manifest.json (e.g. 'Assets/UI/healthbar/manifest.json')."),
    parentPath: z
      .string()
      .optional()
      .describe("Hierarchy path or instanceID to parent the assembled component under (defaults to a Canvas)."),
  },
  /**
   * @param {{manifestPath:string, parentPath?:string}} args - build options.
   * @param {import("../../core/types.js").ToolContext} ctx - live Editor connection.
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    try {
      return ok(JSON.stringify(await ctx.unity.request("ui_build_from_manifest", args), null, 2));
    } catch (e) {
      return err(e.message);
    }
  },
};
