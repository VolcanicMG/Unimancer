/** batch_execute — run several Unimancer tools in one MCP call. */
import { z } from "zod";
import { err } from "../../core/types.js";

/**
 * Runs an ordered list of tool calls in sequence, collecting each result. Cuts
 * round-trips for multi-step workflows (e.g. create object → add components →
 * set transform). Dispatches through `ctx.tools` (the registry injected at
 * startup), so it can call any Unity or Node tool except itself.
 */
export const batchExecute = {
  name: "batch_execute",
  description:
    "Run multiple Unimancer tools sequentially in ONE call. Pass `calls` as an ordered array of { tool, args }. Returns per-call results. Use to avoid many round-trips for multi-step edits. Cannot nest batch_execute.",
  inputSchema: {
    calls: z
      .array(z.object({ tool: z.string(), args: z.record(z.string(), z.any()).optional() }))
      .describe("Ordered tool calls to run."),
    stopOnError: z.boolean().optional().describe("Stop at the first failing call (default false)."),
  },
  /**
   * @param {{calls: Array<{tool:string, args?:object}>, stopOnError?:boolean}} args
   * @param {import("../../core/types.js").ToolContext} ctx
   * @returns {Promise<import("../../core/types.js").ToolResult>}
   */
  async handler(args, ctx) {
    const registry = ctx.tools;
    if (!registry) return err("batch_execute: tool registry unavailable");

    const results = [];
    let failed = 0;
    for (const call of args.calls) {
      if (call.tool === "batch_execute") {
        results.push({ tool: call.tool, isError: true, error: "nested batch_execute not allowed" });
        failed++;
        if (args.stopOnError) break;
        continue;
      }
      const def = registry.get(call.tool);
      if (!def) {
        results.push({ tool: call.tool, isError: true, error: `unknown tool: ${call.tool}` });
        failed++;
        if (args.stopOnError) break;
        continue;
      }
      try {
        const r = await def.handler(call.args ?? {}, ctx);
        const isError = !!(r && r.isError);
        if (isError) failed++;
        results.push({ tool: call.tool, isError, content: r && r.content });
        if (isError && args.stopOnError) break;
      } catch (e) {
        failed++;
        results.push({ tool: call.tool, isError: true, error: e instanceof Error ? e.message : String(e) });
        if (args.stopOnError) break;
      }
    }

    const summary = `batch: ${results.length} call(s), ${failed} failed`;
    return {
      content: [{ type: "text", text: summary + "\n" + JSON.stringify(results, null, 2) }],
      isError: failed > 0,
    };
  },
};
