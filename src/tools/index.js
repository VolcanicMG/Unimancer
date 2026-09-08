/**
 * Tool barrel: aggregates every tool group into one array consumed by index.js.
 * Each group owns its own subdirectory + index, so groups can be built in
 * parallel without touching this file.
 *
 * Scope note: Unity Editor authoring lives in Unity's own CLI (`unity mcp`), not
 * here. Unimancer only keeps what that CLI does not cover — Android devices and
 * the HTML->Unity art bridge.
 */
import { adbTools } from "./adb/index.js";
import { emulatorTools } from "./emulator/index.js";
import { bridgeTools } from "./bridge/index.js";

/** @type {import("../core/types.js").ToolDefinition[]} */
export const allTools = [...adbTools, ...emulatorTools, ...bridgeTools];
