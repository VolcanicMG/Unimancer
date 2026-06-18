using System;
using Newtonsoft.Json.Linq;
using UnityEditorInternal;

namespace Unimancer
{
    /// <summary>
    /// Toggles the Editor profiler recording (and optionally deep profiling) via
    /// <see cref="ProfilerDriver"/>. ProfilerDriver lives in the internal-ish
    /// UnityEditorInternal namespace — it's the editor API the Profiler window
    /// itself drives, stable across the versions we target but not part of the
    /// public scripting surface. Synchronous: just flips flags and reads them back.
    /// </summary>
    public class ProfilerEnableTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "profiler_enable";

        /// <inheritdoc />
        public override string Description =>
            "Enable/disable the Editor profiler (ProfilerDriver.enabled) and optionally deep profiling, returning the new state.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Apply the requested enabled / deepProfile flags and return the resulting state.
        /// </summary>
        /// <param name="parameters">{ enabled (required bool), deepProfile? (bool) }.</param>
        /// <returns>{ enabled, deepProfiling } or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var enabledToken = parameters["enabled"];
                if (enabledToken == null || enabledToken.Type == JTokenType.Null)
                    return new JObject { ["error"] = "enabled (bool) is required" };

                // Internal-ish editor API: ProfilerDriver is the same controller the
                // Profiler window uses; flip recording on/off.
                ProfilerDriver.enabled = enabledToken.Value<bool>();

                // Only touch deep profiling if the caller explicitly asked.
                var deepToken = parameters["deepProfile"];
                if (deepToken != null && deepToken.Type != JTokenType.Null)
                    ProfilerDriver.deepProfiling = deepToken.Value<bool>();

                return new JObject
                {
                    ["enabled"] = ProfilerDriver.enabled,
                    ["deepProfiling"] = ProfilerDriver.deepProfiling,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
