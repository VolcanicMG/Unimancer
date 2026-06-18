using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Samples a set of named profiler counters over N Editor frames using the
    /// public <see cref="ProfilerRecorder"/> API (Unity 2020.2+), then returns
    /// per-stat last/avg/max with a unit. Asynchronous because it must collect one
    /// sample per <see cref="EditorApplication.update"/> tick across multiple
    /// frames before it can answer; the bridge enqueues ExecuteAsync on the main
    /// thread, and we subscribe our own update callback to drive the sampling.
    /// </summary>
    public class ProfilerFrameStatsTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "profiler_frame_stats";

        /// <inheritdoc />
        public override string Description =>
            "Sample named profiler counters (Main Thread, GC.Alloc, Draw Calls, Batches, Triangles, Vertices) over N Editor frames and return per-stat last/avg/max with units. Invalid counters are skipped.";

        /// <inheritdoc />
        public override bool IsAsync => true;

        // Default number of Editor frames to sample over when `frames` is omitted.
        private const int DefaultFrames = 60;

        /// <summary>
        /// A counter we want to record: a ProfilerCategory + stat name, plus the
        /// unit we report it as. Stat names vary slightly across Unity versions, so
        /// each recorder is created best-effort and skipped if not Valid.
        /// </summary>
        private readonly struct StatSpec
        {
            public readonly ProfilerCategory Category;
            public readonly string StatName;
            public readonly string Unit;

            public StatSpec(ProfilerCategory category, string statName, string unit)
            {
                Category = category;
                StatName = statName;
                Unit = unit;
            }
        }

        // The counters we attempt to sample. Names follow Unity's built-in profiler
        // counter naming; any that resolve to an invalid recorder on this version
        // are reported under "skipped" rather than failing the call.
        private static readonly StatSpec[] Specs =
        {
            new StatSpec(ProfilerCategory.Internal, "Main Thread", "ns"),
            new StatSpec(ProfilerCategory.Memory, "GC.Alloc", "bytes"),
            new StatSpec(ProfilerCategory.Memory, "GC Allocated In Frame", "bytes"),
            new StatSpec(ProfilerCategory.Render, "Draw Calls Count", "count"),
            new StatSpec(ProfilerCategory.Render, "Batches Count", "count"),
            new StatSpec(ProfilerCategory.Render, "Triangles Count", "count"),
            new StatSpec(ProfilerCategory.Render, "Vertices Count", "count"),
        };

        /// <summary>
        /// Start sampling and resolve <paramref name="tcs"/> once `frames` frames
        /// have elapsed. All work runs on the Unity main thread.
        /// </summary>
        /// <param name="parameters">{ frames? } — number of frames to sample.</param>
        /// <param name="tcs">Resolved with per-stat results, or faulted on error.</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                int frames = parameters["frames"]?.Value<int?>() ?? DefaultFrames;
                if (frames < 1) frames = 1;

                // Create recorders for every spec; track which were valid vs skipped.
                var recorders = new List<(StatSpec spec, ProfilerRecorder rec)>();
                var skipped = new JArray();
                foreach (var spec in Specs)
                {
                    // Capacity = frames so we can compute avg/max over the window.
                    var rec = ProfilerRecorder.StartNew(spec.Category, spec.StatName, frames);
                    if (rec.Valid)
                        recorders.Add((spec, rec));
                    else
                    {
                        rec.Dispose();
                        skipped.Add(spec.StatName);
                    }
                }

                int sampled = 0;
                EditorApplication.CallbackFunction tick = null;
                tick = () =>
                {
                    sampled++;
                    if (sampled < frames) return;

                    // Done: detach, summarize, dispose, and resolve.
                    EditorApplication.update -= tick;
                    try
                    {
                        var stats = new JArray();
                        foreach (var (spec, rec) in recorders)
                        {
                            Summarize(rec, spec, stats);
                            rec.Dispose();
                        }

                        tcs.TrySetResult(new JObject
                        {
                            ["framesSampled"] = sampled,
                            ["stats"] = stats,
                            ["skipped"] = skipped,
                        });
                    }
                    catch (Exception e)
                    {
                        foreach (var (_, rec) in recorders) rec.Dispose();
                        tcs.TrySetException(e);
                    }
                };

                EditorApplication.update += tick;
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        }

        /// <summary>
        /// Compute last/avg/max for one recorder's sample buffer and append it to
        /// <paramref name="stats"/>.
        /// </summary>
        /// <param name="rec">The recorder, already populated with samples.</param>
        /// <param name="spec">The spec describing this counter.</param>
        /// <param name="stats">Output array to append the summary JObject to.</param>
        private static void Summarize(ProfilerRecorder rec, StatSpec spec, JArray stats)
        {
            int count = rec.Count;
            long last = rec.LastValue;
            double sum = 0;
            long max = 0;

            // ProfilerRecorder exposes a ring buffer; CopyTo gives us all samples
            // currently held (up to capacity) for avg/max.
            if (count > 0)
            {
                var samples = new List<ProfilerRecorderSample>(count);
                rec.CopyTo(samples);
                foreach (var s in samples)
                {
                    sum += s.Value;
                    if (s.Value > max) max = s.Value;
                }
            }

            stats.Add(new JObject
            {
                ["name"] = spec.StatName,
                ["lastValue"] = last,
                ["avg"] = count > 0 ? Math.Round(sum / count, 2) : 0,
                ["max"] = max,
                ["unit"] = spec.Unit,
            });
        }
    }
}
