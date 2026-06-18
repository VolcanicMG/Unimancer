#!/usr/bin/env node
/**
 * Unimancer — MCP server entry point.
 *
 * Wires an MCP server over stdio, builds the shared tool context (a lazy Unity
 * Editor connection + a name->tool registry for batch dispatch), registers every
 * tool and the read-only resources, and serves.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { UnityConnection } from "./core/unityConnection.js";
import { registerTools } from "./core/registry.js";
import { registerResources } from "./core/resources.js";
import { allTools } from "./tools/index.js";

/**
 * Resolve the runtime bridge endpoint from UNITY_MCP_RUNTIME_URL or default 8091.
 * @returns {{host: string, port: number}}
 */
function runtimeEndpoint() {
  const raw = process.env.UNITY_MCP_RUNTIME_URL;
  if (raw) {
    const m = raw.match(/^(?:tcp:\/\/)?([^:/]+):(\d+)/);
    if (m) return { host: m[1], port: Number(m[2]) };
  }
  return { host: "127.0.0.1", port: 8091 };
}

async function main() {
  const server = new McpServer({ name: "unimancer", version: "0.1.0" });

  /** @type {import("./core/types.js").ToolContext} */
  const ctx = {
    unity: new UnityConnection(),
    // name->tool registry so batch_execute can dispatch to any tool.
    tools: new Map(allTools.map((t) => [t.name, t])),
    // Separate connection to the in-build/Play-mode runtime bridge (port 8091).
    runtime: new UnityConnection(runtimeEndpoint()),
  };

  registerTools(server, allTools, ctx);
  registerResources(server, ctx);

  // Forward unsolicited Unity editor events to the client as MCP logging
  // notifications (best-effort; ignored if the client didn't enable logging).
  ctx.unity.onEvent((evt) => {
    const level = evt && evt.event === "console_error" ? "error" : "info";
    try {
      const s = server.server;
      if (s && typeof s.sendLoggingMessage === "function") {
        s.sendLoggingMessage({ level, logger: "unity", data: evt }).catch(() => {});
      }
    } catch {
      /* client may not support logging notifications */
    }
  });

  const transport = new StdioServerTransport();
  await server.connect(transport);

  const shutdown = () => {
    ctx.unity.close();
    ctx.runtime.close();
    process.exit(0);
  };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

main().catch((e) => {
  process.stderr.write(`unimancer fatal: ${e instanceof Error ? e.stack : String(e)}\n`);
  process.exit(1);
});
