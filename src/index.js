#!/usr/bin/env node
/**
 * Unimancer — MCP server entry point.
 *
 * Wires an MCP server over stdio and registers the Android (adb/emulator) and
 * HTML->Unity bridge tools. Nothing here connects to Unity at boot: the one tool
 * that needs the Editor shells out to the `unity` CLI on demand (see core/unityCli.js).
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./core/registry.js";
import { allTools } from "./tools/index.js";

async function main() {
  const server = new McpServer({ name: "unimancer", version: "0.1.0" });

  /** @type {import("./core/types.js").ToolContext} */
  const ctx = {};

  registerTools(server, allTools, ctx);

  const transport = new StdioServerTransport();
  await server.connect(transport);

  const shutdown = () => process.exit(0);
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

main().catch((e) => {
  process.stderr.write(`unimancer fatal: ${e instanceof Error ? e.stack : String(e)}\n`);
  process.exit(1);
});
