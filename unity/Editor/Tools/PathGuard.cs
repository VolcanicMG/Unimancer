using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// Shared path-safety guard for file-writing/deleting tools.
    ///
    /// SAFETY: any tool that creates, edits, or deletes files must keep its target
    /// inside the project's Assets/ tree. This rejects paths that escape via ".."
    /// or that are not rooted at "Assets/", preventing writes outside the project.
    /// </summary>
    public static class PathGuard
    {
        /// <summary>
        /// Validate that an asset path is safe to write/delete.
        /// </summary>
        /// <param name="path">The candidate asset path (forward-slash, project-relative).</param>
        /// <returns>Null if the path is allowed; otherwise a { error } JObject to return.</returns>
        public static JObject Validate(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new JObject { ["error"] = "path is required" };

            // Normalize backslashes so the checks below cannot be sidestepped on Windows.
            var normalized = path.Replace('\\', '/');

            if (normalized.Contains(".."))
                return new JObject { ["error"] = $"path may not contain '..': {path}" };

            if (!normalized.StartsWith("Assets/") && normalized != "Assets")
                return new JObject { ["error"] = $"path must be under Assets/: {path}" };

            return null;
        }
    }
}
