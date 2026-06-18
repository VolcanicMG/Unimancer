/**
 * Registers Unimancer tool definitions with an MCP server instance.
 *
 * Each {@link import("./types.js").ToolDefinition} is wrapped so the MCP SDK
 * handles Zod validation + JSON-schema generation from `inputSchema`, then our
 * handler runs with the shared {@link import("./types.js").ToolContext}. Handler
 * exceptions are converted into MCP error results rather than crashing the
 * server.
 */

/**
 * @param {import("@modelcontextprotocol/sdk/server/mcp.js").McpServer} server
 * @param {import("./types.js").ToolDefinition[]} tools
 * @param {import("./types.js").ToolContext} ctx
 */
export function registerTools(server, tools, ctx) {
  for (const tool of tools) {
    server.registerTool(
      tool.name,
      { description: tool.description, inputSchema: tool.inputSchema },
      async (args) => {
        try {
          return await tool.handler(args, ctx);
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          return { content: [{ type: "text", text: `Tool "${tool.name}" failed: ${message}` }], isError: true };
        }
      },
    );
  }
}
