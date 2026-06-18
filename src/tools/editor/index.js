/** Core — Editor control, Console, Packages & Tests tools (Unity C# bridge). */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { editorState } from "./editorState.js";
import { editorSetPlayMode } from "./editorSetPlayMode.js";
import { menuExecute } from "./menuExecute.js";
import { consoleRead } from "./consoleRead.js";
import { consoleClear } from "./consoleClear.js";
import { packageList } from "./packageList.js";
import { packageAdd } from "./packageAdd.js";
import { packageRemove } from "./packageRemove.js";
import { runTests } from "./runTests.js";
import { selectionGet } from "./selectionGet.js";
import { selectionSet } from "./selectionSet.js";
import { projectInfo } from "./projectInfo.js";

export const editorTools = [
  editorState,
  editorSetPlayMode,
  menuExecute,
  consoleRead,
  consoleClear,
  packageList,
  packageAdd,
  packageRemove,
  runTests,
  selectionGet,
  selectionSet,
  projectInfo,
];
