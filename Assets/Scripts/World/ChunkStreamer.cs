using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    /// <summary>
    /// Seamless world streaming. Chunks around the player are built on a frame
    /// budget, promoted or demoted between three levels of detail, and released
    /// when they fall out of range. Distant terrain and skyline are baked once
    /// so the horizon is always populated without any loading screen.
    /// </summary>
    public class ChunkStreamer : MonoBehaviour
    {
        private WorldConfig _cfg;
        private WorldMap _map;
        private RoadNetwork _roads;
        private CityLayout _layout;
        private ChunkBuilder _builder;
        private ChunkGeometry _scratch;
        private FrameBudget _budget;

        private readonly Dictionary<Vector2Int, WorldChunk> _chunks = new Dictionary<Vector2Int, WorldChunk>();
        private readonly List<Vector2Int> _pending = new List<Vector2Int>(64);
        private readonly Dictionary<Vector2Int, int> _desiredLod = new Dictionary<Vector2Int, int>(256);
        private readonly Stack<WorldChunk> _recycled = new Stack<WorldChunk>();
        private Transform _chunkRoot;
        private Transform _lightRoot;
        private readonly List<Light> _lightPool = new List<Light>();

        [Header("Detail rings (chunks)")]
        public int highDetailRings = 1;
        public int mediumDetailRings = 3;
        public int lowDetailRings = 5;
        public int maxStreetLights = 24;

        private Vector2Int _lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        private float _lightTimer;
        private bool _running;

        public int LoadedChunks => _chunks.Count;

        /// <summary>How many loaded tiles actually have a working floor.</summary>
        public int ChunksWithGround
        {
            get
            {
                int n = 0;
                foreach (var kv in _chunks) if (kv.Value != null && kv.Value.HasGroundCollider) n++;
                return n;
            }
        }

        /// <summary>The tile the given world position sits on, or null.</summary>
        public WorldChunk ChunkAt(Vector3 world)
            => _cfg != null && _chunks.TryGetValue(_cfg.WorldToChunk(world), out var c) ? c : null;
        public int PendingChunks => _pending.Count;
        public bool WorldReady { get; private set; }

        public void Initialize(WorldConfig cfg, WorldMap map, RoadNetwork roads, CityLayout layout)
        {
            _cfg = cfg; _map = map; _roads = roads; _layout = layout;
            _builder = new ChunkBuilder(cfg, map, roads, layout);
            _scratch = new ChunkGeometry();
            _budget = new FrameBudget(cfg.chunkBuildBudgetMs);

            _chunkRoot = new GameObject("Chunks").transform;
            _chunkRoot.SetParent(transform, false);
            _lightRoot = new GameObject("StreetLights").transform;
            _lightRoot.SetParent(transform, false);
        }

        public void ApplyQuality(int high, int medium, int low, int lights, float budgetMs)
        {
            highDetailRings = Mathf.Max(1, high);
            mediumDetailRings = Mathf.Max(highDetailRings, medium);
            lowDetailRings = Mathf.Max(mediumDetailRings, low);
            maxStreetLights = Mathf.Max(0, lights);
            if (_budget != null) _budget.BudgetMs = budgetMs;
            _lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        }

        // ------------------------------------------------------------------
        // Initial load
        // ------------------------------------------------------------------
        public IEnumerator PreloadAround(Vector3 position, System.Action<float> onProgress)
        {
            RefreshDesired(_cfg.WorldToChunk(position));
            int total = Mathf.Max(1, _pending.Count);
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            while (_pending.Count > 0)
            {
                BuildNext();
                if (sw.Elapsed.TotalMilliseconds > 12.0)
                {
                    onProgress?.Invoke(1f - (float)_pending.Count / total);
                    sw.Restart();
                    yield return null;
                }
            }
            onProgress?.Invoke(1f);
            WorldReady = true;
            if (!_running) { _running = true; StartCoroutine(StreamLoop()); }
        }

        private IEnumerator StreamLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                if (_pending.Count > 0)
                {
                    _budget.Begin();
                    while (_pending.Count > 0 && !_budget.Exhausted)
                        BuildNext();
                }
                yield return wait;
            }
        }

        private void Update()
        {
            if (_cfg == null) return;
            var center = _cfg.WorldToChunk(Services.PlayerPosition);
            if (center != _lastCenter)
            {
                _lastCenter = center;
                RefreshDesired(center);
            }

            _lightTimer -= Time.unscaledDeltaTime;
            if (_lightTimer <= 0f)
            {
                _lightTimer = 0.35f;
                UpdateStreetLights();
            }
        }

        // ------------------------------------------------------------------
        // Scheduling
        // ------------------------------------------------------------------
        private void RefreshDesired(Vector2Int center)
        {
            _desiredLod.Clear();
            for (int dz = -lowDetailRings; dz <= lowDetailRings; dz++)
            for (int dx = -lowDetailRings; dx <= lowDetailRings; dx++)
            {
                var c = new Vector2Int(center.x + dx, center.y + dz);
                if (!_cfg.InBounds(c)) continue;
                int ring = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
                int lod = ring <= highDetailRings ? 0 : (ring <= mediumDetailRings ? 1 : 2);
                _desiredLod[c] = lod;
            }

            // Unload everything that dropped out of range.
            var toRemove = new List<Vector2Int>();
            foreach (var kv in _chunks)
                if (!_desiredLod.ContainsKey(kv.Key)) toRemove.Add(kv.Key);
            foreach (var c in toRemove) Unload(c);

            // Queue builds and LOD changes, nearest first.
            _pending.Clear();
            foreach (var kv in _desiredLod)
            {
                if (_chunks.TryGetValue(kv.Key, out var existing) && existing.Lod == kv.Value) continue;
                _pending.Add(kv.Key);
            }
            _pending.Sort((a, b) =>
            {
                int da = Mathf.Max(Mathf.Abs(a.x - center.x), Mathf.Abs(a.y - center.y));
                int db = Mathf.Max(Mathf.Abs(b.x - center.x), Mathf.Abs(b.y - center.y));
                return da.CompareTo(db);
            });
        }

        private void BuildNext()
        {
            if (_pending.Count == 0) return;
            var coord = _pending[0];
            _pending.RemoveAt(0);
            if (!_desiredLod.TryGetValue(coord, out int lod)) return;

            if (!_chunks.TryGetValue(coord, out var chunk))
            {
                chunk = _recycled.Count > 0 ? _recycled.Pop() : null;
                if (chunk == null)
                {
                    var go = new GameObject("Chunk");
                    chunk = go.AddComponent<WorldChunk>();
                    chunk.Setup(coord, _chunkRoot, _cfg.chunkSize);
                }
                else
                {
                    chunk.gameObject.SetActive(true);
                    chunk.Coord = coord;
                    chunk.gameObject.name = "Chunk_" + coord.x + "_" + coord.y;
                }
                _chunks[coord] = chunk;
            }

            _builder.Build(coord, lod, _scratch);
            chunk.Apply(_scratch, lod, lod <= 1);
        }

        private void Unload(Vector2Int coord)
        {
            if (!_chunks.TryGetValue(coord, out var chunk)) return;
            _chunks.Remove(coord);
            chunk.Release();
            chunk.gameObject.SetActive(false);
            if (_recycled.Count < 48) _recycled.Push(chunk);
            else Destroy(chunk.gameObject);
        }

        public bool IsLoaded(Vector3 worldPos)
        {
            var c = _cfg.WorldToChunk(worldPos);
            return _chunks.TryGetValue(c, out var chunk) && chunk.Lod >= 0;
        }

        public int LodAt(Vector3 worldPos)
        {
            var c = _cfg.WorldToChunk(worldPos);
            return _chunks.TryGetValue(c, out var chunk) ? chunk.Lod : 3;
        }

        // ------------------------------------------------------------------
        // Street lighting - a small pool follows the player at night
        // ------------------------------------------------------------------
        private void UpdateStreetLights()
        {
            bool night = Services.Clock != null && Services.Clock.IsNight;
            int budget = night ? maxStreetLights : 0;

            while (_lightPool.Count < budget)
            {
                var go = new GameObject("StreetLight");
                go.transform.SetParent(_lightRoot, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForceVertex;
                l.enabled = false;
                _lightPool.Add(l);
            }

            for (int i = budget; i < _lightPool.Count; i++) _lightPool[i].enabled = false;
            if (budget == 0) return;

            Vector3 p = Services.PlayerPosition;
            var best = new List<ChunkGeometry.PointLightSpec>(budget);
            var bestDist = new List<float>(budget);

            foreach (var kv in _chunks)
            {
                var chunk = kv.Value;
                if (chunk.Lod != 0) continue;
                var specs = chunk.LightSpecs;
                for (int i = 0; i < specs.Count; i++)
                {
                    float d = (specs[i].Position - p).sqrMagnitude;
                    if (d > 90f * 90f) continue;
                    int insert = best.Count;
                    for (int k = 0; k < bestDist.Count; k++) if (d < bestDist[k]) { insert = k; break; }
                    if (insert >= budget) continue;
                    best.Insert(insert, specs[i]);
                    bestDist.Insert(insert, d);
                    if (best.Count > budget) { best.RemoveAt(best.Count - 1); bestDist.RemoveAt(bestDist.Count - 1); }
                }
            }

            for (int i = 0; i < _lightPool.Count; i++)
            {
                if (i < best.Count)
                {
                    var l = _lightPool[i];
                    l.transform.position = best[i].Position;
                    l.color = best[i].Color;
                    l.range = best[i].Range;
                    l.intensity = best[i].Intensity;
                    l.enabled = true;
                }
                else _lightPool[i].enabled = false;
            }
        }

        // ------------------------------------------------------------------
        // Distant world (built once)
        // ------------------------------------------------------------------
        public IEnumerator BuildDistantWorld(System.Action<float> onProgress)
        {
            var root = new GameObject("DistantWorld").transform;
            root.SetParent(transform, false);

            // --- Far terrain ---
            var geo = new ChunkGeometry();
            const int res = 96;
            float step = _cfg.worldSize / res;
            float half = _cfg.HalfSize;
            var heights = new float[res + 1, res + 1];
            for (int z = 0; z <= res; z++)
            {
                for (int x = 0; x <= res; x++)
                    heights[x, z] = _map.SampleHeight(-half + x * step, -half + z * step) - 2.5f;
                if ((z & 7) == 0) { onProgress?.Invoke(z / (float)res * 0.6f); yield return null; }
            }

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = -half + (x + 0.5f) * step;
                    float wz = -half + (z + 0.5f) * step;
                    var d = _map.DistrictAt(wx, wz);
                    Material m = FarMaterial(d, heights[x, z]);
                    int sub = geo.Sub(m);
                    Vector3 p00 = new Vector3(-half + x * step, heights[x, z], -half + z * step);
                    Vector3 p10 = new Vector3(-half + (x + 1) * step, heights[x + 1, z], -half + z * step);
                    Vector3 p11 = new Vector3(-half + (x + 1) * step, heights[x + 1, z + 1], -half + (z + 1) * step);
                    Vector3 p01 = new Vector3(-half + x * step, heights[x, z + 1], -half + (z + 1) * step);
                    Vector3 n = Vector3.Cross(p10 - p00, p01 - p00).normalized;
                    if (n.y < 0f) n = -n;
                    int i0 = geo.Builder.AddVertex(p00, n, new Vector2(p00.x, p00.z) * 0.01f);
                    int i1 = geo.Builder.AddVertex(p10, n, new Vector2(p10.x, p10.z) * 0.01f);
                    int i2 = geo.Builder.AddVertex(p11, n, new Vector2(p11.x, p11.z) * 0.01f);
                    int i3 = geo.Builder.AddVertex(p01, n, new Vector2(p01.x, p01.z) * 0.01f);
                    geo.Builder.AddQuadFacing(i0, i1, i2, i3, Vector3.up, sub);
                }
                if ((z & 7) == 0) { onProgress?.Invoke(0.6f + z / (float)res * 0.2f); yield return null; }
            }

            CreateStaticMesh(root, "FarTerrain", geo, 500);
            yield return null;

            // --- Distant skyline (slightly inset so real buildings always win) ---
            geo.Clear();
            var skyline = MaterialLibrary.Surface(SurfaceKind.Concrete, 2, new Color(0.62f, 0.64f, 0.70f), 0.12f);
            int sky = geo.Sub(skyline);
            int emitted = 0;
            for (int i = 0; i < _layout.Lots.Count; i++)
            {
                var lot = _layout.Lots[i];
                if (lot.Kind != LotKind.Building) continue;
                var profile = DistrictCatalog.Get(lot.District);
                if (profile.maxHeight < 24f) continue;
                var rng = new Rng(lot.Seed);
                float h = rng.Range(profile.minHeight, profile.maxHeight) * 0.94f;
                if (h < 22f) continue;
                float y = _map.SampleHeight(lot.Center.x, lot.Center.y);
                geo.Builder.AddBox(new Vector3(lot.Center.x, y + h * 0.5f, lot.Center.y),
                    new Vector3(lot.Size.x * 0.94f, h, lot.Size.y * 0.94f),
                    Quaternion.Euler(0f, lot.Yaw, 0f), 0.05f, sky);
                emitted++;
                if ((emitted & 255) == 0) { onProgress?.Invoke(0.8f + i / (float)_layout.Lots.Count * 0.2f); yield return null; }
            }
            if (emitted > 0) CreateStaticMesh(root, "FarSkyline", geo, 400);

            onProgress?.Invoke(1f);
        }

        private static void CreateStaticMesh(Transform parent, string name, ChunkGeometry geo, int renderQueueHint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = geo.BuildMesh(name);
            mr.sharedMaterials = geo.UsedMaterials();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static Material FarMaterial(DistrictType d, float height)
        {
            if (height < 1f) return MaterialLibrary.Surface(SurfaceKind.Sand, 1, new Color(0.55f, 0.52f, 0.42f), 0.2f);
            if (height > 430f) return MaterialLibrary.Surface(SurfaceKind.Snow, 0, Color.white, 0.3f);
            switch (d)
            {
                case DistrictType.Badlands: return MaterialLibrary.Surface(SurfaceKind.Dirt, 0, new Color(0.78f, 0.62f, 0.42f), 0.06f);
                case DistrictType.Mountains: return MaterialLibrary.Surface(SurfaceKind.Rock, 0, Color.white, 0.06f);
                case DistrictType.Beach: return MaterialLibrary.Surface(SurfaceKind.Sand, 0, Color.white, 0.1f);
                case DistrictType.Downtown:
                case DistrictType.Commercial:
                case DistrictType.Industrial:
                case DistrictType.Port:
                case DistrictType.Airport:
                case DistrictType.Marigold:
                    return MaterialLibrary.Surface(SurfaceKind.Concrete, 1, new Color(0.64f, 0.63f, 0.62f), 0.1f);
                default: return MaterialLibrary.Surface(SurfaceKind.Grass, 0, Color.white, 0.05f);
            }
        }
    }
}
