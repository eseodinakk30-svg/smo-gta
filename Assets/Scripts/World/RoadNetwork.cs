using System.Collections.Generic;
using UnityEngine;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    public enum RoadKind { Street, Avenue, Highway, Rural, Dirt, Runway, Taxiway, Alley }

    public struct RoadSegment
    {
        public Vector2 A, B;
        public Vector2 Dir;          // normalised A -> B
        public float Length;
        public RoadKind Kind;
        public float HalfWidth;      // asphalt half width
        public int LanesPerDirection;
        public bool OneWay;
        public int NodeA, NodeB;
        public bool HasSidewalk;
        public float Jitter;         // tiny height offset that prevents z-fighting at junctions

        /// <summary>
        /// Set on the spans that carry a road over water. A bridge does not
        /// follow the ground - it meets the banks at each end and arches over
        /// whatever is between - so its height comes from DeckA, DeckB and Arch
        /// rather than from the height field.
        /// </summary>
        public bool IsBridge;
        public float DeckA, DeckB, Arch;

        public Vector2 Point(float t) => Vector2.Lerp(A, B, t);
        public Vector2 Right => new Vector2(Dir.y, -Dir.x);

        /// <summary>Deck height along a bridge; meaningless for an ordinary road.</summary>
        public float DeckAt(float t) => Mathf.Lerp(DeckA, DeckB, t) + Arch * Mathf.Sin(Mathf.PI * t);
    }

    public class RoadNode
    {
        public Vector2 Pos;
        public readonly List<int> Segments = new List<int>(4);
        public bool IsIntersection;
        public bool HasTrafficLight;
        public RoadKind DominantKind;
    }

    /// <summary>
    /// The street plan of San Monica. Produces the geometry that chunks render,
    /// the lane graph that traffic drives on, and the walkable graph that
    /// pedestrians and the police use for pathfinding.
    /// </summary>
    public class RoadNetwork
    {
        public readonly List<RoadSegment> Segments = new List<RoadSegment>();
        public readonly List<RoadNode> Nodes = new List<RoadNode>();

        private readonly WorldConfig _cfg;
        private readonly WorldMap _map;
        private readonly Dictionary<long, int> _nodeLookup = new Dictionary<long, int>();
        private Dictionary<long, List<int>> _spatial;
        private float _cellSize = 256f;

        public float LaneWidth => _cfg.laneWidth;

        public RoadNetwork(WorldConfig cfg, WorldMap map)
        {
            _cfg = cfg;
            _map = map;
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        public void Build()
        {
            Segments.Clear(); Nodes.Clear(); _nodeLookup.Clear();
            BuildHighways();
            BuildUrbanGrid();
            BuildRuralRoads();
            BuildAirport();
            StitchComponents();
            BuildSpatialIndex();
            MarkIntersections();
        }

        /// <summary>
        /// The four generators lay their roads independently, and where their
        /// ends do not meet the city comes out as separate islands of tarmac -
        /// fifteen of them, the largest holding under a third of the junctions.
        /// Traffic then has nowhere to go and nothing that navigates by road can
        /// cross town. This joins them up: each stranded network gets a link to
        /// the nearest node of a bigger one, provided the ground between stays
        /// above water, so genuine islands are left for a bridge or a boat.
        /// </summary>
        private void StitchComponents()
        {
            const float MaxLink = 420f;
            const float MaxBridge = 520f;

            for (int pass = 0; pass < 24; pass++)
            {
                var component = LabelComponents(out int count);
                if (count <= 1) return;

                // Size each component so the smaller ones are the ones that move.
                var size = new int[count];
                for (int i = 0; i < component.Length; i++) size[component[i]]++;
                int biggest = 0;
                for (int i = 1; i < count; i++) if (size[i] > size[biggest]) biggest = i;

                bool linked = false;
                for (int c = 0; c < count && !linked; c++)
                {
                    if (c == biggest) continue;

                    // Prefer a road on solid ground; fall back to bridging the
                    // shortest water crossing, which is what separates the two
                    // halves of San Monica across the bay.
                    int bestFrom = -1, bestTo = -1;
                    float bestDist = MaxLink * MaxLink;
                    int bridgeFrom = -1, bridgeTo = -1;
                    float bridgeDist = MaxBridge * MaxBridge;

                    for (int i = 0; i < Nodes.Count; i++)
                    {
                        if (component[i] != c) continue;
                        for (int j = 0; j < Nodes.Count; j++)
                        {
                            if (component[j] == c) continue;
                            float d = (Nodes[i].Pos - Nodes[j].Pos).sqrMagnitude;
                            if (d < bestDist && LinkStaysOnLand(Nodes[i].Pos, Nodes[j].Pos))
                            {
                                bestDist = d; bestFrom = i; bestTo = j;
                            }
                            else if (d < bridgeDist && BanksAreDry(Nodes[i].Pos, Nodes[j].Pos))
                            {
                                bridgeDist = d; bridgeFrom = i; bridgeTo = j;
                            }
                        }
                    }

                    if (bestFrom >= 0)
                        AddSegment(Nodes[bestFrom].Pos, Nodes[bestTo].Pos, RoadKind.Street, 1);
                    else if (bridgeFrom >= 0)
                        AddSegment(Nodes[bridgeFrom].Pos, Nodes[bridgeTo].Pos, RoadKind.Avenue, 1,
                                   false, false, true);
                    else continue;
                    linked = true;
                }
                if (!linked) return;      // what is left is separated by water
            }
        }

        /// <summary>Component index per node, via a flood fill over the segments.</summary>
        private int[] LabelComponents(out int count)
        {
            var label = new int[Nodes.Count];
            for (int i = 0; i < label.Length; i++) label[i] = -1;

            count = 0;
            var stack = new Stack<int>();
            for (int start = 0; start < Nodes.Count; start++)
            {
                if (label[start] >= 0) continue;
                label[start] = count;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var node = Nodes[stack.Pop()];
                    for (int i = 0; i < node.Segments.Count; i++)
                    {
                        var seg = Segments[node.Segments[i]];
                        int a = seg.NodeA, b = seg.NodeB;
                        if (a >= 0 && label[a] < 0) { label[a] = count; stack.Push(a); }
                        if (b >= 0 && label[b] < 0) { label[b] = count; stack.Push(b); }
                    }
                }
                count++;
            }
            return label;
        }

        /// <summary>
        /// A bridge still has to start and finish on dry land, or its ramps end
        /// in the sea. The middle is allowed to be water - that is the point.
        /// </summary>
        private bool BanksAreDry(Vector2 a, Vector2 b)
        {
            float dry = _cfg.seaLevel + 1.5f;
            return _map.SampleHeight(a.x, a.y) > dry && _map.SampleHeight(b.x, b.y) > dry;
        }

        /// <summary>A link is only worth laying where a car could actually drive.</summary>
        private bool LinkStaysOnLand(Vector2 a, Vector2 b)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(4, Mathf.CeilToInt(length / 12f));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                if (_map.SampleHeight(p.x, p.y) <= _cfg.seaLevel + 1.2f) return false;
            }
            return true;
        }

        private int GetNode(Vector2 p, RoadKind kind)
        {
            long key = ((long)Mathf.RoundToInt(p.x * 0.25f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 0.25f);
            if (_nodeLookup.TryGetValue(key, out int idx))
            {
                if (kind < Nodes[idx].DominantKind) Nodes[idx].DominantKind = kind;
                return idx;
            }
            idx = Nodes.Count;
            Nodes.Add(new RoadNode { Pos = p, DominantKind = kind });
            _nodeLookup[key] = idx;
            return idx;
        }

        private void AddSegment(Vector2 a, Vector2 b, RoadKind kind, int lanes, bool oneWay = false,
                                bool sidewalk = true, bool bridge = false)
        {
            float len = Vector2.Distance(a, b);
            if (len < 4f) return;
            var seg = new RoadSegment
            {
                IsBridge = bridge,
                A = a, B = b, Length = len, Dir = (b - a) / len,
                Kind = kind, LanesPerDirection = Mathf.Max(1, lanes), OneWay = oneWay,
                HasSidewalk = sidewalk && kind != RoadKind.Highway && kind != RoadKind.Runway && kind != RoadKind.Taxiway,
                Jitter = ((Segments.Count * 37) % 11) * 0.0016f
            };
            seg.HalfWidth = HalfWidthFor(kind, seg.LanesPerDirection, oneWay);
            if (bridge)
            {
                seg.DeckA = _map.SampleHeight(a.x, a.y);
                seg.DeckB = _map.SampleHeight(b.x, b.y);
                // Rise enough for the middle of the span to clear the water with
                // room for a boat, measured from the lower of the two banks.
                float mid = (seg.DeckA + seg.DeckB) * 0.5f;
                seg.Arch = Mathf.Max(0f, (_cfg.seaLevel + 7f) - mid);
            }
            seg.NodeA = GetNode(a, kind);
            seg.NodeB = GetNode(b, kind);
            int si = Segments.Count;
            Segments.Add(seg);
            Nodes[seg.NodeA].Segments.Add(si);
            Nodes[seg.NodeB].Segments.Add(si);
        }

        private float HalfWidthFor(RoadKind kind, int lanes, bool oneWay)
        {
            float dirs = oneWay ? 1f : 2f;
            switch (kind)
            {
                case RoadKind.Highway: return lanes * dirs * _cfg.laneWidth * 0.5f + 2.4f;
                case RoadKind.Avenue: return lanes * dirs * _cfg.laneWidth * 0.5f + 1.2f;
                case RoadKind.Runway: return 22f;
                case RoadKind.Taxiway: return 11f;
                case RoadKind.Alley: return 3.2f;
                case RoadKind.Dirt: return 3.4f;
                case RoadKind.Rural: return lanes * dirs * _cfg.laneWidth * 0.5f + 0.8f;
                default: return lanes * dirs * _cfg.laneWidth * 0.5f + 0.6f;
            }
        }

        // --- Urban grid -----------------------------------------------------

        private bool IsBuildable(Vector2 p)
        {
            if (Mathf.Abs(p.x) > _cfg.HalfSize - 200f || Mathf.Abs(p.y) > _cfg.HalfSize - 200f) return false;
            if (_map.Landness(p.x, p.y) < 26f) return false;
            return true;
        }

        private void BuildUrbanGrid()
        {
            float pitch = _cfg.blockSize + _cfg.streetWidth;
            const float minX = -3900f, maxX = 3900f, minZ = -4400f, maxZ = 4400f;
            int nx = Mathf.CeilToInt((maxX - minX) / pitch);
            int nz = Mathf.CeilToInt((maxZ - minZ) / pitch);

            var valid = new bool[nx + 1, nz + 1];
            var pos = new Vector2[nx + 1, nz + 1];
            for (int ix = 0; ix <= nx; ix++)
            for (int iz = 0; iz <= nz; iz++)
            {
                // A gentle warp stops the grid from reading as a perfect chessboard.
                float wx = Noise.Fbm(ix * 0.09f, iz * 0.09f, 2) * 9f;
                float wz = Noise.Fbm(ix * 0.09f + 31f, iz * 0.09f, 2) * 9f;
                var p = new Vector2(minX + ix * pitch + wx, minZ + iz * pitch + wz);
                pos[ix, iz] = p;
                valid[ix, iz] = IsBuildable(p) && _map.UrbanMask(p.x, p.y) > 0.22f &&
                                _map.DistrictAt(p.x, p.y) != DistrictType.Airport;
            }

            for (int ix = 0; ix <= nx; ix++)
            for (int iz = 0; iz <= nz; iz++)
            {
                if (!valid[ix, iz]) continue;
                bool avenueX = ix % 4 == 0;
                bool avenueZ = iz % 4 == 0;

                if (ix < nx && valid[ix + 1, iz])
                {
                    var kind = avenueZ ? RoadKind.Avenue : RoadKind.Street;
                    AddSegment(pos[ix, iz], pos[ix + 1, iz], kind, avenueZ ? 2 : 1);
                }
                if (iz < nz && valid[ix, iz + 1])
                {
                    var kind = avenueX ? RoadKind.Avenue : RoadKind.Street;
                    AddSegment(pos[ix, iz], pos[ix, iz + 1], kind, avenueX ? 2 : 1);
                }
            }
        }

        // --- Highways -------------------------------------------------------

        private void BuildHighways()
        {
            // Interstate 9 - north/south spine on the east flank of the city.
            AddPolyline(new[]
            {
                new Vector2(1750f, -5600f), new Vector2(1900f, -3800f), new Vector2(2050f, -2200f),
                new Vector2(2150f, -600f), new Vector2(2050f, 900f), new Vector2(1850f, 2400f),
                new Vector2(1900f, 4000f), new Vector2(2350f, 5600f), new Vector2(3100f, 7000f)
            }, RoadKind.Highway, 3);

            // Coast Highway - hugs the shoreline from the marina down past the port.
            var coast = new List<Vector2>();
            for (float z = 3200f; z >= -4200f; z -= 260f)
            {
                float x = _map.ShoreX(z) + 300f;
                coast.Add(new Vector2(x, z));
            }
            AddPolyline(coast.ToArray(), RoadKind.Highway, 2);

            // Cross-town connector linking downtown to the interstate.
            AddPolyline(new[]
            {
                new Vector2(-3050f, 250f), new Vector2(-1750f, 150f), new Vector2(-300f, 0f),
                new Vector2(1100f, -150f), new Vector2(2100f, -300f)
            }, RoadKind.Highway, 2);

            // Airport expressway.
            AddPolyline(new[]
            {
                new Vector2(2100f, -450f), new Vector2(2500f, -560f), new Vector2(3000f, -640f),
                new Vector2(3550f, -700f)
            }, RoadKind.Highway, 2);

            // Northern mountain pass.
            AddPolyline(new[]
            {
                new Vector2(1900f, 4000f), new Vector2(2900f, 4400f), new Vector2(4100f, 4700f),
                new Vector2(5200f, 4300f), new Vector2(6300f, 3600f)
            }, RoadKind.Rural, 1);

            // Eastern desert route.
            AddPolyline(new[]
            {
                new Vector2(2050f, -2200f), new Vector2(3200f, -2450f), new Vector2(4400f, -2700f),
                new Vector2(5600f, -2600f), new Vector2(6900f, -2100f)
            }, RoadKind.Rural, 1);
        }

        private void AddPolyline(Vector2[] pts, RoadKind kind, int lanes)
        {
            const float step = 110f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                int div = Mathf.Max(1, Mathf.RoundToInt(len / step));
                Vector2 prev = a;
                for (int s = 1; s <= div; s++)
                {
                    float t = (float)s / div;
                    Vector2 p = Vector2.Lerp(a, b, t);
                    // Smooth the joints with a light lateral wobble for a natural feel.
                    if (s < div)
                    {
                        Vector2 n = new Vector2(-(b - a).normalized.y, (b - a).normalized.x);
                        p += n * Noise.Fbm(p.x * 0.0016f, p.y * 0.0016f, 2) * 12f;
                    }
                    if (_map.Landness(p.x, p.y) > -8f && _map.Landness(prev.x, prev.y) > -8f)
                        AddSegment(prev, p, kind, lanes, false, false);
                    prev = p;
                }
            }
        }

        // --- Rural roads ----------------------------------------------------

        private void BuildRuralRoads()
        {
            var targets = new List<Vector2>
            {
                new Vector2(2500f, 2450f), new Vector2(3100f, 1150f), new Vector2(1900f, -3600f),
                new Vector2(4500f, 2100f), new Vector2(3900f, -2600f), new Vector2(3600f, 4200f),
                new Vector2(6100f, 1200f), new Vector2(5200f, -2800f), new Vector2(-2600f, 3600f),
                new Vector2(-1200f, 4600f), new Vector2(5600f, 4600f), new Vector2(2200f, -5600f),
                new Vector2(6400f, -400f), new Vector2(-3200f, -3400f)
            };

            foreach (var t in targets)
            {
                int nearest = NearestNodeOfKind(t, RoadKind.Rural, RoadKind.Highway);
                if (nearest < 0) continue;
                Vector2 from = Nodes[nearest].Pos;
                var pts = new List<Vector2> { from };
                int steps = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(from, t) / 320f), 1, 14);
                for (int i = 1; i <= steps; i++)
                {
                    float f = (float)i / steps;
                    Vector2 p = Vector2.Lerp(from, t, f);
                    Vector2 dir = (t - from).normalized;
                    Vector2 n = new Vector2(-dir.y, dir.x);
                    p += n * Noise.Fbm(p.x * 0.0012f + 17f, p.y * 0.0012f, 3) * 190f * Mathf.Sin(f * Mathf.PI);
                    if (_map.Landness(p.x, p.y) < 10f) break;
                    pts.Add(p);
                }
                if (pts.Count > 1) AddPolyline(pts.ToArray(), RoadKind.Rural, 1);

                // Local lanes around each rural hub.
                var rng = new SanMonica.Core.Rng(Mathf.RoundToInt(t.x + t.y * 31f));
                for (int b = 0; b < 5; b++)
                {
                    float ang = rng.Value * Mathf.PI * 2f;
                    float len = rng.Range(240f, 620f);
                    Vector2 end = t + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                    if (_map.Landness(end.x, end.y) < 12f) continue;
                    AddPolyline(new[] { t, end }, rng.Chance(0.4f) ? RoadKind.Dirt : RoadKind.Rural, 1);
                }
            }
        }

        private int NearestNodeOfKind(Vector2 p, params RoadKind[] kinds)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < Nodes.Count; i++)
            {
                bool ok = false;
                for (int k = 0; k < kinds.Length; k++) if (Nodes[i].DominantKind == kinds[k]) { ok = true; break; }
                if (!ok) continue;
                float d = (Nodes[i].Pos - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // --- Airport --------------------------------------------------------

        private void BuildAirport()
        {
            Vector2 c = _map.AirportCenter;
            // Two parallel runways plus taxiways and an apron loop.
            AddSegment(c + new Vector2(-1250f, 260f), c + new Vector2(1250f, 260f), RoadKind.Runway, 2, false, false);
            AddSegment(c + new Vector2(-1150f, -420f), c + new Vector2(1150f, -420f), RoadKind.Runway, 2, false, false);
            AddSegment(c + new Vector2(-1150f, 60f), c + new Vector2(1150f, 60f), RoadKind.Taxiway, 1, false, false);
            AddSegment(c + new Vector2(-1150f, 60f), c + new Vector2(-1150f, 260f), RoadKind.Taxiway, 1, false, false);
            AddSegment(c + new Vector2(1150f, 60f), c + new Vector2(1150f, 260f), RoadKind.Taxiway, 1, false, false);
            AddSegment(c + new Vector2(-1150f, 60f), c + new Vector2(-1150f, -420f), RoadKind.Taxiway, 1, false, false);
            AddSegment(c + new Vector2(1150f, 60f), c + new Vector2(1150f, -420f), RoadKind.Taxiway, 1, false, false);

            // Terminal access road connecting to the expressway.
            AddPolyline(new[]
            {
                new Vector2(3550f, -700f), c + new Vector2(700f, -700f), c + new Vector2(0f, -760f),
                c + new Vector2(-700f, -700f), c + new Vector2(-900f, -300f)
            }, RoadKind.Avenue, 2);
        }

        // ------------------------------------------------------------------
        // Spatial index & queries
        // ------------------------------------------------------------------

        private static long CellKey(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        private void BuildSpatialIndex()
        {
            _cellSize = _cfg.chunkSize;
            _spatial = new Dictionary<long, List<int>>(Segments.Count);
            for (int i = 0; i < Segments.Count; i++)
            {
                var s = Segments[i];
                float pad = s.HalfWidth + _cfg.sidewalkWidth + 2f;
                int minX = Mathf.FloorToInt((Mathf.Min(s.A.x, s.B.x) - pad) / _cellSize);
                int maxX = Mathf.FloorToInt((Mathf.Max(s.A.x, s.B.x) + pad) / _cellSize);
                int minZ = Mathf.FloorToInt((Mathf.Min(s.A.y, s.B.y) - pad) / _cellSize);
                int maxZ = Mathf.FloorToInt((Mathf.Max(s.A.y, s.B.y) + pad) / _cellSize);
                for (int cx = minX; cx <= maxX; cx++)
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    long k = CellKey(cx, cz);
                    if (!_spatial.TryGetValue(k, out var list)) { list = new List<int>(8); _spatial[k] = list; }
                    list.Add(i);
                }
            }
        }

        private void MarkIntersections()
        {
            foreach (var n in Nodes)
            {
                n.IsIntersection = n.Segments.Count > 2;
                if (!n.IsIntersection) continue;
                bool major = false;
                foreach (int si in n.Segments)
                {
                    var k = Segments[si].Kind;
                    if (k == RoadKind.Avenue || k == RoadKind.Highway) { major = true; break; }
                }
                n.HasTrafficLight = major && n.Segments.Count >= 3;
            }
        }

        /// <summary>Collects every segment whose footprint touches the given world cell.</summary>
        public void CollectSegments(int cellX, int cellZ, List<int> results)
        {
            results.Clear();
            if (_spatial != null && _spatial.TryGetValue(CellKey(cellX, cellZ), out var list))
                results.AddRange(list);
        }

        public void CollectSegmentsAround(Vector2 p, float radius, List<int> results)
        {
            results.Clear();
            int minX = Mathf.FloorToInt((p.x - radius) / _cellSize);
            int maxX = Mathf.FloorToInt((p.x + radius) / _cellSize);
            int minZ = Mathf.FloorToInt((p.y - radius) / _cellSize);
            int maxZ = Mathf.FloorToInt((p.y + radius) / _cellSize);
            for (int cx = minX; cx <= maxX; cx++)
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                if (_spatial == null || !_spatial.TryGetValue(CellKey(cx, cz), out var list)) continue;
                foreach (int i in list) if (!results.Contains(i)) results.Add(i);
            }
        }

        public static float DistanceToSegment(Vector2 p, in RoadSegment s, out float t)
        {
            Vector2 ap = p - s.A;
            t = Mathf.Clamp01(Vector2.Dot(ap, s.Dir) / Mathf.Max(0.001f, s.Length));
            Vector2 closest = s.A + s.Dir * (t * s.Length);
            return Vector2.Distance(p, closest);
        }

        private readonly List<int> _queryBuffer = new List<int>(32);

        /// <summary>Signed clearance from road asphalt: negative means the point is on the road.</summary>
        public float RoadClearance(Vector2 p, out int segmentIndex)
        {
            segmentIndex = -1;
            float best = float.MaxValue;
            int cx = Mathf.FloorToInt(p.x / _cellSize), cz = Mathf.FloorToInt(p.y / _cellSize);
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                if (_spatial == null || !_spatial.TryGetValue(CellKey(cx + ox, cz + oz), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var s = Segments[list[i]];
                    float d = DistanceToSegment(p, in s, out _) - s.HalfWidth;
                    if (d < best) { best = d; segmentIndex = list[i]; }
                }
            }
            return best == float.MaxValue ? 9999f : best;
        }

        public bool IsOnRoad(Vector2 p, float margin = 0f) => RoadClearance(p, out _) < margin;

        public int NearestSegment(Vector2 p, float maxDistance = 400f)
        {
            int best = -1; float bestD = maxDistance;
            CollectSegmentsAround(p, Mathf.Min(maxDistance, 400f), _queryBuffer);
            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                var s = Segments[_queryBuffer[i]];
                float d = DistanceToSegment(p, in s, out _);
                if (d < bestD) { bestD = d; best = _queryBuffer[i]; }
            }
            return best;
        }

        public int NearestNode(Vector2 p)
        {
            int seg = NearestSegment(p, 1200f);
            if (seg < 0)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    float d = (Nodes[i].Pos - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                return best;
            }
            var s2 = Segments[seg];
            return (p - s2.A).sqrMagnitude <= (p - s2.B).sqrMagnitude ? s2.NodeA : s2.NodeB;
        }

        /// <summary>Centre of a driving lane. Lane 0 is the innermost lane of that direction.</summary>
        /// <summary>
        /// Height of the road surface at a point on a segment. Everything that
        /// places something on a road - traffic, parked cars, pedestrians, the
        /// spawn point - must ask for this rather than the height field, or on a
        /// bridge it gets the bed of the bay instead of the deck overhead.
        /// </summary>
        private float SurfaceHeight(in RoadSegment s, float t, Vector2 at)
            => s.IsBridge ? s.DeckAt(t) : _map.SampleHeight(at.x, at.y);

        /// <summary>
        /// Surface height at a junction. A junction where a bridge lands is up on
        /// the deck, not down on the bed, and paths routed through it have to say
        /// so or everything walking the path drops into the water.
        /// </summary>
        public float NodeSurfaceHeight(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= Nodes.Count) return 0f;
            var node = Nodes[nodeIndex];
            float best = _map.SampleHeight(node.Pos.x, node.Pos.y);
            for (int i = 0; i < node.Segments.Count; i++)
            {
                var s = Segments[node.Segments[i]];
                if (!s.IsBridge) continue;
                float deck = s.NodeA == nodeIndex ? s.DeckAt(0f) : s.DeckAt(1f);
                if (deck > best) best = deck;
            }
            return best;
        }

        public Vector3 LanePoint(int segIndex, int lane, bool forward, float t)
        {
            var s = Segments[segIndex];
            float u = forward ? t : 1f - t;
            Vector2 flat = s.Point(u);
            float offset = (lane + 0.5f) * _cfg.laneWidth;
            if (s.OneWay) offset -= s.LanesPerDirection * _cfg.laneWidth * 0.5f;
            Vector2 right = s.Right * (forward ? 1f : -1f);
            Vector2 pos = flat + right * offset;
            return new Vector3(pos.x, SurfaceHeight(s, u, pos) + 0.05f, pos.y);
        }

        public Vector3 SidewalkPoint(int segIndex, bool leftSide, float t)
        {
            var s = Segments[segIndex];
            Vector2 flat = s.Point(t);
            float offset = s.HalfWidth + _cfg.sidewalkWidth * 0.5f;
            Vector2 pos = flat + s.Right * (leftSide ? -offset : offset);
            return new Vector3(pos.x, SurfaceHeight(s, t, pos) + 0.16f, pos.y);
        }

        public float SegmentSpeedLimit(int segIndex)
        {
            switch (Segments[segIndex].Kind)
            {
                case RoadKind.Highway: return 31f;   // ~112 km/h
                case RoadKind.Avenue: return 17f;
                case RoadKind.Rural: return 22f;
                case RoadKind.Dirt: return 12f;
                case RoadKind.Alley: return 7f;
                case RoadKind.Runway:
                case RoadKind.Taxiway: return 14f;
                default: return 13f;
            }
        }

        // ------------------------------------------------------------------
        // Pathfinding (A* over the road graph)
        // ------------------------------------------------------------------

        private readonly Dictionary<int, float> _gScore = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _cameFrom = new Dictionary<int, int>();
        private readonly List<int> _open = new List<int>();

        /// <summary>Finds a node path. Returns false when no route exists within the budget.</summary>
        public bool FindPath(int startNode, int goalNode, List<int> outPath, int maxExpansions = 2500)
        {
            outPath.Clear();
            if (startNode < 0 || goalNode < 0 || startNode >= Nodes.Count || goalNode >= Nodes.Count) return false;
            if (startNode == goalNode) { outPath.Add(startNode); return true; }

            _gScore.Clear(); _cameFrom.Clear(); _open.Clear();
            _gScore[startNode] = 0f;
            _open.Add(startNode);
            Vector2 goalPos = Nodes[goalNode].Pos;
            int expansions = 0;

            while (_open.Count > 0 && expansions++ < maxExpansions)
            {
                // Linear scan is fine for the branching factor of a street grid.
                int bestIdx = 0; float bestF = float.MaxValue;
                for (int i = 0; i < _open.Count; i++)
                {
                    int n = _open[i];
                    float f = _gScore[n] + Vector2.Distance(Nodes[n].Pos, goalPos);
                    if (f < bestF) { bestF = f; bestIdx = i; }
                }
                int current = _open[bestIdx];
                _open.RemoveAt(bestIdx);

                if (current == goalNode)
                {
                    int c = current;
                    outPath.Add(c);
                    while (_cameFrom.TryGetValue(c, out int prev)) { c = prev; outPath.Add(c); }
                    outPath.Reverse();
                    return true;
                }

                var node = Nodes[current];
                for (int i = 0; i < node.Segments.Count; i++)
                {
                    var seg = Segments[node.Segments[i]];
                    int next = seg.NodeA == current ? seg.NodeB : seg.NodeA;
                    float cost = seg.Length * KindCost(seg.Kind);
                    float tentative = _gScore[current] + cost;
                    if (_gScore.TryGetValue(next, out float existing) && existing <= tentative) continue;
                    _gScore[next] = tentative;
                    _cameFrom[next] = current;
                    if (!_open.Contains(next)) _open.Add(next);
                }
            }
            return false;
        }

        private static float KindCost(RoadKind k)
        {
            switch (k)
            {
                case RoadKind.Highway: return 0.55f;
                case RoadKind.Avenue: return 0.8f;
                case RoadKind.Rural: return 0.9f;
                case RoadKind.Dirt: return 1.6f;
                case RoadKind.Runway:
                case RoadKind.Taxiway: return 6f;
                default: return 1f;
            }
        }

        public int SegmentBetween(int nodeA, int nodeB)
        {
            var list = Nodes[nodeA].Segments;
            for (int i = 0; i < list.Count; i++)
            {
                var s = Segments[list[i]];
                if (s.NodeA == nodeB || s.NodeB == nodeB) return list[i];
            }
            return -1;
        }

        /// <summary>Picks a random road position suitable for spawning traffic or parked cars.</summary>
        public bool RandomRoadPoint(ref SanMonica.Core.Rng rng, Vector2 near, float minDist, float maxDist,
                                    out Vector3 point, out int segment, out bool forward)
        {
            point = Vector3.zero; segment = -1; forward = true;
            CollectSegmentsAround(near, maxDist, _queryBuffer);
            if (_queryBuffer.Count == 0) return false;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int si = _queryBuffer[rng.Range(0, _queryBuffer.Count)];
                var s = Segments[si];
                if (s.Kind == RoadKind.Runway || s.Kind == RoadKind.Taxiway) continue;
                float t = rng.Value;
                Vector2 flat = s.Point(t);
                float d = Vector2.Distance(flat, near);
                if (d < minDist || d > maxDist) continue;
                forward = rng.Chance(0.5f);
                int lane = rng.Range(0, s.LanesPerDirection);
                point = LanePoint(si, lane, forward, t);
                segment = si;
                return true;
            }
            return false;
        }
    }
}
