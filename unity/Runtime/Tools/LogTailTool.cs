using Newtonsoft.Json.Linq;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Returns the most recent log lines captured by RuntimeBridge from the RUNNING
    /// game (Debug.Log/Warning/Error). Lightweight way to see what the game is
    /// printing without an attached console.
    /// </summary>
    public class LogTailTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_log_tail";

        /// <inheritdoc/>
        public override string Description =>
            "Tail recent log lines from the running game. params: count? (int, default 50). Returns { logs: [...] } (newest last).";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var count = parameters["count"]?.ToObject<int>() ?? 50;
                var logs = new JArray();
                foreach (var line in RuntimeBridge.RecentLogs(count))
                    logs.Add(line);
                return new JObject { ["logs"] = logs };
            }
            catch (System.Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
