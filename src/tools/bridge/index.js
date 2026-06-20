/** Bridge — HTML->Unity art pipeline: dry-run inventory, asset/manifest export, and a one-shot export+build orchestrator. */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { htmlInventory } from "./htmlInventory.js";
import { htmlExport } from "./htmlExport.js";
import { htmlToUnity } from "./html_to_unity.js";
import { htmlPreview } from "./htmlPreview.js";

export const bridgeTools = [htmlInventory, htmlPreview, htmlExport, htmlToUnity];
