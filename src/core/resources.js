/**
 * Registers read-only MCP resources backed by Unity bridge reads. Resources let
 * a client pull live Editor context (scene tree, console, selection, project,
 * editor state) without spending a tool call. Each resource lazily queries the
 * same Editor methods the corresponding tools use.
 */

/**
 * @param {import("@modelcontextprotocol/sdk/server/mcp.js").McpServer} server
 * @param {import("./types.js").ToolContext} ctx
 */
export function registerResources(server, ctx) {
  /**
   * Register one JSON resource that proxies a Unity bridge method.
   * @param {string} name @param {string} uri @param {string} description @param {string} method
   */
  const res = (name, uri, description, method) =>
    server.registerResource(
      name,
      uri,
      { title: name, description, mimeType: "application/json" },
      async (u) => ({
        contents: [
          {
            uri: u.href,
            mimeType: "application/json",
            text: JSON.stringify(await ctx.unity.request(method, {}), null, 2),
          },
        ],
      }),
    );

  res("scene-hierarchy", "unity://scene/hierarchy", "Active scene hierarchy tree.", "scene_get_hierarchy");
  res("console", "unity://console", "Recent Unity console entries.", "console_read");
  res("selection", "unity://selection", "Current Editor selection.", "selection_get");
  res("project", "unity://project", "Project + build settings.", "project_info");
  res("editor-state", "unity://editor/state", "Editor play/pause/compile state.", "editor_state");
}
