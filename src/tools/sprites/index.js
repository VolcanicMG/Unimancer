/** Sprites — configure import settings on existing images and procedurally generate sprite PNGs. */
/** @type {import("../../core/types.js").ToolDefinition[]} */
import { spriteImport } from "./spriteImport.js";
import { spriteGenerate } from "./spriteGenerate.js";

export const spriteTools = [spriteImport, spriteGenerate];
