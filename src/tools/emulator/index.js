/** Tier 3 — Emulator & SDK-environment tools (mostly shell, no Unity needed). */
import { emulatorListAvds } from "./listAvds.js";
import { emulatorStart } from "./start.js";
import { emulatorStop } from "./stop.js";
import { androidSdkCheck } from "./sdkCheck.js";
import { avdListRunning } from "./listRunning.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const emulatorTools = [
  emulatorListAvds,
  emulatorStart,
  emulatorStop,
  androidSdkCheck,
  avdListRunning,
];
