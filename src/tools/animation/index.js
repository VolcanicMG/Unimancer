/** Core — Animation clips & Animator tools (Unity C# bridge). */
import { animationCreateClip } from "./createClip.js";
import { animationAddCurve } from "./addCurve.js";
import { animationListClips } from "./listClips.js";
import { animatorGet } from "./animatorGet.js";
import { animatorSetParameter } from "./animatorSetParameter.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const animationTools = [
  animationCreateClip,
  animationAddCurve,
  animationListClips,
  animatorGet,
  animatorSetParameter,
];
