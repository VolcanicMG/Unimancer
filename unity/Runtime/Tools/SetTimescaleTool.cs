using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Sets Time.timeScale on the RUNNING game: 0 pauses, 1 is normal, &gt;1 runs
    /// fast / for fast-forwarding tests.
    /// </summary>
    public class SetTimescaleTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_set_timescale";

        /// <inheritdoc/>
        public override string Description =>
            "Set Time.timeScale on the running game. params: timeScale (number >= 0). 0 = pause, 1 = normal, >1 = fast-forward. Returns { timeScale }.";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var timeScale = parameters["timeScale"]?.ToObject<float>() ?? 1f;
                if (timeScale < 0f) timeScale = 0f; // guard: negative timeScale is invalid.
                Time.timeScale = timeScale;
                return new JObject { ["timeScale"] = Time.timeScale };
            }
            catch (System.Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
