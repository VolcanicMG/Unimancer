/**
 * Tool barrel: aggregates every tool tier into one array consumed by index.js.
 * Each tier owns its own subdirectory + index, so tiers can be built in
 * parallel without touching this file.
 */
import { adbTools } from "./adb/index.js";
import { androidBuildTools } from "./androidBuild/index.js";
import { emulatorTools } from "./emulator/index.js";

/** @type {import("../core/types.js").ToolDefinition[]} */
export const allTools = [...adbTools, ...androidBuildTools, ...emulatorTools];
