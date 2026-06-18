/** Tier 2 — Android build & config tools (require the Unity C# bridge). */
import { androidSwitchPlatform } from "./switchPlatform.js";
import { androidPlayerSettings } from "./playerSettings.js";
import { androidKeystoreConfig } from "./keystoreConfig.js";
import { androidBuild } from "./build.js";
import { androidManifest } from "./manifest.js";
import { androidGradleTemplate } from "./gradleTemplate.js";

/** @type {import("../../core/types.js").ToolDefinition[]} */
export const androidBuildTools = [
  androidSwitchPlatform,
  androidPlayerSettings,
  androidKeystoreConfig,
  androidBuild,
  androidManifest,
  androidGradleTemplate,
];
