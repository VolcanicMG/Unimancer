using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// Reads recent Editor console entries.
    ///
    /// NOTE: This relies on Unity *internal* APIs accessed via reflection
    /// (UnityEditor.LogEntries and UnityEditor.LogEntry). These types are not
    /// part of the public API and their members (GetCount, StartGettingEntries,
    /// GetEntryInternal, EndGettingEntries, and the LogEntry.mode bitmask) can
    /// change between Unity versions. The mode bitmask is matched against the
    /// known error / warning bit ranges to classify each entry; anything not
    /// flagged as error or warning is treated as a plain log.
    /// </summary>
    public class ConsoleReadTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "console_read";

        /// <inheritdoc />
        public override string Description =>
            "Read recent Editor console entries (count, types filter). Returns an array of { type, message }.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        // Mode bitmask constants observed in UnityEditor.LogEntry.mode. These are
        // internal and version-sensitive; see class summary.
        private const int ModeError = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 7) | (1 << 9) | (1 << 13) | (1 << 17) | (1 << 18) | (1 << 21);
        private const int ModeWarning = (1 << 8) | (1 << 5);

        /// <summary>
        /// Pull entries from the internal LogEntries buffer and classify them.
        /// </summary>
        /// <param name="parameters">count (default 100), types (subset of error/warning/log).</param>
        /// <returns>{ entries: [{ type, message }] }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                int count = parameters["count"]?.ToObject<int?>() ?? 100;

                var wantTypes = new HashSet<string>();
                if (parameters["types"] is JArray typesArr)
                    foreach (var t in typesArr) wantTypes.Add(t.ToString());
                bool filterTypes = wantTypes.Count > 0;

                var logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                var logEntryType = Type.GetType("UnityEditor.LogEntry,UnityEditor");
                if (logEntriesType == null || logEntryType == null)
                    return new JObject { ["error"] = "internal LogEntries/LogEntry types not found (Unity version mismatch)" };

                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var startGetting = logEntriesType.GetMethod("StartGettingEntries", flags);
                var endGetting = logEntriesType.GetMethod("EndGettingEntries", flags);
                var getEntryInternal = logEntriesType.GetMethod("GetEntryInternal", flags);

                if (startGetting == null || endGetting == null || getEntryInternal == null)
                    return new JObject { ["error"] = "internal LogEntries methods not found (Unity version mismatch)" };

                var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                int total = (int)startGetting.Invoke(null, null);
                var entries = new JArray();
                try
                {
                    // Iterate newest entries first, capped at count.
                    int start = Math.Max(0, total - count);
                    for (int i = total - 1; i >= start; i--)
                    {
                        var entry = Activator.CreateInstance(logEntryType);
                        getEntryInternal.Invoke(null, new object[] { i, entry });

                        int mode = modeField != null ? (int)modeField.GetValue(entry) : 0;
                        string message = messageField != null ? messageField.GetValue(entry) as string : null;

                        string type = ClassifyMode(mode);
                        if (filterTypes && !wantTypes.Contains(type)) continue;

                        entries.Add(new JObject { ["type"] = type, ["message"] = message ?? "" });
                    }
                }
                finally
                {
                    endGetting.Invoke(null, null);
                }

                return new JObject { ["entries"] = entries };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Map a LogEntry.mode bitmask to "error", "warning", or "log".
        /// </summary>
        /// <param name="mode">The internal mode bitmask.</param>
        /// <returns>The classified entry type.</returns>
        private static string ClassifyMode(int mode)
        {
            if ((mode & ModeError) != 0) return "error";
            if ((mode & ModeWarning) != 0) return "warning";
            return "log";
        }
    }
}
