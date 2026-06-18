using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Inspects and enables Unity's custom Gradle templates under
    /// Assets/Plugins/Android/. 'enable' best-effort copies Unity's built-in
    /// default template into the project so it can be edited.
    ///
    /// Default templates ship under the Editor install at
    /// {EditorApplicationContentsPath}/PlaybackEngines/AndroidPlayer/Tools/GradleTemplates/.
    /// Exact filenames can vary across Unity 6.x point releases, so 'enable' is
    /// best-effort: if the source default cannot be located it reports that
    /// rather than failing the whole tool.
    /// </summary>
    public class AndroidGradleTemplateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "android_gradle_template";

        /// <inheritdoc />
        public override string Description =>
            "Inspect (status/read) or enable Unity's custom Android Gradle templates under Assets/Plugins/Android/.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        private const string PluginsAndroidDir = "Assets/Plugins/Android";

        /// <summary>
        /// Dispatch the status/read/enable action for the named template.
        /// </summary>
        /// <param name="parameters">template name and action.</param>
        /// <returns>Action-specific result, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var template = parameters["template"]?.ToString() ?? "mainTemplate";
                var action = parameters["action"]?.ToString() ?? "status";

                var projectFile = ProjectFileName(template);
                if (projectFile == null)
                    return new JObject { ["error"] = $"unknown template: {template}" };

                var projectPath = Path.Combine(PluginsAndroidDir, projectFile);

                switch (action)
                {
                    case "status":
                        return new JObject
                        {
                            ["template"] = template,
                            ["path"] = projectPath,
                            ["exists"] = File.Exists(projectPath),
                        };

                    case "read":
                        if (!File.Exists(projectPath))
                            return new JObject { ["template"] = template, ["exists"] = false };
                        return new JObject
                        {
                            ["template"] = template,
                            ["exists"] = true,
                            ["contents"] = File.ReadAllText(projectPath),
                        };

                    case "enable":
                        return Enable(template, projectFile, projectPath);

                    default:
                        return new JObject { ["error"] = $"unknown action: {action}" };
                }
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Copy Unity's default template into the project if it's not already there.
        /// </summary>
        /// <param name="template">Logical template key.</param>
        /// <param name="projectFile">The project-side file name.</param>
        /// <param name="projectPath">Full project-relative destination path.</param>
        /// <returns>Result describing whether the template was created.</returns>
        private static JObject Enable(string template, string projectFile, string projectPath)
        {
            if (File.Exists(projectPath))
                return new JObject { ["template"] = template, ["path"] = projectPath, ["alreadyExists"] = true };

            var source = DefaultTemplateSourcePath(projectFile);
            if (source == null || !File.Exists(source))
            {
                return new JObject
                {
                    ["template"] = template,
                    ["created"] = false,
                    ["note"] = "Default template not found in this Editor install; create it from Project Settings > Player > Publishing Settings.",
                };
            }

            Directory.CreateDirectory(PluginsAndroidDir);
            File.Copy(source, projectPath);
            AssetDatabase.Refresh();
            return new JObject { ["template"] = template, ["path"] = projectPath, ["created"] = true };
        }

        /// <summary>Map a template key to its project-side file name.</summary>
        /// <param name="template">Logical template key.</param>
        /// <returns>The file name, or null if unknown.</returns>
        private static string ProjectFileName(string template)
        {
            switch (template)
            {
                case "mainTemplate": return "mainTemplate.gradle";
                case "settingsTemplate": return "settingsTemplate.gradle";
                case "gradleProperties": return "gradleTemplate.properties";
                case "baseProjectTemplate": return "baseProjectTemplate.gradle";
                default: return null;
            }
        }

        /// <summary>
        /// Best-effort path to Unity's bundled default template for the given file.
        /// </summary>
        /// <param name="projectFile">The project-side file name.</param>
        /// <returns>An absolute source path, or null if it can't be derived.</returns>
        private static string DefaultTemplateSourcePath(string projectFile)
        {
            var gradleTemplates = Path.Combine(
                EditorApplication.applicationContentsPath,
                "PlaybackEngines", "AndroidPlayer", "Tools", "GradleTemplates");
            if (!Directory.Exists(gradleTemplates)) return null;
            return Path.Combine(gradleTemplates, projectFile);
        }
    }
}
