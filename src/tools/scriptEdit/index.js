/** Core — Script validation & structured editing tools (Unity C# bridge). */
import { scriptGetSha } from "./getSha.js";
import { scriptApplyEdits } from "./applyEdits.js";
import { scriptValidate } from "./validate.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const scriptEditTools = [scriptGetSha, scriptApplyEdits, scriptValidate];
