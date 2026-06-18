/** Core — NavMesh & lighting bake tools (Unity C# bridge). */
import { navmeshBake } from "./bake.js";
import { navmeshClear } from "./clear.js";
import { lightingBake } from "./lightingBake.js";
import { lightingClear } from "./lightingClear.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const navmeshTools = [
  navmeshBake,
  navmeshClear,
  lightingBake,
  lightingClear,
];
