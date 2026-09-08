using System;
using System.Collections.Generic;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Build;

namespace Unimancer.Commands
{
    /// <summary>
    /// Android PlayerSettings commands contributed to com.unity.pipeline. Uses the
    /// Unity 6.x NamedBuildTarget-based APIs.
    ///
    /// SECURITY: signing passwords are deliberately NOT part of this surface —
    /// neither accepted nor returned. Pass them to
    /// <c>unity build --android-keystore-*</c> at build time instead, so secrets
    /// never sit in a command log or a settings asset written from here.
    /// </summary>
    public static class AndroidCommands
    {
        /// <summary>
        /// Get or set Android PlayerSettings. Every argument is optional: whatever
        /// is supplied is applied, and the current values are always returned — so
        /// a bare call is a pure read.
        /// </summary>
        [CliCommand("android_player_settings",
            "Get or set Android PlayerSettings (app id, version code, SDK levels, scripting backend, target architectures, keystore name/alias). " +
            "All args are optional: supplied values are applied, then the current settings are returned — a bare call is a pure read. " +
            "Signing passwords are NOT handled here; pass them to `unity build --android-keystore-*`.",
            Tags = new[] { "android" })]
        public static Dictionary<string, object> AndroidPlayerSettings(
            [CliArg("applicationIdentifier", "Android application id (e.g. 'com.studio.game').")] string applicationIdentifier = null,
            [CliArg("bundleVersionCode", "Android versionCode (integer build number).")] int? bundleVersionCode = null,
            [CliArg("minSdkVersion", "Minimum Android SDK API level (e.g. 23).")] int? minSdkVersion = null,
            [CliArg("targetSdkVersion", "Target Android SDK API level (0 = highest installed).")] int? targetSdkVersion = null,
            [CliArg("targetArchitectures", "Comma-separated: ARMv7, ARM64.")] string targetArchitectures = null,
            [CliArg("scriptingBackend", "IL2CPP | Mono2x")] string scriptingBackend = null,
            [CliArg("keystoreName", "Path to the custom keystore; also enables useCustomKeystore.")] string keystoreName = null,
            [CliArg("keystoreAlias", "Key alias within the keystore; also enables useCustomKeystore.")] string keystoreAlias = null)
        {
            var android = NamedBuildTarget.Android;

            if (applicationIdentifier != null)
                PlayerSettings.SetApplicationIdentifier(android, applicationIdentifier);

            if (bundleVersionCode.HasValue)
                PlayerSettings.Android.bundleVersionCode = bundleVersionCode.Value;

            if (minSdkVersion.HasValue)
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)minSdkVersion.Value;

            if (targetSdkVersion.HasValue)
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetSdkVersion.Value;

            if (!string.IsNullOrEmpty(scriptingBackend))
            {
                if (scriptingBackend != "IL2CPP" && scriptingBackend != "Mono2x")
                    throw new ArgumentException($"unknown scriptingBackend: {scriptingBackend} (expected IL2CPP or Mono2x)");
                PlayerSettings.SetScriptingBackend(android,
                    scriptingBackend == "Mono2x" ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP);
            }

            if (!string.IsNullOrEmpty(targetArchitectures))
                PlayerSettings.Android.targetArchitectures = ParseArchitectures(targetArchitectures);

            // Naming a keystore or alias is only meaningful with custom signing on.
            if (keystoreName != null)
            {
                PlayerSettings.Android.keystoreName = keystoreName;
                PlayerSettings.Android.useCustomKeystore = true;
            }
            if (keystoreAlias != null)
            {
                PlayerSettings.Android.keyaliasName = keystoreAlias;
                PlayerSettings.Android.useCustomKeystore = true;
            }

            return new Dictionary<string, object>
            {
                ["applicationIdentifier"] = PlayerSettings.GetApplicationIdentifier(android),
                ["bundleVersion"] = PlayerSettings.bundleVersion,
                ["bundleVersionCode"] = PlayerSettings.Android.bundleVersionCode,
                ["minSdkVersion"] = (int)PlayerSettings.Android.minSdkVersion,
                ["targetSdkVersion"] = (int)PlayerSettings.Android.targetSdkVersion,
                ["scriptingBackend"] = PlayerSettings.GetScriptingBackend(android).ToString(),
                ["targetArchitectures"] = PlayerSettings.Android.targetArchitectures.ToString(),
                ["useCustomKeystore"] = PlayerSettings.Android.useCustomKeystore,
                ["keystoreName"] = PlayerSettings.Android.keystoreName,
                ["keystoreAlias"] = PlayerSettings.Android.keyaliasName,
            };
        }

        /// <summary>
        /// Map a comma-separated architecture list to AndroidArchitecture flags.
        /// x86_64 is rejected rather than ignored: the enum member is obsolete from
        /// Unity 6.5 ("X86_64 is no longer supported").
        /// </summary>
        /// <param name="csv">Comma-separated "ARMv7"/"ARM64" (case-insensitive).</param>
        /// <returns>The combined AndroidArchitecture flags.</returns>
        /// <exception cref="ArgumentException">An unknown or unsupported architecture was named.</exception>
        private static AndroidArchitecture ParseArchitectures(string csv)
        {
            var arch = AndroidArchitecture.None;
            foreach (var raw in csv.Split(','))
            {
                var a = raw.Trim();
                if (a.Length == 0) continue;
                if (string.Equals(a, "ARM64", StringComparison.OrdinalIgnoreCase))
                    arch |= AndroidArchitecture.ARM64;
                else if (string.Equals(a, "ARMv7", StringComparison.OrdinalIgnoreCase))
                    arch |= AndroidArchitecture.ARMv7;
                else if (string.Equals(a, "x86_64", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("x86_64 is not supported on this Unity version (Android X86_64 was retired in Unity 6.5); use ARMv7 and/or ARM64.");
                else
                    throw new ArgumentException($"unknown architecture: {a} (expected ARMv7 or ARM64)");
            }
            if (arch == AndroidArchitecture.None)
                throw new ArgumentException("targetArchitectures named no valid architecture (expected ARMv7 and/or ARM64)");
            return arch;
        }
    }
}
