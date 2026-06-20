/** Bridge — HTML->Unity-sprite decomposition: dry-run inventory + asset/manifest export (Playwright, Node-only). */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { htmlInventory } from "./htmlInventory.js";
import { htmlExport } from "./htmlExport.js";

export const bridgeTools = [htmlInventory, htmlExport];
