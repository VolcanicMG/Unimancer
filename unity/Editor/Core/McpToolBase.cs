using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// Base class for every Unimancer Editor-side tool. A tool exposes a name +
    /// description and implements either a synchronous <see cref="Execute"/> or,
    /// for work that must run on the Unity main thread, an asynchronous
    /// <see cref="ExecuteAsync"/> (set <see cref="IsAsync"/> to true).
    /// </summary>
    public abstract class McpToolBase
    {
        /// <summary>Unique tool name, matching the MCP tool name on the Node side.</summary>
        public abstract string Name { get; }

        /// <summary>Human/LLM-facing description of what the tool does.</summary>
        public abstract string Description { get; }

        /// <summary>True if the tool overrides <see cref="ExecuteAsync"/> instead of <see cref="Execute"/>.</summary>
        public virtual bool IsAsync => false;

        /// <summary>Run a fast, synchronous tool on the socket thread.</summary>
        /// <param name="parameters">Tool parameters as a JObject.</param>
        /// <returns>The result payload as a JObject.</returns>
        public virtual JObject Execute(JObject parameters)
        {
            throw new NotImplementedException($"{Name}: override Execute or set IsAsync and override ExecuteAsync.");
        }

        /// <summary>Run a tool that needs the main thread or long-running work.</summary>
        /// <param name="parameters">Tool parameters as a JObject.</param>
        /// <param name="tcs">Completion source to resolve with the result or fault.</param>
        public virtual void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            tcs.TrySetException(new NotImplementedException($"{Name}: ExecuteAsync must be overridden when IsAsync is true."));
        }
    }
}
