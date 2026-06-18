using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// Recursively searches file contents under a project folder for a literal or
    /// regex query and returns matching lines. Pure file I/O (Directory.Enumerate
    /// + read) — does not touch AssetDatabase. Runs synchronously.
    /// </summary>
    public class ScriptFindInFilesTool : McpToolBase
    {
        /// <summary>Max characters of a matched line returned before trimming.</summary>
        private const int MaxLineLength = 200;

        /// <inheritdoc />
        public override string Name => "script_find_in_files";

        /// <inheritdoc />
        public override string Description =>
            "Recursively search file contents under a folder for a query (literal or regex), filtered by extension.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Scan files and collect matches.
        /// </summary>
        /// <param name="parameters">query (required); regex, extensions, folder, maxResults (optional).</param>
        /// <returns>{ matches: [{ path, line, text }], count, truncated }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var query = parameters["query"]?.ToString();
                if (string.IsNullOrEmpty(query))
                    return new JObject { ["error"] = "query is required" };

                var useRegex = parameters["regex"]?.ToObject<bool>() ?? false;
                var folder = parameters["folder"]?.ToString();
                if (string.IsNullOrEmpty(folder)) folder = "Assets";
                var maxResults = parameters["maxResults"]?.ToObject<int>() ?? 100;
                if (maxResults <= 0) maxResults = 100;

                // Path-safety guard: confine scanning to the project's Assets/ tree.
                var guard = PathGuard.Validate(folder);
                if (guard != null) return guard;

                if (!Directory.Exists(folder))
                    return new JObject { ["error"] = $"folder not found: {folder}" };

                var extensions = (parameters["extensions"] as JArray)?
                    .Select(e => e.ToString())
                    .ToArray();
                if (extensions == null || extensions.Length == 0)
                    extensions = new[] { ".cs" };

                Regex compiled = null;
                if (useRegex)
                    compiled = new Regex(query, RegexOptions.Compiled, TimeSpan.FromSeconds(2)); // bound backtracking (ReDoS)

                var matches = new JArray();
                var truncated = false;

                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    if (!extensions.Any(x => string.Equals(x, ext, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    string[] lines;
                    try { lines = File.ReadAllLines(file); }
                    catch { continue; } // skip unreadable/binary files

                    // Normalize to forward-slash project-relative style for output.
                    var reportPath = file.Replace('\\', '/');

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        var hit = useRegex
                            ? compiled.IsMatch(line)
                            : line.IndexOf(query, StringComparison.Ordinal) >= 0;
                        if (!hit) continue;

                        var text = line.Trim();
                        if (text.Length > MaxLineLength)
                            text = text.Substring(0, MaxLineLength) + "…";

                        matches.Add(new JObject
                        {
                            ["path"] = reportPath,
                            ["line"] = i + 1,
                            ["text"] = text,
                        });

                        if (matches.Count >= maxResults)
                        {
                            truncated = true;
                            break;
                        }
                    }

                    if (truncated) break;
                }

                return new JObject
                {
                    ["matches"] = matches,
                    ["count"] = matches.Count,
                    ["truncated"] = truncated,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
