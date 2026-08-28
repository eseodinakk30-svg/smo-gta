using UnityEngine;
using SanMonica.Characters;
using SanMonica.Core;

namespace SanMonica.AI
{
    /// <summary>
    /// The AI level of detail budget: full behaviour up close, reduced thinking
    /// in the middle distance and a cheap drifting simulation far away. This is
    /// what lets the city keep a hundred agents alive on a phone.
    /// </summary>
    public class AILodManager : MonoBehaviour
    {
        [Header("Distance bands (metres)")]
        public float FullDetail = 50f;
        public float Reduced = 150f;

        [Header("Budget")]
        public int PedsPerFrame = 12;

        private PedFactory _factory;
        private readonly RoundRobinScheduler _scheduler = new RoundRobinScheduler();

        public int FullDetailCount { get; private set; }
        public int ReducedCount { get; private set; }
        public int SimulatedCount { get; private set; }

        public void Initialize(PedFactory factory) { _factory = factory; }

        public void ApplyQuality(float fullDetail, float reduced, int perFrame)
        {
            FullDetail = fullDetail;
            Reduced = reduced;
            PedsPerFrame = Mathf.Max(4, perFrame);
        }

        private void Update()
        {
            if (_factory == null) return;
            var peds = _factory.ActivePeds;
            if (peds.Count == 0) { FullDetailCount = ReducedCount = SimulatedCount = 0; return; }

            Vector3 player = Services.PlayerPosition;
            _scheduler.Slice(peds.Count, PedsPerFrame, out int start, out int count);

            int full = 0, reduced = 0, simulated = 0;
            for (int i = 0; i < count; i++)
            {
                var brain = peds[start + i];
                if (brain == null) continue;
                float distance = Vector3.Distance(brain.transform.position, player);
                int lod = distance < FullDetail ? 0 : (distance < Reduced ? 1 : 2);
                if (brain.Lod != lod) brain.SetLod(lod);
            }

            // Cheap running tally for the debug overlay.
            for (int i = 0; i < peds.Count; i++)
            {
                var brain = peds[i];
                if (brain == null) continue;
                if (brain.Lod == 0) full++;
                else if (brain.Lod == 1) reduced++;
                else simulated++;
            }
            FullDetailCount = full;
            ReducedCount = reduced;
            SimulatedCount = simulated;
        }
    }
}
