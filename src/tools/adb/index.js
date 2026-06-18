/** Tier 1 — ADB device-control tools (pure `adb`, no Unity needed). */
import { adbDevices } from "./devices.js";
import { adbInstall } from "./install.js";
import { adbUninstall } from "./uninstall.js";
import { adbLaunchApp } from "./launchApp.js";
import { adbStopApp } from "./stopApp.js";
import { adbClearData } from "./clearData.js";
import { adbLogcat } from "./logcat.js";
import { adbScreenshot } from "./screenshot.js";
import { adbScreenrecord } from "./screenrecord.js";
import { adbShell } from "./shell.js";
import { adbPush } from "./push.js";
import { adbPull } from "./pull.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const adbTools = [
  adbDevices,
  adbInstall,
  adbUninstall,
  adbLaunchApp,
  adbStopApp,
  adbClearData,
  adbLogcat,
  adbScreenshot,
  adbScreenrecord,
  adbShell,
  adbPush,
  adbPull,
];
