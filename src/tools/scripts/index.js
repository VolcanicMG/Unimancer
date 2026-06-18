/** Core — Script & Material/Shader tools (Unity C# bridge). */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { scriptCreate } from "./scriptCreate.js";
import { scriptRead } from "./scriptRead.js";
import { scriptEdit } from "./scriptEdit.js";
import { scriptDelete } from "./scriptDelete.js";
import { scriptFindInFiles } from "./scriptFindInFiles.js";
import { materialCreate } from "./materialCreate.js";
import { materialSetProperties } from "./materialSetProperties.js";
import { shaderFind } from "./shaderFind.js";

export const scriptTools = [
  scriptCreate,
  scriptRead,
  scriptEdit,
  scriptDelete,
  scriptFindInFiles,
  materialCreate,
  materialSetProperties,
  shaderFind,
];
