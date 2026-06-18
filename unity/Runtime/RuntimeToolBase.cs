using Newtonsoft.Json.Linq;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Base class for runtime (in-player / Play-mode) Unimancer tools. Unlike the
    /// Editor McpToolBase, these use ONLY UnityEngine APIs so they compile into
    /// builds. Discovered by reflection and dispatched by Name by RuntimeBridge.
    /// Execution is marshalled onto the Unity main thread, so Execute may freely
    /// touch the scene.
    /// </summary>
    public abstract class RuntimeToolBase
    {
        /// <summary>Unique tool name (matches the JS tool name on the Node side).</summary>
        public abstract string Name { get; }

        /// <summary>What the tool does, for an LLM choosing to call it.</summary>
        public abstract string Description { get; }

        /// <summary>Run the tool on the main thread and return its result JObject.</summary>
        /// <param name="parameters">Tool parameters.</param>
        /// <returns>Result payload, or a { error } object on failure.</returns>
        public abstract JObject Execute(JObject parameters);
    }
}
