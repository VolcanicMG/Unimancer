/**
 * Tool barrel: aggregates every tool group into one array consumed by index.js.
 * Each group owns its own subdirectory + index, so groups can be built in
 * parallel without touching this file.
 */
import { adbTools } from "./adb/index.js";
import { androidBuildTools } from "./androidBuild/index.js";
import { emulatorTools } from "./emulator/index.js";
import { gameObjectTools } from "./gameObject/index.js";
import { sceneAssetTools } from "./sceneAssets/index.js";
import { scriptTools } from "./scripts/index.js";
import { editorTools } from "./editor/index.js";
import { captureTools } from "./capture/index.js";

/** @type {import("../core/types.js").ToolDefinition[]} */
export const allTools = [
  ...adbTools,
  ...androidBuildTools,
  ...emulatorTools,
  ...gameObjectTools,
  ...sceneAssetTools,
  ...scriptTools,
  ...editorTools,
  ...captureTools,
];
