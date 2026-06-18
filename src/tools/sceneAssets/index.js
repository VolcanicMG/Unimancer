/** Core — Scene, Asset & Prefab tools (Unity C# bridge). */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { sceneOpen } from "./sceneOpen.js";
import { sceneSave } from "./sceneSave.js";
import { sceneNew } from "./sceneNew.js";
import { sceneList } from "./sceneList.js";
import { sceneGetHierarchy } from "./sceneGetHierarchy.js";
import { assetCreateFolder } from "./assetCreateFolder.js";
import { assetMove } from "./assetMove.js";
import { assetDelete } from "./assetDelete.js";
import { assetFind } from "./assetFind.js";
import { assetRefresh } from "./assetRefresh.js";
import { prefabCreate } from "./prefabCreate.js";
import { prefabInstantiate } from "./prefabInstantiate.js";

export const sceneAssetTools = [
  sceneOpen,
  sceneSave,
  sceneNew,
  sceneList,
  sceneGetHierarchy,
  assetCreateFolder,
  assetMove,
  assetDelete,
  assetFind,
  assetRefresh,
  prefabCreate,
  prefabInstantiate,
];
