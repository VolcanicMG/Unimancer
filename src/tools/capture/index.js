/** Core — Visual capture tools (render Game/Scene/Camera views to images). */
import { captureGameView } from "./captureGameView.js";
import { captureSceneView } from "./captureSceneView.js";
import { captureCamera } from "./captureCamera.js";
import { captureSceneViewMultiAngle } from "./captureSceneViewMultiAngle.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const captureTools = [
  captureGameView,
  captureSceneView,
  captureCamera,
  captureSceneViewMultiAngle,
];
