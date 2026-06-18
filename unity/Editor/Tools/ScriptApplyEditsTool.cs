using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Applies precise, non-clobbering edits to a C# file under Assets/. Two edit
    /// shapes are supported per entry in the <c>edits</c> array:
    /// <list type="bullet">
    ///   <item>line-range: { startLine, endLine, replacement } — 1-based, inclusive;
    ///   replaces those lines with <c>replacement</c> (multi-line or empty).</item>
    ///   <item>find/replace: { find, replace, all? } — literal (non-regex) match;
    ///   replaces first occurrence, or all when <c>all</c> is true.</item>
    /// </list>
    /// Line-range edits are applied first, sorted DESCENDING by startLine, so
    /// applying one edit never shifts the line numbers of edits not yet applied.
    /// Find/replace edits run afterward, in array order. Overlapping line ranges
    /// are rejected. If <c>expectedSha</c> is supplied and the file's current
    /// SHA-256 differs, nothing is written (guards concurrent changes). On success
    /// the asset is reimported. Runs synchronously.
    /// </summary>
    public class ScriptApplyEditsTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "script_apply_edits";

        /// <inheritdoc />
        public override string Description =>
            "Apply precise line-range and/or anchored find/replace edits to a .cs file under Assets/, with optional expectedSha guard against concurrent changes.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>A parsed line-range edit (1-based, inclusive).</summary>
        private struct LineEdit
        {
            public int StartLine;
            public int EndLine;
            public string Replacement;
        }

        /// <summary>A parsed literal find/replace edit.</summary>
        private struct FindEdit
        {
            public string Find;
            public string Replace;
            public bool All;
        }

        /// <summary>
        /// Validate and apply the edits, then reimport on success.
        /// </summary>
        /// <param name="parameters">path (required), expectedSha (optional), edits (required array).</param>
        /// <returns>{ path, newSha256, linesBefore, linesAfter, editsApplied }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                // Path-safety guard: keep edits inside the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    return new JObject { ["error"] = $"path must be a .cs file: {path}" };

                if (!File.Exists(path))
                    return new JObject { ["error"] = $"file not found: {path}" };

                var editsArray = parameters["edits"] as JArray;
                if (editsArray == null || editsArray.Count == 0)
                    return new JObject { ["error"] = "edits must be a non-empty array" };

                // Read original bytes; compute the current sha for the optional guard.
                var originalBytes = File.ReadAllBytes(path);
                var currentSha = ScriptHash.Sha256Hex(originalBytes);

                var expectedSha = parameters["expectedSha"]?.ToString();
                if (!string.IsNullOrEmpty(expectedSha) &&
                    !string.Equals(expectedSha, currentSha, StringComparison.OrdinalIgnoreCase))
                {
                    // Refuse to write — the file changed since the caller fingerprinted it.
                    return new JObject
                    {
                        ["error"] = "expectedSha mismatch: file changed since it was read; aborting without writing",
                        ["expectedSha"] = expectedSha,
                        ["actualSha"] = currentSha,
                    };
                }

                var text = System.Text.Encoding.UTF8.GetString(originalBytes);
                int linesBefore = ScriptHash.CountLines(text);

                // ---- Parse and classify edits -------------------------------------
                var lineEdits = new List<LineEdit>();
                var findEdits = new List<FindEdit>();

                for (int i = 0; i < editsArray.Count; i++)
                {
                    var edit = editsArray[i] as JObject;
                    if (edit == null)
                        return new JObject { ["error"] = $"edit[{i}] must be an object" };

                    bool isLineRange = edit["startLine"] != null || edit["endLine"] != null;
                    bool isFind = edit["find"] != null;

                    if (isLineRange && isFind)
                        return new JObject { ["error"] = $"edit[{i}] mixes line-range and find/replace fields; use one shape per edit" };

                    if (isLineRange)
                    {
                        if (edit["startLine"] == null || edit["endLine"] == null || edit["replacement"] == null)
                            return new JObject { ["error"] = $"edit[{i}] line-range requires startLine, endLine, and replacement" };

                        int start = edit["startLine"].Value<int>();
                        int end = edit["endLine"].Value<int>();

                        if (start < 1 || end < 1)
                            return new JObject { ["error"] = $"edit[{i}] startLine/endLine must be >= 1" };
                        if (end < start)
                            return new JObject { ["error"] = $"edit[{i}] endLine ({end}) must be >= startLine ({start})" };
                        if (end > linesBefore)
                            return new JObject { ["error"] = $"edit[{i}] endLine ({end}) exceeds file line count ({linesBefore})" };

                        lineEdits.Add(new LineEdit
                        {
                            StartLine = start,
                            EndLine = end,
                            Replacement = edit["replacement"].ToString(),
                        });
                    }
                    else if (isFind)
                    {
                        var find = edit["find"].ToString();
                        if (string.IsNullOrEmpty(find))
                            return new JObject { ["error"] = $"edit[{i}] find must be a non-empty string" };

                        findEdits.Add(new FindEdit
                        {
                            Find = find,
                            Replace = edit["replace"]?.ToString() ?? string.Empty,
                            All = edit["all"] != null && edit["all"].Value<bool>(),
                        });
                    }
                    else
                    {
                        return new JObject { ["error"] = $"edit[{i}] must be a line-range ({{startLine,endLine,replacement}}) or find/replace ({{find,replace}})" };
                    }
                }

                // ---- Reject overlapping line ranges -------------------------------
                // Sort ascending only to check adjacency/overlap clearly.
                var sortedForOverlap = lineEdits.OrderBy(e => e.StartLine).ToList();
                for (int i = 1; i < sortedForOverlap.Count; i++)
                {
                    if (sortedForOverlap[i].StartLine <= sortedForOverlap[i - 1].EndLine)
                    {
                        return new JObject
                        {
                            ["error"] =
                                $"overlapping line ranges: [{sortedForOverlap[i - 1].StartLine}-{sortedForOverlap[i - 1].EndLine}] " +
                                $"and [{sortedForOverlap[i].StartLine}-{sortedForOverlap[i].EndLine}]",
                        };
                    }
                }

                int editsApplied = 0;

                // ---- Apply line-range edits, DESCENDING by startLine --------------
                // Splitting on '\n' keeps any '\r' on each line so CRLF endings are
                // preserved on the lines we don't touch.
                if (lineEdits.Count > 0)
                {
                    var lines = new List<string>(text.Split('\n'));
                    foreach (var le in lineEdits.OrderByDescending(e => e.StartLine))
                    {
                        // 1-based inclusive -> 0-based indices.
                        int startIdx = le.StartLine - 1;
                        int count = le.EndLine - le.StartLine + 1;
                        lines.RemoveRange(startIdx, count);
                        // Insert the replacement (may itself contain newlines, or be empty).
                        lines.Insert(startIdx, le.Replacement);
                        editsApplied++;
                    }
                    text = string.Join("\n", lines);
                }

                // ---- Apply find/replace edits, in array order --------------------
                foreach (var fe in findEdits)
                {
                    if (fe.All)
                    {
                        // Ordinal literal replace-all.
                        text = text.Replace(fe.Find, fe.Replace);
                    }
                    else
                    {
                        int idx = text.IndexOf(fe.Find, StringComparison.Ordinal);
                        if (idx < 0)
                            return new JObject { ["error"] = $"find/replace anchor not found: {fe.Find}" };
                        text = text.Substring(0, idx) + fe.Replace + text.Substring(idx + fe.Find.Length);
                    }
                    editsApplied++;
                }

                // ---- Write, reimport, and report ---------------------------------
                File.WriteAllText(path, text);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                return new JObject
                {
                    ["path"] = path,
                    ["newSha256"] = ScriptHash.Sha256HexOf(text),
                    ["linesBefore"] = linesBefore,
                    ["linesAfter"] = ScriptHash.CountLines(text),
                    ["editsApplied"] = editsApplied,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
