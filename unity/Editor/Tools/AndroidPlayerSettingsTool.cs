using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Unimancer
{
    /// <summary>
    /// Gets or sets Android PlayerSettings: application identifier, version
    /// name/code, min/target SDK levels, scripting backend, and target CPU
    /// architectures. Uses the Unity 6.x NamedBuildTarget-based APIs.
    /// </summary>
    public class AndroidPlayerSettingsTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "android_player_settings";

        /// <inheritdoc />
        public override string Description =>
            "Get or set Android PlayerSettings (app id, version, SDK levels, scripting backend, target architectures).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Read or apply Android player settings depending on the 'action' field.
        /// </summary>
        /// <param name="parameters">action ('get'|'set') plus optional setting fields.</param>
        /// <returns>The current/updated settings, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var action = parameters["action"]?.ToString() ?? "get";
                if (action == "set")
                    ApplySettings(parameters);
                return ReadSettings();
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>Apply only the fields present in <paramref name="p"/>.</summary>
        /// <param name="p">Parameters JObject; missing fields are left untouched.</param>
        private static void ApplySettings(JObject p)
        {
            var android = NamedBuildTarget.Android;

            if (p["applicationIdentifier"] != null)
                PlayerSettings.SetApplicationIdentifier(android, p["applicationIdentifier"].ToString());

            if (p["bundleVersion"] != null)
                PlayerSettings.bundleVersion = p["bundleVersion"].ToString();

            if (p["bundleVersionCode"] != null)
                PlayerSettings.Android.bundleVersionCode = p["bundleVersionCode"].ToObject<int>();

            if (p["minSdkVersion"] != null)
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)p["minSdkVersion"].ToObject<int>();

            if (p["targetSdkVersion"] != null)
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)p["targetSdkVersion"].ToObject<int>();

            if (p["scriptingBackend"] != null)
            {
                var backend = p["scriptingBackend"].ToString() == "Mono2x"
                    ? ScriptingImplementation.Mono2x
                    : ScriptingImplementation.IL2CPP;
                PlayerSettings.SetScriptingBackend(android, backend);
            }

            if (p["targetArchitectures"] is JArray archArr)
                PlayerSettings.Android.targetArchitectures = ParseArchitectures(archArr);
        }

        /// <summary>Map the requested architecture names to an AndroidArchitecture flag set.</summary>
        /// <param name="archArr">Array of "ARM64"|"ARMv7" (X86_64 removed in Unity 6.5).</param>
        /// <returns>The combined AndroidArchitecture flags.</returns>
        private static AndroidArchitecture ParseArchitectures(JArray archArr)
        {
            var arch = AndroidArchitecture.None;
            foreach (var a in archArr)
            {
                switch (a.ToString())
                {
                    case "ARM64": arch |= AndroidArchitecture.ARM64; break;
                    case "ARMv7": arch |= AndroidArchitecture.ARMv7; break;
                }
            }
            return arch;
        }

        /// <summary>Read the current Android player settings into a JObject.</summary>
        /// <returns>A JObject of the current values.</returns>
        private static JObject ReadSettings()
        {
            var android = NamedBuildTarget.Android;
            return new JObject
            {
                ["applicationIdentifier"] = PlayerSettings.GetApplicationIdentifier(android),
                ["bundleVersion"] = PlayerSettings.bundleVersion,
                ["bundleVersionCode"] = PlayerSettings.Android.bundleVersionCode,
                ["minSdkVersion"] = (int)PlayerSettings.Android.minSdkVersion,
                ["targetSdkVersion"] = (int)PlayerSettings.Android.targetSdkVersion,
                ["scriptingBackend"] = PlayerSettings.GetScriptingBackend(android).ToString(),
                ["targetArchitectures"] = PlayerSettings.Android.targetArchitectures.ToString(),
            };
        }
    }
}
