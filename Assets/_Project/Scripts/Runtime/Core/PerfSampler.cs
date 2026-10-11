using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Collects frame times and the renderer's own counters (batches, SetPass calls, shadow casters, GC) for a few seconds and
    /// summarises them as one log line. Render counters only exist in the editor and development builds; a release player reports n/a for them.
    /// </summary>
    public sealed class PerfSampler : IDisposable
    {
        readonly List<float> frameMs = new List<float>(600);
        readonly List<(string label, ProfilerRecorder recorder, List<long> samples)> counters = new List<(string, ProfilerRecorder, List<long>)>();

        public PerfSampler()
        {
            Add("batches", ProfilerCategory.Render, "Batches Count");
            Add("setPass", ProfilerCategory.Render, "SetPass Calls Count");
            Add("drawCalls", ProfilerCategory.Render, "Draw Calls Count");
            Add("shadowCasters", ProfilerCategory.Render, "Shadow Casters Count");
            Add("gcBytes", ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        void Add(string label, ProfilerCategory category, string stat) =>
            counters.Add((label, ProfilerRecorder.StartNew(category, stat), new List<long>(600)));

        /// <summary>Call once per frame.</summary>
        public void Sample()
        {
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            foreach (var (_, recorder, samples) in counters)
                if (recorder.Valid) samples.Add(recorder.LastValue);
        }

        public int Frames => frameMs.Count;

        public string Summary()
        {
            var text = new StringBuilder();
            if (frameMs.Count > 0)
            {
                var sorted = new List<float>(frameMs);
                sorted.Sort();
                var average = 0f;
                foreach (var ms in sorted) average += ms;
                average /= sorted.Count;
                var p95 = sorted[Mathf.Min(sorted.Count - 1, Mathf.CeilToInt(sorted.Count * 0.95f) - 1)];
                text.Append($"frames={sorted.Count} frameMs avg={average:0.0} p95={p95:0.0} worst={sorted[sorted.Count - 1]:0.0} fps={1000f / Mathf.Max(0.01f, average):0}");
            }
            foreach (var (label, _, samples) in counters)
            {
                if (samples.Count == 0) { text.Append($" {label}=n/a"); continue; }
                long sum = 0, max = 0;
                foreach (var value in samples) { sum += value; max = Math.Max(max, value); }
                text.Append($" {label} avg={sum / samples.Count} max={max}");
            }
            return text.ToString();
        }

        public void Dispose()
        {
            foreach (var (_, recorder, _) in counters) recorder.Dispose();
        }
    }
}
