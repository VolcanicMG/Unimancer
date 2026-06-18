using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Creates a new C# script under Assets/ and imports it. Refuses to overwrite
    /// an existing file unless explicitly told to. Runs synchronously on the main
    /// thread because it touches AssetDatabase.
    /// </summary>
    public class ScriptCreateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "script_create";

        /// <inheritdoc />
        public override string Description =>
            "Create a new C# script under Assets/ (path ending in .cs) with the given content; refuses overwrite unless overwrite=true.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Write the script file and import the asset.
        /// </summary>
        /// <param name="parameters">path (required, Assets/...cs), content (required), overwrite (optional).</param>
        /// <returns>{ created } with the asset path, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var content = parameters["content"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };
                if (content == null)
                    return new JObject { ["error"] = "content is required" };

                // Path-safety guard: keep all writes inside the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                if (!path.EndsWith(".cs", StringComparison.Ordinal))
                    return new JObject { ["error"] = "path must end in .cs" };

                var overwrite = parameters["overwrite"]?.ToObject<bool>() ?? false;
                if (File.Exists(path) && !overwrite)
                    return new JObject { ["error"] = $"file already exists: {path} (pass overwrite=true to replace)" };

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, content);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                return new JObject { ["created"] = path };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
