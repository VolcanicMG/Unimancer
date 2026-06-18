using System;
using Newtonsoft.Json.Linq;
using UnityEngine.Profiling;

namespace Unimancer
{
    /// <summary>
    /// Returns a snapshot of current Unity memory usage in megabytes, read from
    /// the public <see cref="UnityEngine.Profiling.Profiler"/> API. Synchronous —
    /// each Get*Long() call is an instantaneous read on the main thread.
    /// </summary>
    public class ProfilerMemoryTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "profiler_memory";

        /// <inheritdoc />
        public override string Description =>
            "Return current Unity memory usage in MB (allocated, reserved, mono used/heap, graphics driver).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        // Bytes per megabyte; we report MB rounded to two decimals for readability.
        private const double BytesPerMB = 1024d * 1024d;

        /// <summary>
        /// Read the memory counters and convert bytes to MB.
        /// </summary>
        /// <param name="parameters">Unused; this tool takes no parameters.</param>
        /// <returns>{ totalAllocatedMB, totalReservedMB, monoUsedMB, monoHeapMB, gfxDriverMB } or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                return new JObject
                {
                    ["totalAllocatedMB"] = ToMB(Profiler.GetTotalAllocatedMemoryLong()),
                    ["totalReservedMB"] = ToMB(Profiler.GetTotalReservedMemoryLong()),
                    ["monoUsedMB"] = ToMB(Profiler.GetMonoUsedSizeLong()),
                    ["monoHeapMB"] = ToMB(Profiler.GetMonoHeapSizeLong()),
                    ["gfxDriverMB"] = ToMB(Profiler.GetAllocatedMemoryForGraphicsDriver()),
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>Convert a byte count to megabytes, rounded to two decimals.</summary>
        /// <param name="bytes">Raw byte count from a profiler counter.</param>
        /// <returns>The value in MB.</returns>
        private static double ToMB(long bytes) => Math.Round(bytes / BytesPerMB, 2);
    }
}
