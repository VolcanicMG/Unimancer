/** Core — Profiler stats tools (Unity C# bridge). */
import { profilerMemory } from "./memory.js";
import { profilerFrameStats } from "./frameStats.js";
import { profilerEnable } from "./enable.js";
import { profilerTopMarkers } from "./topMarkers.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const profilerTools = [
  profilerMemory,
  profilerFrameStats,
  profilerEnable,
  profilerTopMarkers,
];
