using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditorInternal;

namespace Unimancer
{
    /// <summary>
    /// Best-effort top time markers for the most recent profiled frame(s), read via
    /// the internal <see cref="ProfilerDriver"/> raw-frame-data API
    /// (GetRawFrameDataView). This is VERSION-FRAGILE editor internals: the
    /// raw-frame-data and marker-iteration surface has shifted across Unity
    /// releases. If the Profiler isn't recording or the data isn't accessible we
    /// return { note, markers: [] } instead of throwing, so the tool degrades
    /// gracefully rather than failing the call.
    /// </summary>
    public class ProfilerTopMarkersTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "profiler_top_markers";

        /// <inheritdoc />
        public override string Description =>
            "Best-effort top time markers for the most recent profiled frame(s) via internal ProfilerDriver raw-frame-data. Returns [{ marker, totalMs, calls }] capped to ~20, or a note if unavailable. Version-fragile.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        // Cap how many markers we return so the payload stays small.
        private const int MaxMarkers = 20;

        // How many recent frames to aggregate over by default.
        private const int DefaultFrames = 1;

        private const string NotRecordingNote = "requires the Profiler window to be recording";

        /// <summary>
        /// Walk the raw frame data for the most recent frame(s), aggregate self time
        /// and call counts per marker name, and return the top markers by time.
        /// </summary>
        /// <param name="parameters">{ frames? } — how many recent frames to aggregate.</param>
        /// <returns>{ markers: [...] } on success, or { note, markers: [] } when unavailable.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                int frames = parameters["frames"]?.Value<int?>() ?? DefaultFrames;
                if (frames < 1) frames = 1;

                int firstFrame = ProfilerDriver.firstFrameIndex;
                int lastFrame = ProfilerDriver.lastFrameIndex;

                // No captured frames means the Profiler isn't recording (or hasn't yet).
                if (lastFrame < 0 || firstFrame < 0 || lastFrame < firstFrame)
                    return new JObject { ["note"] = NotRecordingNote, ["markers"] = new JArray() };

                int startFrame = Math.Max(firstFrame, lastFrame - frames + 1);

                // Aggregate self-time (ns) and call counts per marker name across the
                // requested frame window, summing over all threads in each frame.
                var totalsNs = new Dictionary<string, double>();
                var calls = new Dictionary<string, int>();

                for (int frame = startFrame; frame <= lastFrame; frame++)
                    AccumulateFrame(frame, totalsNs, calls);

                if (totalsNs.Count == 0)
                    return new JObject { ["note"] = NotRecordingNote, ["markers"] = new JArray() };

                // Sort by total time descending and take the top N.
                var ordered = new List<KeyValuePair<string, double>>(totalsNs);
                ordered.Sort((a, b) => b.Value.CompareTo(a.Value));

                var markers = new JArray();
                int emitted = 0;
                foreach (var kv in ordered)
                {
                    if (emitted++ >= MaxMarkers) break;
                    markers.Add(new JObject
                    {
                        ["marker"] = kv.Key,
                        // ns -> ms
                        ["totalMs"] = Math.Round(kv.Value / 1_000_000d, 4),
                        ["calls"] = calls.TryGetValue(kv.Key, out var c) ? c : 0,
                    });
                }

                return new JObject { ["frames"] = lastFrame - startFrame + 1, ["markers"] = markers };
            }
            catch (Exception e)
            {
                // Internal API shapes drift between Unity versions; never throw out
                // of this tool — degrade to a note so the caller still gets a result.
                return new JObject
                {
                    ["note"] = $"{NotRecordingNote} (internal profiler API unavailable: {e.Message})",
                    ["markers"] = new JArray(),
                };
            }
        }

        /// <summary>
        /// Sum self-time and call counts for every sample in a single frame across
        /// all of its threads, using ProfilerDriver.GetRawFrameDataView. Each
        /// RawFrameDataView exposes sample names, self time (ns), and stats. This
        /// per-sample walk is the version-fragile part and is isolated here.
        /// </summary>
        /// <param name="frame">Frame index to read.</param>
        /// <param name="totalsNs">Marker name -> accumulated self time in ns.</param>
        /// <param name="calls">Marker name -> accumulated call/sample count.</param>
        private static void AccumulateFrame(
            int frame,
            Dictionary<string, double> totalsNs,
            Dictionary<string, int> calls)
        {
            // Iterate threads until we hit an invalid view for this frame.
            for (int threadIndex = 0; ; threadIndex++)
            {
                var view = ProfilerDriver.GetRawFrameDataView(frame, threadIndex);
                if (view == null || !view.valid)
                    break;

                int sampleCount = view.sampleCount;
                for (int i = 0; i < sampleCount; i++)
                {
                    // GetSampleName + GetSampleTimeNs are the stable-ish accessors on
                    // RawFrameDataView across the versions we target.
                    string name = view.GetSampleName(i);
                    if (string.IsNullOrEmpty(name)) continue;

                    double selfNs = view.GetSampleTimeNs(i);

                    totalsNs.TryGetValue(name, out var acc);
                    totalsNs[name] = acc + selfNs;

                    calls.TryGetValue(name, out var c);
                    calls[name] = c + 1;
                }
            }
        }
    }
}
