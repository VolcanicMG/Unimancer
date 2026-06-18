using System;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// Clears the Editor console.
    ///
    /// NOTE: Uses the Unity *internal* API UnityEditor.LogEntries.Clear() via
    /// reflection. This is not part of the public API and can change between
    /// Unity versions.
    /// </summary>
    public class ConsoleClearTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "console_clear";

        /// <inheritdoc />
        public override string Description => "Clear all Editor console entries. Returns { cleared: true }.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Invoke the internal LogEntries.Clear().
        /// </summary>
        /// <param name="parameters">Ignored (no parameters).</param>
        /// <returns>{ cleared: true }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntriesType == null)
                    return new JObject { ["error"] = "internal LogEntries type not found (Unity version mismatch)" };

                var clear = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (clear == null)
                    return new JObject { ["error"] = "internal LogEntries.Clear not found (Unity version mismatch)" };

                clear.Invoke(null, null);
                return new JObject { ["cleared"] = true };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
