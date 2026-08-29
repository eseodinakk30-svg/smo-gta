using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    /// <summary>
    /// Turns the deterministic city description into renderable geometry for a
    /// single 256 m chunk at a given level of detail.
    /// LOD 0 = full detail, LOD 1 = reduced props, LOD 2 = massing only.
    /// </summary>
    public class ChunkBuilder
    {
        private readonly WorldConfig _cfg;
        private readonly WorldMap _map;
        private readonly RoadNetwork _roads;
        private readonly CityLayout _layout;
        private readonly List<int> _segBuffer = new List<int>(64);

        public ChunkBuilder(WorldConfig cfg, WorldMap map, RoadNetwork roads, CityLayout layout)
        {
            _cfg = cfg; _map = map; _roads = roads; _layout = layout;
        }

        public void Build(Vector2Int coord, int lod, ChunkGeometry geo)
        {
            geo.Clear();
            Vector3 origin = _cfg.ChunkOrigin(coord);
            float size = _cfg.chunkSize;

            BuildTerrain(origin, size, lod, geo);
            BuildRoads(coord, origin, size, lod, geo);
            BuildLots(coord, lod, geo);
            if (lod <= 1) BuildStreetFurniture(coord, origin, size, lod, geo);
            if (lod <= 1) BuildNature(coord, origin, size, lod, geo);
        }

        // ------------------------------------------------------------------
        // Terrain
        // ------------------------------------------------------------------
        private void BuildTerrain(Vector3 origin, float size, int lod, ChunkGeometry geo)
        {
            int cells = lod == 0 ? 16 : (lod == 1 ? 8 : 4);
            float step = size / cells;

            // Pre-sample the height field one row wider so the edges line up with neighbours.
            var h = new float[cells + 1, cells + 1];
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
                h[x, z] = _map.SampleHeight(origin.x + x * step, origin.z + z * step);

            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                float wx = origin.x + (x + 0.5f) * step;
                float wz = origin.z + (z + 0.5f) * step;
                var mat = GroundMaterial(wx, wz, h[x, z]);
                int sub = geo.Sub(mat);

                Vector3 p00 = new Vector3(origin.x + x * step, h[x, z], origin.z + z * step);
                Vector3 p10 = new Vector3(origin.x + (x + 1) * step, h[x + 1, z], origin.z + z * step);
                Vector3 p11 = new Vector3(origin.x + (x + 1) * step, h[x + 1, z + 1], origin.z + (z + 1) * step);
                Vector3 p01 = new Vector3(origin.x + x * step, h[x, z + 1], origin.z + (z + 1) * step);

                Vector3 n = Vector3.Cross(p10 - p00, p01 - p00).normalized;
                if (n.y < 0f) n = -n;
                Vector2 uvScale = Vector2.one * (step * 0.09f);
                int i0 = geo.Builder.AddVertex(p00, n, new Vector2(p00.x, p00.z) * 0.09f);
                int i1 = geo.Builder.AddVertex(p10, n, new Vector2(p10.x, p10.z) * 0.09f);
                int i2 = geo.Builder.AddVertex(p11, n, new Vector2(p11.x, p11.z) * 0.09f);
                int i3 = geo.Builder.AddVertex(p01, n, new Vector2(p01.x, p01.z) * 0.09f);
                geo.Builder.AddQuadFacing(i0, i1, i2, i3, Vector3.up, sub);
            }
        }

        private Material GroundMaterial(float x, float z, float height)
        {
            var district = _map.DistrictAt(x, z);
            float land = _map.Landness(x, z);
            float slope = _map.SampleSlope(x, z);

            if (land < 0f) return MaterialLibrary.Surface(SurfaceKind.Sand, 1, new Color(0.55f, 0.52f, 0.42f), 0.25f, 0f, 0.6f);
            if (land < 130f && height < 8f) return MaterialLibrary.Surface(SurfaceKind.Sand, 0, Color.white, 0.15f);
            if (slope > 34f) return MaterialLibrary.Surface(SurfaceKind.Rock, 0, Color.white, 0.08f);
            if (height > 430f) return MaterialLibrary.Surface(SurfaceKind.Snow, 0, Color.white, 0.35f);

            switch (district)
            {
                case DistrictType.Badlands:
                    return MaterialLibrary.Surface(SurfaceKind.Dirt, 0, new Color(0.78f, 0.62f, 0.42f), 0.08f);
                case DistrictType.Farmland:
                    return MaterialLibrary.Surface(SurfaceKind.Grass, 1, new Color(0.90f, 0.94f, 0.72f), 0.08f);
                case DistrictType.Forest:
                case DistrictType.Mountains:
                    return MaterialLibrary.Surface(SurfaceKind.Grass, 0, new Color(0.80f, 0.90f, 0.78f), 0.06f);
                case DistrictType.Park:
                case DistrictType.University:
                case DistrictType.Wealthy:
                    return MaterialLibrary.Surface(SurfaceKind.Grass, 2, new Color(0.92f, 1f, 0.86f), 0.08f);
                case DistrictType.Industrial:
                case DistrictType.Port:
                case DistrictType.Airport:
                    return MaterialLibrary.Surface(SurfaceKind.Concrete, 3, new Color(0.70f, 0.70f, 0.68f), 0.14f);
                case DistrictType.Downtown:
                case DistrictType.Commercial:
                case DistrictType.Marigold:
                    return MaterialLibrary.Surface(SurfaceKind.Concrete, 1, new Color(0.66f, 0.65f, 0.63f), 0.14f);
                default:
                    return MaterialLibrary.Surface(SurfaceKind.Grass, 0, Color.white, 0.07f);
            }
        }

        // ------------------------------------------------------------------
        // Roads, sidewalks and markings
        // ------------------------------------------------------------------
        private void BuildRoads(Vector2Int coord, Vector3 origin, float size, int lod, ChunkGeometry geo)
        {
            int cellX = Mathf.FloorToInt(origin.x / _cfg.chunkSize);
            int cellZ = Mathf.FloorToInt(origin.z / _cfg.chunkSize);
            _roads.CollectSegments(cellX, cellZ, _segBuffer);
            if (_segBuffer.Count == 0) return;

            var asphalt = MaterialLibrary.Surface(SurfaceKind.Asphalt, 0, Color.white, 0.22f);
            var runway = MaterialLibrary.Surface(SurfaceKind.Asphalt, 1, new Color(0.85f, 0.85f, 0.88f), 0.18f);
            var dirt = MaterialLibrary.Surface(SurfaceKind.Dirt, 1, new Color(0.85f, 0.74f, 0.55f), 0.10f);
            var walk = MaterialLibrary.Surface(SurfaceKind.Sidewalk, 0, Color.white, 0.16f);
            var kerb = MaterialLibrary.Surface(SurfaceKind.Concrete, 0, new Color(0.74f, 0.73f, 0.70f), 0.18f);
            var paint = MaterialLibrary.Surface(SurfaceKind.RoadMarking, 0, Color.white, 0.05f);

            for (int i = 0; i < _segBuffer.Count; i++)
            {
                int si = _segBuffer[i];
                var s = _roads.Segments[si];
                // Each segment is emitted exactly once, by the chunk holding its midpoint.
                Vector2 mid = s.Point(0.5f);
                if (Mathf.FloorToInt(mid.x / _cfg.chunkSize) != cellX || Mathf.FloorToInt(mid.y / _cfg.chunkSize) != cellZ) continue;

                Material surface = s.Kind == RoadKind.Dirt ? dirt
                    : (s.Kind == RoadKind.Runway || s.Kind == RoadKind.Taxiway) ? runway : asphalt;

                int steps = Mathf.Clamp(Mathf.RoundToInt(s.Length / 12f), 1, 12);
                if (lod == 2) steps = Mathf.Min(steps, 3);

                float lift = 0.045f + s.Jitter;
                EmitRibbon(geo, s, -s.HalfWidth, s.HalfWidth, lift, steps, surface, 0.12f);

                if (s.HasSidewalk && lod <= 1)
                {
                    float inner = s.HalfWidth;
                    float outer = s.HalfWidth + _cfg.sidewalkWidth;
                    EmitRibbon(geo, s, inner, outer, 0.17f, steps, walk, 0.2f);
                    EmitRibbon(geo, s, -outer, -inner, 0.17f, steps, walk, 0.2f);
                    // Kerb faces.
                    EmitVerticalStrip(geo, s, inner, 0.045f, 0.17f, steps, kerb);
                    EmitVerticalStrip(geo, s, -inner, 0.045f, 0.17f, steps, kerb);
                }

                if (lod == 0 && s.Kind != RoadKind.Dirt && s.Kind != RoadKind.Alley)
                {
                    // Centre line: solid on highways, dashed elsewhere.
                    if (!s.OneWay)
                    {
                        if (s.Kind == RoadKind.Highway)
                            EmitRibbon(geo, s, -0.16f, 0.16f, lift + 0.012f, steps, paint, 0.5f);
                        else
                            EmitDashes(geo, s, 0f, 0.14f, lift + 0.012f, paint);
                    }
                    // Lane dividers.
                    for (int l = 1; l < s.LanesPerDirection; l++)
                    {
                        float o = l * _cfg.laneWidth;
                        EmitDashes(geo, s, o, 0.12f, lift + 0.012f, paint);
                        EmitDashes(geo, s, -o, 0.12f, lift + 0.012f, paint);
                    }
                    // Edge lines.
                    EmitRibbon(geo, s, s.HalfWidth - 0.35f, s.HalfWidth - 0.13f, lift + 0.012f, steps, paint, 0.5f);
                    EmitRibbon(geo, s, -s.HalfWidth + 0.13f, -s.HalfWidth + 0.35f, lift + 0.012f, steps, paint, 0.5f);
                }
            }
        }

        private void EmitRibbon(ChunkGeometry geo, in RoadSegment s, float offA, float offB, float lift, int steps, Material mat, float uvScale)
        {
            int sub = geo.Sub(mat);
            Vector2 right = s.Right;
            Vector3 prevL = default, prevR = default;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 c = s.Point(t);
                Vector2 a2 = c + right * offA;
                Vector2 b2 = c + right * offB;
                Vector3 a = new Vector3(a2.x, _map.SampleHeight(a2.x, a2.y) + lift, a2.y);
                Vector3 b = new Vector3(b2.x, _map.SampleHeight(b2.x, b2.y) + lift, b2.y);
                if (i > 0)
                {
                    float v0 = (i - 1) * s.Length / steps * uvScale;
                    float v1 = i * s.Length / steps * uvScale;
                    float w = Mathf.Abs(offB - offA) * uvScale;
                    int i0 = geo.Builder.AddVertex(prevL, Vector3.up, new Vector2(0f, v0));
                    int i1 = geo.Builder.AddVertex(prevR, Vector3.up, new Vector2(w, v0));
                    int i2 = geo.Builder.AddVertex(b, Vector3.up, new Vector2(w, v1));
                    int i3 = geo.Builder.AddVertex(a, Vector3.up, new Vector2(0f, v1));
                    geo.Builder.AddQuadFacing(i0, i1, i2, i3, Vector3.up, sub);
                }
                prevL = a; prevR = b;
            }
        }

        private void EmitVerticalStrip(ChunkGeometry geo, in RoadSegment s, float offset, float yLow, float yHigh, int steps, Material mat)
        {
            int sub = geo.Sub(mat);
            Vector2 right = s.Right;
            Vector3 prevLow = default, prevHigh = default;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 c = s.Point(t) + right * offset;
                float h = _map.SampleHeight(c.x, c.y);
                Vector3 low = new Vector3(c.x, h + yLow, c.y);
                Vector3 high = new Vector3(c.x, h + yHigh, c.y);
                if (i > 0)
                {
                    Vector3 n = Vector3.Cross(high - low, prevLow - low).normalized;
                    int i0 = geo.Builder.AddVertex(prevLow, n, new Vector2(0f, 0f));
                    int i1 = geo.Builder.AddVertex(low, n, new Vector2(1f, 0f));
                    int i2 = geo.Builder.AddVertex(high, n, new Vector2(1f, 1f));
                    int i3 = geo.Builder.AddVertex(prevHigh, n, new Vector2(0f, 1f));
                    geo.Builder.AddQuadFacing(i0, i1, i2, i3, n, sub);
                }
                prevLow = low; prevHigh = high;
            }
        }

        private void EmitDashes(ChunkGeometry geo, in RoadSegment s, float offset, float halfWidth, float lift, Material mat)
        {
            int sub = geo.Sub(mat);
            const float dash = 3f, gap = 4.5f;
            float pos = 1.5f;
            Vector2 right = s.Right;
            while (pos + dash < s.Length)
            {
                float t0 = pos / s.Length, t1 = (pos + dash) / s.Length;
                Vector2 c0 = s.Point(t0) + right * offset;
                Vector2 c1 = s.Point(t1) + right * offset;
                Vector2 w = right * halfWidth;
                Vector3 a = new Vector3(c0.x - w.x, _map.SampleHeight(c0.x, c0.y) + lift, c0.y - w.y);
                Vector3 b = new Vector3(c0.x + w.x, _map.SampleHeight(c0.x, c0.y) + lift, c0.y + w.y);
                Vector3 c = new Vector3(c1.x + w.x, _map.SampleHeight(c1.x, c1.y) + lift, c1.y + w.y);
                Vector3 d = new Vector3(c1.x - w.x, _map.SampleHeight(c1.x, c1.y) + lift, c1.y - w.y);
                int i0 = geo.Builder.AddVertex(a, Vector3.up, Vector2.zero);
                int i1 = geo.Builder.AddVertex(b, Vector3.up, new Vector2(1f, 0f));
                int i2 = geo.Builder.AddVertex(c, Vector3.up, Vector2.one);
                int i3 = geo.Builder.AddVertex(d, Vector3.up, new Vector2(0f, 1f));
                geo.Builder.AddQuadFacing(i0, i1, i2, i3, Vector3.up, sub);
                pos += dash + gap;
            }
        }

        // ------------------------------------------------------------------
        // Lots
        // ------------------------------------------------------------------
        private void BuildLots(Vector2Int coord, int lod, ChunkGeometry geo)
        {
            var list = _layout.LotsInChunk(coord);
            if (list == null) return;
            Vector3 origin = _cfg.ChunkOrigin(coord);
            float size = _cfg.chunkSize;

            for (int i = 0; i < list.Count; i++)
            {
                var lot = _layout.Lots[list[i]];
                // Only the chunk that owns the centre draws the lot.
                if (lot.Center.x < origin.x || lot.Center.x >= origin.x + size ||
                    lot.Center.y < origin.z || lot.Center.y >= origin.z + size) continue;

                var rng = new Rng(lot.Seed);
                float ground = _map.SampleHeight(lot.Center.x, lot.Center.y);
                Vector3 g = new Vector3(lot.Center.x, ground, lot.Center.y);
                var profile = DistrictCatalog.Get(lot.District);

                switch (lot.Kind)
                {
                    case LotKind.Building:
                    {
                        var result = BuildingFactory.Build(geo, _map, lot.District, profile, g, lot.Size, lot.Yaw, ref rng, lod);
                        if (lod == 0 && lot.ShopIndex >= 0 && lot.ShopIndex < _layout.Shops.Count)
                        {
                            var shop = _layout.Shops[lot.ShopIndex];
                            PropFactory.ShopSign(geo, result.Entrance + Vector3.up * 4.2f, lot.Yaw, shop.Definition.signColor);
                            geo.AddBoxCollider(result.Entrance + Vector3.up * 1.2f, new Vector3(2.6f, 2.4f, 1.6f),
                                GameLayers.Interactable, Quaternion.Euler(0f, lot.Yaw, 0f), true);
                        }
                        if (lod == 0 && rng.Chance(0.10f) && result.Height > 12f)
                            PropFactory.Billboard(geo, result.Center + Vector3.up * (result.Height * 0.5f + 1f), lot.Yaw, ref rng);
                        break;
                    }
                    case LotKind.Park:
                        BuildPark(geo, g, lot, profile, ref rng, lod);
                        break;
                    case LotKind.Plaza:
                        BuildPlaza(geo, g, lot, ref rng, lod);
                        break;
                    case LotKind.ParkingLot:
                        BuildParkingLot(geo, g, lot, ref rng, lod);
                        break;
                    case LotKind.Yard:
                        BuildYard(geo, g, lot, ref rng, lod);
                        break;
                    case LotKind.Farmfield:
                        BuildField(geo, g, lot, ref rng, lod);
                        break;
                    case LotKind.Apron:
                        BuildApron(geo, g, lot, ref rng, lod);
                        break;
                }
            }
        }

        private void BuildPark(ChunkGeometry geo, Vector3 g, BuildingLot lot, DistrictProfile profile, ref Rng rng, int lod)
        {
            var grass = MaterialLibrary.Surface(SurfaceKind.Grass, 2, new Color(0.86f, 1f, 0.80f), 0.07f);
            geo.Builder.AddBox(g + Vector3.up * 0.06f, new Vector3(lot.Size.x, 0.12f, lot.Size.y), Quaternion.identity, 0.1f, geo.Sub(grass));

            if (lod > 1) return;
            int trees = Mathf.RoundToInt(lot.Size.x * lot.Size.y * 0.004f * Mathf.Max(0.2f, profile.treeDensity));
            for (int i = 0; i < trees; i++)
            {
                Vector2 o = new Vector2(rng.Range(-0.45f, 0.45f), rng.Range(-0.45f, 0.45f));
                Vector3 p = g + new Vector3(o.x * lot.Size.x, 0f, o.y * lot.Size.y);
                p.y = _map.SampleHeight(p.x, p.z);
                PropFactory.Tree(geo, p, ref rng, rng.Chance(0.25f) ? TreeKind.Pine : TreeKind.Broadleaf, lod);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 o = new Vector2(rng.Range(-0.4f, 0.4f), rng.Range(-0.4f, 0.4f));
                Vector3 p = g + new Vector3(o.x * lot.Size.x, 0f, o.y * lot.Size.y);
                p.y = _map.SampleHeight(p.x, p.z);
                PropFactory.Bench(geo, p, rng.Value * 360f);
            }
            if (rng.Chance(0.35f))
                PropFactory.StreetLamp(geo, g, 0f);
        }

        private void BuildPlaza(ChunkGeometry geo, Vector3 g, BuildingLot lot, ref Rng rng, int lod)
        {
            var tile = MaterialLibrary.Surface(SurfaceKind.Tile, 0, new Color(0.80f, 0.78f, 0.74f), 0.28f);
            geo.Builder.AddBox(g + Vector3.up * 0.09f, new Vector3(lot.Size.x, 0.18f, lot.Size.y), Quaternion.identity, 0.12f, geo.Sub(tile));
            if (lod > 1) return;

            // Fountain in the middle of larger plazas.
            if (lot.Size.x > 30f && rng.Chance(0.5f))
            {
                var stone = MaterialLibrary.Surface(SurfaceKind.Marble, 0, Color.white, 0.4f);
                var water = MaterialLibrary.Transparent(new Color(0.25f, 0.55f, 0.68f, 0.7f));
                geo.Builder.AddCylinder(g + Vector3.up * 0.45f, 5.5f, 0.9f, 14, geo.Sub(stone), true, 0.3f);
                geo.Builder.AddCylinder(g + Vector3.up * 0.82f, 5.0f, 0.16f, 14, geo.Sub(water), true, 0.3f);
                geo.Builder.AddCylinder(g + Vector3.up * 1.6f, 0.7f, 2.4f, 8, geo.Sub(stone), true, 0.4f);
                geo.AddBoxCollider(g + Vector3.up * 0.45f, new Vector3(11f, 0.9f, 11f), GameLayers.Prop);
            }
            int benches = rng.Range(2, 6);
            for (int i = 0; i < benches; i++)
            {
                Vector2 o = new Vector2(rng.Range(-0.42f, 0.42f), rng.Range(-0.42f, 0.42f));
                Vector3 p = g + new Vector3(o.x * lot.Size.x, 0.18f, o.y * lot.Size.y);
                PropFactory.Bench(geo, p, rng.Value * 360f);
            }
            for (int i = 0; i < 2; i++)
            {
                Vector2 o = new Vector2(rng.Range(-0.45f, 0.45f), rng.Range(-0.45f, 0.45f));
                PropFactory.StreetLamp(geo, g + new Vector3(o.x * lot.Size.x, 0.18f, o.y * lot.Size.y), rng.Value * 360f);
            }
        }

        private void BuildParkingLot(ChunkGeometry geo, Vector3 g, BuildingLot lot, ref Rng rng, int lod)
        {
            var tarmac = MaterialLibrary.Surface(SurfaceKind.Asphalt, 0, new Color(0.88f, 0.88f, 0.90f), 0.2f);
            geo.Builder.AddBox(g + Vector3.up * 0.07f, new Vector3(lot.Size.x, 0.14f, lot.Size.y), Quaternion.identity, 0.1f, geo.Sub(tarmac));
            if (lod > 1) return;

            var paint = MaterialLibrary.Surface(SurfaceKind.RoadMarking, 0, Color.white, 0.05f);
            int slots = Mathf.Max(2, Mathf.FloorToInt(lot.Size.x / 2.8f));
            int sub = geo.Sub(paint);
            for (int i = 0; i <= slots; i++)
            {
                float ox = (-0.5f + (float)i / slots) * lot.Size.x;
                geo.Builder.AddBox(g + new Vector3(ox, 0.155f, 0f), new Vector3(0.14f, 0.02f, lot.Size.y * 0.8f), Quaternion.identity, 0.5f, sub);
            }
            PropFactory.StreetLamp(geo, g + new Vector3(lot.Size.x * 0.4f, 0.14f, lot.Size.y * 0.4f), 0f, true);
        }

        private void BuildYard(ChunkGeometry geo, Vector3 g, BuildingLot lot, ref Rng rng, int lod)
        {
            var concrete = MaterialLibrary.Surface(SurfaceKind.Concrete, 3, new Color(0.66f, 0.65f, 0.62f), 0.14f);
            geo.Builder.AddBox(g + Vector3.up * 0.07f, new Vector3(lot.Size.x, 0.14f, lot.Size.y), Quaternion.identity, 0.1f, geo.Sub(concrete));
            if (lod > 1) return;

            bool port = lot.District == DistrictType.Port;
            int stacks = rng.Range(3, port ? 14 : 7);
            for (int i = 0; i < stacks; i++)
            {
                Vector2 o = new Vector2(rng.Range(-0.4f, 0.4f), rng.Range(-0.4f, 0.4f));
                Vector3 p = g + new Vector3(o.x * lot.Size.x, 0.14f, o.y * lot.Size.y);
                int height = rng.Range(1, port ? 4 : 3);
                float yaw = rng.Chance(0.5f) ? 0f : 90f;
                for (int k = 0; k < height; k++)
                    PropFactory.Container(geo, p + Vector3.up * (k * 2.62f), yaw, ref rng);
            }
            if (port && rng.Chance(0.35f))
                PropFactory.PortCrane(geo, g + new Vector3(0f, 0.14f, lot.Size.y * 0.3f), rng.Chance(0.5f) ? 0f : 90f);
            if (rng.Chance(0.6f))
            {
                Vector3 a = g + new Vector3(-lot.Size.x * 0.5f, 0.14f, -lot.Size.y * 0.5f);
                Vector3 b = g + new Vector3(lot.Size.x * 0.5f, 0.14f, -lot.Size.y * 0.5f);
                PropFactory.Fence(geo, a, b, 3.2f, true);
            }
            PropFactory.StreetLamp(geo, g + new Vector3(-lot.Size.x * 0.42f, 0.14f, lot.Size.y * 0.42f), 0f, true);
        }

        private void BuildField(ChunkGeometry geo, Vector3 g, BuildingLot lot, ref Rng rng, int lod)
        {
            var crop = MaterialLibrary.Surface(SurfaceKind.Farmland, rng.Range(0, 3),
                new Color(rng.Range(0.75f, 1f), rng.Range(0.85f, 1f), rng.Range(0.55f, 0.85f)), 0.06f);
            int cells = lod == 0 ? 4 : 2;
            float step = lot.Size.x / cells;
            int sub = geo.Sub(crop);
            for (int x = 0; x < cells; x++)
            for (int z = 0; z < cells; z++)
            {
                float px = g.x - lot.Size.x * 0.5f + (x + 0.5f) * step;
                float pz = g.z - lot.Size.y * 0.5f + (z + 0.5f) * (lot.Size.y / cells);
                float py = _map.SampleHeight(px, pz);
                geo.Builder.AddBox(new Vector3(px, py + 0.12f, pz), new Vector3(step, 0.24f, lot.Size.y / cells), Quaternion.identity, 0.05f, sub);
            }
            if (lod == 0 && rng.Chance(0.5f))
            {
                Vector3 a = g + new Vector3(-lot.Size.x * 0.5f, 0f, -lot.Size.y * 0.5f);
                Vector3 b = g + new Vector3(lot.Size.x * 0.5f, 0f, -lot.Size.y * 0.5f);
                a.y = _map.SampleHeight(a.x, a.z); b.y = _map.SampleHeight(b.x, b.z);
                PropFactory.Fence(geo, a, b, 1.3f, false);
            }
        }

        private void BuildApron(ChunkGeometry geo, Vector3 g, BuildingLot lot, ref Rng rng, int lod)
        {
            var tarmac = MaterialLibrary.Surface(SurfaceKind.Asphalt, 1, new Color(0.90f, 0.90f, 0.92f), 0.18f);
            geo.Builder.AddBox(g + Vector3.up * 0.08f, new Vector3(lot.Size.x, 0.16f, lot.Size.y), Quaternion.identity, 0.08f, geo.Sub(tarmac));
            if (lod > 1) return;
            var paint = MaterialLibrary.Surface(SurfaceKind.RoadMarking, 0, new Color(1f, 0.85f, 0.2f), 0.05f);
            int sub = geo.Sub(paint);
            for (int i = 0; i < 12; i++)
            {
                float ox = (-0.5f + (i + 0.5f) / 12f) * lot.Size.x;
                geo.Builder.AddBox(g + new Vector3(ox, 0.17f, 0f), new Vector3(0.3f, 0.02f, lot.Size.y * 0.7f), Quaternion.identity, 0.3f, sub);
            }
        }

        // ------------------------------------------------------------------
        // Street furniture
        // ------------------------------------------------------------------
        private void BuildStreetFurniture(Vector2Int coord, Vector3 origin, float size, int lod, ChunkGeometry geo)
        {
            int cellX = Mathf.FloorToInt(origin.x / _cfg.chunkSize);
            int cellZ = Mathf.FloorToInt(origin.z / _cfg.chunkSize);
            _roads.CollectSegments(cellX, cellZ, _segBuffer);

            for (int i = 0; i < _segBuffer.Count; i++)
            {
                int si = _segBuffer[i];
                var s = _roads.Segments[si];
                Vector2 mid = s.Point(0.5f);
                if (Mathf.FloorToInt(mid.x / _cfg.chunkSize) != cellX || Mathf.FloorToInt(mid.y / _cfg.chunkSize) != cellZ) continue;

                var district = _map.DistrictAt(mid.x, mid.y);
                var profile = DistrictCatalog.Get(district);
                var rng = Rng.FromCoords(_cfg.seed, si, 0, 313);

                if (s.HasSidewalk)
                {
                    float spacing = profile.streetLightSpacing;
                    int lamps = Mathf.FloorToInt(s.Length / spacing);
                    for (int l = 0; l <= lamps; l++)
                    {
                        float t = lamps == 0 ? 0.5f : (float)l / lamps;
                        bool left = l % 2 == 0;
                        Vector2 p2 = s.Point(t) + s.Right * ((left ? -1f : 1f) * (s.HalfWidth + _cfg.sidewalkWidth * 0.7f));
                        Vector3 p = new Vector3(p2.x, _map.SampleHeight(p2.x, p2.y) + 0.17f, p2.y);
                        float yaw = Mathf.Atan2(s.Right.x * (left ? 1f : -1f), s.Right.y * (left ? 1f : -1f)) * Mathf.Rad2Deg;
                        PropFactory.StreetLamp(geo, p, yaw, s.Kind == RoadKind.Highway || s.Kind == RoadKind.Avenue);
                    }

                    if (lod == 0)
                    {
                        int extras = Mathf.RoundToInt(s.Length / 30f);
                        for (int e = 0; e < extras; e++)
                        {
                            float t = rng.Value;
                            bool left = rng.Chance(0.5f);
                            Vector2 p2 = s.Point(t) + s.Right * ((left ? -1f : 1f) * (s.HalfWidth + _cfg.sidewalkWidth * 0.55f));
                            Vector3 p = new Vector3(p2.x, _map.SampleHeight(p2.x, p2.y) + 0.17f, p2.y);
                            float roll = rng.Value;
                            float yaw = Mathf.Atan2(s.Right.x * (left ? 1f : -1f), s.Right.y * (left ? 1f : -1f)) * Mathf.Rad2Deg;
                            if (roll < 0.20f) PropFactory.Bin(geo, p, ref rng);
                            else if (roll < 0.32f) PropFactory.Hydrant(geo, p);
                            else if (roll < 0.44f) PropFactory.Bench(geo, p, yaw);
                            else if (roll < 0.52f) PropFactory.ParkingMeter(geo, p, yaw);
                            else if (roll < 0.58f && profile.pedDensity > 1f) PropFactory.BusStop(geo, p, yaw);
                            else if (roll < 0.58f + profile.treeDensity * 0.6f)
                                PropFactory.Tree(geo, p, ref rng,
                                    district == DistrictType.Beach || district == DistrictType.Marina ? TreeKind.Palm : TreeKind.Broadleaf, lod);
                        }
                    }
                }
                else if (s.Kind == RoadKind.Rural && lod == 0 && rng.Chance(0.4f))
                {
                    Vector2 p2 = s.Point(rng.Value) + s.Right * (s.HalfWidth + 8f);
                    Vector3 p = new Vector3(p2.x, _map.SampleHeight(p2.x, p2.y), p2.y);
                    PropFactory.PowerPylon(geo, p);
                }
            }

            // Traffic lights at signalled intersections inside this chunk.
            if (lod == 0)
            {
                for (int n = 0; n < _roads.Nodes.Count; n++)
                {
                    var node = _roads.Nodes[n];
                    if (!node.HasTrafficLight) continue;
                    if (node.Pos.x < origin.x || node.Pos.x >= origin.x + size ||
                        node.Pos.y < origin.z || node.Pos.y >= origin.z + size) continue;
                    for (int k = 0; k < node.Segments.Count && k < 4; k++)
                    {
                        var s = _roads.Segments[node.Segments[k]];
                        Vector2 dir = (s.NodeA == n) ? s.Dir : -s.Dir;
                        Vector2 right = new Vector2(dir.y, -dir.x);
                        Vector2 p2 = node.Pos + dir * (s.HalfWidth + 3.5f) + right * (s.HalfWidth + 2.2f);
                        Vector3 p = new Vector3(p2.x, _map.SampleHeight(p2.x, p2.y) + 0.17f, p2.y);
                        PropFactory.TrafficLight(geo, p, Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Wild vegetation and rocks
        // ------------------------------------------------------------------
        private void BuildNature(Vector2Int coord, Vector3 origin, float size, int lod, ChunkGeometry geo)
        {
            var districtCenter = _map.DistrictAt(origin.x + size * 0.5f, origin.z + size * 0.5f);
            var profile = DistrictCatalog.Get(districtCenter);
            if (profile.treeDensity <= 0.02f && districtCenter != DistrictType.Badlands) return;

            var rng = Rng.FromCoords(_cfg.seed, coord.x, coord.y, 1777);
            int attempts = lod == 0 ? Mathf.RoundToInt(90f * profile.treeDensity) : Mathf.RoundToInt(30f * profile.treeDensity);
            attempts = Mathf.Clamp(attempts, 0, 110);

            for (int i = 0; i < attempts; i++)
            {
                float x = origin.x + rng.Value * size;
                float z = origin.z + rng.Value * size;
                if (_map.Landness(x, z) < 12f) continue;
                if (_roads.RoadClearance(new Vector2(x, z), out _) < 6f) continue;
                var d = _map.DistrictAt(x, z);
                if (WorldMap.IsUrban(d) && d != DistrictType.Wealthy && d != DistrictType.Suburb) continue;
                float slope = _map.SampleSlope(x, z);
                if (slope > 38f) continue;
                float y = _map.SampleHeight(x, z);
                if (y > 480f) continue;

                Vector3 p = new Vector3(x, y, z);
                TreeKind kind;
                if (d == DistrictType.Beach || d == DistrictType.Marina) kind = TreeKind.Palm;
                else if (d == DistrictType.Badlands) kind = rng.Chance(0.55f) ? TreeKind.Bush : TreeKind.DeadTree;
                else if (y > 220f || d == DistrictType.Mountains) kind = TreeKind.Pine;
                else if (d == DistrictType.Forest) kind = rng.Chance(0.6f) ? TreeKind.Pine : TreeKind.Broadleaf;
                else kind = rng.Chance(0.25f) ? TreeKind.Bush : TreeKind.Broadleaf;

                PropFactory.Tree(geo, p, ref rng, kind, lod);

                if (rng.Chance(0.10f) && (d == DistrictType.Mountains || d == DistrictType.Badlands || slope > 20f))
                    PropFactory.Rock(geo, p + new Vector3(rng.Range(-6f, 6f), 0f, rng.Range(-6f, 6f)), ref rng, rng.Range(1.2f, 4.5f));
            }
        }
    }
}
