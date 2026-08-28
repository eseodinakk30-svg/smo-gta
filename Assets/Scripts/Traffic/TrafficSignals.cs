using System.Collections.Generic;
using UnityEngine;
using SanMonica.World;

namespace SanMonica.Traffic
{
    /// <summary>
    /// City-wide traffic light controller. Every signalled junction runs the
    /// same fixed cycle with a per-junction offset, which keeps thousands of
    /// intersections in sync for free - no per-light state to simulate.
    /// </summary>
    public class TrafficSignals
    {
        public float GreenDuration = 16f;
        public float AmberDuration = 3f;

        private RoadNetwork _roads;
        private float _time;

        public void Initialize(RoadNetwork roads) { _roads = roads; }

        public void Tick(float dt) { _time += dt; }

        private float CycleLength => (GreenDuration + AmberDuration) * 2f;

        /// <summary>0 = north/south green, 1 = east/west green.</summary>
        public int PhaseAt(int nodeIndex)
        {
            float offset = (nodeIndex * 37 % 100) / 100f * CycleLength;
            float t = Mathf.Repeat(_time + offset, CycleLength);
            return t < GreenDuration + AmberDuration ? 0 : 1;
        }

        public bool IsAmber(int nodeIndex)
        {
            float offset = (nodeIndex * 37 % 100) / 100f * CycleLength;
            float t = Mathf.Repeat(_time + offset, CycleLength);
            float inPhase = t < GreenDuration + AmberDuration ? t : t - (GreenDuration + AmberDuration);
            return inPhase > GreenDuration;
        }

        /// <summary>Is the approach along this segment allowed to cross the junction?</summary>
        public bool IsGreen(int nodeIndex, int segmentIndex)
        {
            if (_roads == null || nodeIndex < 0 || nodeIndex >= _roads.Nodes.Count) return true;
            var node = _roads.Nodes[nodeIndex];
            if (!node.HasTrafficLight) return true;
            if (segmentIndex < 0 || segmentIndex >= _roads.Segments.Count) return true;

            var seg = _roads.Segments[segmentIndex];
            // Axis 0 when the road runs mostly north/south.
            int axis = Mathf.Abs(seg.Dir.y) >= Mathf.Abs(seg.Dir.x) ? 0 : 1;
            if (PhaseAt(nodeIndex) != axis) return false;
            return !IsAmber(nodeIndex);
        }

        public Color LightColour(int nodeIndex, int segmentIndex)
        {
            if (IsGreen(nodeIndex, segmentIndex)) return new Color(0.15f, 0.9f, 0.25f);
            if (IsAmber(nodeIndex)) return new Color(1f, 0.75f, 0.1f);
            return new Color(0.95f, 0.15f, 0.12f);
        }
    }
}
