using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Unimancer
{
    /// <summary>
    /// Builds the Android player (APK or AAB) via BuildPipeline.BuildPlayer and
    /// returns the BuildReport summary. Runs synchronously on the main thread;
    /// the build itself is the long-running work, covered by the Node request
    /// timeout.
    /// </summary>
    public class AndroidBuildTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "android_build";

        /// <inheritdoc />
        public override string Description =>
            "Build the Android player (APK or AAB) to the given output path and return the BuildReport summary.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Assemble BuildPlayerOptions and run the build.
        /// </summary>
        /// <param name="parameters">outputPath (required), buildType, format, scenes.</param>
        /// <returns>BuildReport summary fields, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var outputPath = parameters["outputPath"]?.ToString();
                if (string.IsNullOrEmpty(outputPath))
                    return new JObject { ["error"] = "outputPath is required" };

                var buildType = parameters["buildType"]?.ToString() ?? "release";
                var format = parameters["format"]?.ToString() ?? "apk";

                // AAB vs APK is controlled by this Editor flag, not BuildOptions.
                EditorUserBuildSettings.buildAppBundle = format == "aab";

                var scenes = ResolveScenes(parameters["scenes"] as JArray);
                if (scenes.Length == 0)
                    return new JObject { ["error"] = "no scenes to build (none provided and none enabled in Build Settings)" };

                var options = BuildOptions.None;
                if (buildType == "development")
                    options |= BuildOptions.Development;

                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = options,
                };

                BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                BuildSummary summary = report.summary;

                return new JObject
                {
                    ["result"] = summary.result.ToString(),
                    ["totalSize"] = summary.totalSize,
                    ["outputPath"] = summary.outputPath,
                    ["errors"] = summary.totalErrors,
                    ["warnings"] = summary.totalWarnings,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Use explicitly supplied scene paths, else fall back to the enabled
        /// scenes from Build Settings.
        /// </summary>
        /// <param name="provided">Optional array of scene asset paths.</param>
        /// <returns>The scene paths to build.</returns>
        private static string[] ResolveScenes(JArray provided)
        {
            if (provided != null && provided.Count > 0)
                return provided.Select(s => s.ToString()).ToArray();

            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }
    }
}
