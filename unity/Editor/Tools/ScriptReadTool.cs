using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// Reads the text content of a file under Assets/. Read-only; still validated
    /// to stay within the project tree for consistency. Runs synchronously.
    /// </summary>
    public class ScriptReadTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "script_read";

        /// <inheritdoc />
        public override string Description => "Read a file under Assets/ and return its text content.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Read the file at the given path.
        /// </summary>
        /// <param name="parameters">path (required, under Assets/).</param>
        /// <returns>{ path, content }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                // Path-safety guard: confine reads to the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                if (!File.Exists(path))
                    return new JObject { ["error"] = $"file not found: {path}" };

                var content = File.ReadAllText(path);
                return new JObject { ["path"] = path, ["content"] = content };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
