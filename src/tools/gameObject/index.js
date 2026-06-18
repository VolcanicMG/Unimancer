/** Core — GameObject & Component tools (Unity C# bridge). */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { gameobjectCreate } from "./gameobjectCreate.js";
import { gameobjectFind } from "./gameobjectFind.js";
import { gameobjectDelete } from "./gameobjectDelete.js";
import { gameobjectSetTransform } from "./gameobjectSetTransform.js";
import { gameobjectSetActive } from "./gameobjectSetActive.js";
import { gameobjectRename } from "./gameobjectRename.js";
import { componentAdd } from "./componentAdd.js";
import { componentRemove } from "./componentRemove.js";
import { componentList } from "./componentList.js";
import { componentSetProperty } from "./componentSetProperty.js";

export const gameObjectTools = [
  gameobjectCreate,
  gameobjectFind,
  gameobjectDelete,
  gameobjectSetTransform,
  gameobjectSetActive,
  gameobjectRename,
  componentAdd,
  componentRemove,
  componentList,
  componentSetProperty,
];
