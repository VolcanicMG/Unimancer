/**
 * Core contract shared by every Unimancer tool, expressed as JSDoc typedefs.
 *
 * A tool is self-describing: a name, an LLM-facing description, a Zod input
 * schema (raw shape, as the MCP SDK expects), and an async handler. Tools that
 * talk to the Unity Editor use `ctx.unity`; Android tools that only shell out to
 * `adb`/`emulator` ignore it.
 */

/**
 * @typedef {Object} ToolResultContent
 * @property {"text"} type
 * @property {string} text
 */

/**
 * @typedef {Object} ToolResult
 * @property {ToolResultContent[]} content
 * @property {boolean} [isError] - true marks a handled failure.
 */

/**
 * @typedef {Object} ToolContext
 * @property {import("./unityConnection.js").UnityConnection} unity - live Editor connection (may be disconnected).
 * @property {Map<string, ToolDefinition>} [tools] - name->tool registry, injected at startup for batch_execute dispatch.
 * @property {import("./unityConnection.js").UnityConnection} [runtime] - in-build/Play-mode runtime bridge connection (port 8091).
 */

/**
 * @typedef {Object} ToolDefinition
 * @property {string} name - unique MCP tool name (snake_case, e.g. `adb_install`).
 * @property {string} description - what the tool does, for an LLM choosing to call it.
 * @property {Record<string, import("zod").ZodTypeAny>} inputSchema - Zod raw shape for params.
 * @property {(args: any, ctx: ToolContext) => Promise<ToolResult>} handler - executes the tool.
 */

/**
 * Build a successful text result.
 * @param {string} text
 * @returns {ToolResult}
 */
export function ok(text) {
  return { content: [{ type: "text", text }] };
}

/**
 * Build a handled error result (not thrown).
 * @param {string} text
 * @returns {ToolResult}
 */
export function err(text) {
  return { content: [{ type: "text", text }], isError: true };
}
