/** Runtime — in-player / Play-mode tools (Unity Runtime bridge, port 8091). */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { runtimeSceneInfo } from "./sceneInfo.js";
import { runtimeFindObjects } from "./findObjects.js";
import { runtimeGetComponent } from "./getComponent.js";
import { runtimeSetComponentProperty } from "./setComponentProperty.js";
import { runtimeCallMethod } from "./callMethod.js";
import { runtimeSetTimescale } from "./setTimescale.js";
import { runtimeLogTail } from "./logTail.js";
import { runtimeUiClick } from "./uiClick.js";
import { runtimeUiList } from "./uiList.js";
import { runtimeCameraControl } from "./cameraControl.js";
import { runtimePointerDrag } from "./pointerDrag.js";

export const runtimeTools = [
  runtimeSceneInfo,
  runtimeFindObjects,
  runtimeGetComponent,
  runtimeSetComponentProperty,
  runtimeCallMethod,
  runtimeSetTimescale,
  runtimeLogTail,
  runtimeUiClick,
  runtimeUiList,
  runtimeCameraControl,
  runtimePointerDrag,
];
