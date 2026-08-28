using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Optimization
{
    /// <summary>
    /// Frame timing, memory and world statistics. Feeds the auto quality
    /// controller and the on-screen developer overlay.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        public float SmoothedFps { get; private set; } = 60f;
        public float WorstFps { get; private set; } = 60f;
        public float FrameTimeMs { get; private set; }
        public long ManagedMemoryMb { get; private set; }
        public string Summary { get; private set; } = "";

        private float _accumulator;
        private int _frames;
        private float _worstWindow = 999f;
        private float _summaryTimer;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            _accumulator += dt;
            _frames++;
            float instant = 1f / dt;
            if (instant < _worstWindow) _worstWindow = instant;

            if (_accumulator >= 0.5f)
            {
                SmoothedFps = _frames / _accumulator;
                WorstFps = _worstWindow;
                FrameTimeMs = 1000f / Mathf.Max(1f, SmoothedFps);
                _accumulator = 0f;
                _frames = 0;
                _worstWindow = 999f;
            }

            _summaryTimer -= dt;
            if (_summaryTimer <= 0f)
            {
                _summaryTimer = 1f;
                ManagedMemoryMb = System.GC.GetTotalMemory(false) / (1024 * 1024);
                BuildSummary();
            }
        }

        private void BuildSummary()
        {
            var streamer = Services.Streamer;
            var population = Services.Population;
            var traffic = Services.Traffic;
            var lod = Services.AiLod;

            Summary =
                Mathf.RoundToInt(SmoothedFps) + " fps (" + FrameTimeMs.ToString("F1") + " ms)" +
                "   •   " + ManagedMemoryMb + " MB managed" +
                "   •   chunks " + (streamer != null ? streamer.LoadedChunks : 0) +
                " (+" + (streamer != null ? streamer.PendingChunks : 0) + " queued)" +
                "\npedestrians " + (population != null ? population.PedCount : 0) +
                " [" + (lod != null ? lod.FullDetailCount + "/" + lod.ReducedCount + "/" + lod.SimulatedCount : "0/0/0") + "]" +
                "   •   traffic " + (traffic != null ? traffic.TrafficCount : 0) +
                "   •   parked " + (traffic != null ? traffic.ParkedCount : 0);
        }
    }
}
