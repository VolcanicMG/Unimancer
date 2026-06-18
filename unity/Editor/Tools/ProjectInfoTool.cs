using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Reports project + build settings: Unity version, product/company names,
    /// data path, active build target, scripting backend, color space, batch
    /// mode, and the count of locally-listed packages from the manifest. Runs
    /// synchronously on the main thread.
    /// </summary>
    public class ProjectInfoTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "project_info";

        /// <inheritdoc />
        public override string Description =>
            "Report project info: unityVersion, productName, companyName, dataPath, activeBuildTarget, scriptingBackend, colorSpace, batchMode, installedPackageCount.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Read project, player, and build settings.
        /// </summary>
        /// <param name="parameters">Ignored (no parameters).</param>
        /// <returns>Project info fields, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var activeTarget = EditorUserBuildSettings.activeBuildTarget;
                var targetGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
                var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(targetGroup);
                var scriptingBackend = PlayerSettings.GetScriptingBackend(namedTarget).ToString();

                var info = new JObject
                {
                    ["unityVersion"] = Application.unityVersion,
                    ["productName"] = Application.productName,
                    ["companyName"] = Application.companyName,
                    ["dataPath"] = Application.dataPath,
                    ["activeBuildTarget"] = activeTarget.ToString(),
                    ["scriptingBackend"] = scriptingBackend,
                    ["colorSpace"] = PlayerSettings.colorSpace.ToString(),
                    ["batchMode"] = Application.isBatchMode,
                };

                // installedPackageCount is best-effort: count entries in the
                // project manifest's dependencies. Avoids an async UPM round-trip.
                try
                {
                    var manifestPath = System.IO.Path.Combine(
                        System.IO.Directory.GetParent(Application.dataPath).FullName,
                        "Packages", "manifest.json");
                    if (System.IO.File.Exists(manifestPath))
                    {
                        var manifest = JObject.Parse(System.IO.File.ReadAllText(manifestPath));
                        if (manifest["dependencies"] is JObject deps)
                            info["installedPackageCount"] = deps.Count;
                    }
                }
                catch
                {
                    // Manifest read is optional; omit the field on failure.
                }

                return info;
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
