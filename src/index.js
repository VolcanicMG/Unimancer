#!/usr/bin/env node
/**
 * Unimancer — MCP server entry point.
 *
 * Wires an MCP server over stdio, builds the shared tool context (a lazy Unity
 * Editor connection), registers every tool from the tool barrel, and serves.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { UnityConnection } from "./core/unityConnection.js";
import { registerTools } from "./core/registry.js";
import { allTools } from "./tools/index.js";

async function main() {
  const server = new McpServer({ name: "unimancer", version: "0.1.0" });

  /** @type {import("./core/types.js").ToolContext} */
  const ctx = { unity: new UnityConnection() };

  registerTools(server, allTools, ctx);

  const transport = new StdioServerTransport();
  await server.connect(transport);

  // stdio transport keeps the process alive; clean up Unity socket on exit.
  const shutdown = () => {
    ctx.unity.close();
    process.exit(0);
  };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

main().catch((e) => {
  // MCP clients read stdout; log diagnostics to stderr only.
  process.stderr.write(`unimancer fatal: ${e instanceof Error ? e.stack : String(e)}\n`);
  process.exit(1);
});
