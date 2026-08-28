using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.World;

namespace SanMonica.AI
{
    /// <summary>
    /// Pedestrian navigation built on top of the road graph: pavements run
    /// alongside every street, so a single A* over the street network plus local
    /// steering gives NPCs believable routes across the whole city without
    /// baking a navmesh for 268 square kilometres.
    /// </summary>
    public class NavGraph
    {
        private RoadNetwork _roads;
        private WorldMap _map;
        private readonly List<int> _nodePath = new List<int>(64);

        public void Initialize(RoadNetwork roads, WorldMap map)
        {
            _roads = roads;
            _map = map;
        }

        public bool Ready => _roads != null && _roads.Segments.Count > 0;

        /// <summary>Nearest point on a pavement (or road edge in the countryside).</summary>
        public Vector3 SnapToWalkable(Vector3 position, float searchRadius = 60f)
        {
            if (!Ready) return position;
            var flat = new Vector2(position.x, position.z);
            int seg = _roads.NearestSegment(flat, searchRadius);
            if (seg < 0) return new Vector3(position.x, _map.SampleHeight(position.x, position.z), position.z);

            var s = _roads.Segments[seg];
            RoadNetwork.DistanceToSegment(flat, in s, out float t);
            Vector2 centre = s.Point(t);
            bool left = Vector2.Dot(flat - centre, s.Right) < 0f;
            return _roads.SidewalkPoint(seg, left, t);
        }

        public bool IsOnRoad(Vector3 position, float margin = 0.5f)
            => Ready && _roads.IsOnRoad(new Vector2(position.x, position.z), margin);

        public Vector3 RandomWalkPoint(Vector3 near, float radius, ref Rng rng)
        {
            if (!Ready) return near;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = rng.InsideUnitCircle() * radius;
                Vector3 candidate = near + new Vector3(offset.x, 0f, offset.y);
                if (_map.IsWater(candidate.x, candidate.z)) continue;
                var snapped = SnapToWalkable(candidate, radius);
                if ((snapped - near).sqrMagnitude > 25f) return snapped;
            }
            return SnapToWalkable(near + new Vector3(rng.Range(-radius, radius), 0f, rng.Range(-radius, radius)), radius);
        }

        /// <summary>
        /// Builds a walkable route. Short hops are a straight line; longer trips
        /// route through the street graph and are emitted as pavement waypoints.
        /// </summary>
        public bool FindPath(Vector3 from, Vector3 to, List<Vector3> outPath, float directDistance = 30f)
        {
            outPath.Clear();
            if (!Ready) { outPath.Add(to); return true; }

            if ((to - from).sqrMagnitude < directDistance * directDistance)
            {
                outPath.Add(SnapToWalkable(to, 25f));
                return true;
            }

            int startNode = _roads.NearestNode(new Vector2(from.x, from.z));
            int goalNode = _roads.NearestNode(new Vector2(to.x, to.z));
            if (startNode < 0 || goalNode < 0) { outPath.Add(to); return true; }

            if (!_roads.FindPath(startNode, goalNode, _nodePath, 1600))
            {
                outPath.Add(SnapToWalkable(to, 40f));
                return false;
            }

            for (int i = 0; i < _nodePath.Count; i++)
            {
                var node = _roads.Nodes[_nodePath[i]];
                Vector3 p = new Vector3(node.Pos.x, 0f, node.Pos.y);
                p.y = _map.SampleHeight(p.x, p.z) + 0.15f;
                outPath.Add(p);
            }
            outPath.Add(to);
            return true;
        }

        /// <summary>Route for a vehicle, expressed as lane centre waypoints.</summary>
        public bool FindDrivePath(Vector3 from, Vector3 to, List<Vector3> outPath)
        {
            outPath.Clear();
            if (!Ready) return false;
            int startNode = _roads.NearestNode(new Vector2(from.x, from.z));
            int goalNode = _roads.NearestNode(new Vector2(to.x, to.z));
            if (startNode < 0 || goalNode < 0) return false;
            if (!_roads.FindPath(startNode, goalNode, _nodePath, 2200)) return false;

            for (int i = 0; i < _nodePath.Count - 1; i++)
            {
                int segment = _roads.SegmentBetween(_nodePath[i], _nodePath[i + 1]);
                if (segment < 0) continue;
                var s = _roads.Segments[segment];
                bool forward = s.NodeA == _nodePath[i];
                outPath.Add(_roads.LanePoint(segment, 0, forward, 0.5f));
                outPath.Add(_roads.LanePoint(segment, 0, forward, 1f));
            }
            outPath.Add(to);
            return outPath.Count > 0;
        }

        /// <summary>Local avoidance: nudges a desired direction around obstacles.</summary>
        public static Vector3 AvoidObstacles(Vector3 position, Vector3 desired, float radius, float lookahead, int mask)
        {
            if (desired.sqrMagnitude < 0.0001f) return desired;
            Vector3 dir = desired.normalized;
            Vector3 origin = position + Vector3.up * 0.9f;

            if (!Physics.SphereCast(origin, radius, dir, out var hit, lookahead, mask, QueryTriggerInteraction.Ignore))
                return desired;

            Vector3 right = Vector3.Cross(Vector3.up, dir);
            bool rightBlocked = Physics.SphereCast(origin, radius * 0.8f, (dir + right).normalized, out _, lookahead * 0.8f, mask, QueryTriggerInteraction.Ignore);
            bool leftBlocked = Physics.SphereCast(origin, radius * 0.8f, (dir - right).normalized, out _, lookahead * 0.8f, mask, QueryTriggerInteraction.Ignore);

            Vector3 steer;
            if (!rightBlocked) steer = (dir + right * 1.4f).normalized;
            else if (!leftBlocked) steer = (dir - right * 1.4f).normalized;
            else steer = -dir;

            float blend = Mathf.Clamp01(1f - hit.distance / lookahead);
            return Vector3.Lerp(desired, steer * desired.magnitude, blend);
        }
    }
}
