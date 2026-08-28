using System.Collections.Generic;
using UnityEngine;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    /// <summary>
    /// Analytic description of San Monica: terrain height, coastline, rivers and
    /// district layout across the full 16 x 16 km world. Everything is a pure
    /// function of world position plus the world seed, so any system can ask
    /// about any point of the map without the chunk being loaded.
    /// </summary>
    public class WorldMap
    {
        public readonly WorldConfig Config;
        private readonly float _seedOffset;

        // Coarse lookup grids built once at boot (256 x 256 cells of 64 m).
        private const int GridRes = 256;
        private byte[] _districtGrid;
        private float _cellSize;

        // ---- District anchors (world metres) ----
        public struct Anchor
        {
            public DistrictType Type;
            public Vector2 Center;
            public float Reach;
            public float Priority;
        }

        private readonly List<Anchor> _anchors = new List<Anchor>();

        public IReadOnlyList<Anchor> Anchors => _anchors;

        // Key landmark positions used by missions, shops and spawns.
        public Vector2 DowntownCenter = new Vector2(-1750f, 350f);
        public Vector2 PortCenter = new Vector2(-2950f, -1950f);
        public Vector2 MarinaCenter = new Vector2(-3150f, 950f);
        public Vector2 AirportCenter = new Vector2(2750f, -750f);
        public Vector2 AirportSize = new Vector2(1500f, 950f);
        public Vector2 UniversityCenter = new Vector2(650f, 1000f);
        public Vector2 CrestwoodCenter = new Vector2(1500f, 2300f);
        public Vector2 FoundryCenter = new Vector2(950f, -1850f);
        public Vector2 MarigoldCenter = new Vector2(-950f, -1250f);
        public Vector2 ParkCenter = new Vector2(-1250f, 1450f);

        public WorldMap(WorldConfig config)
        {
            Config = config;
            _seedOffset = (config.seed % 10007) * 0.137f;
            BuildAnchors();
        }

        private void BuildAnchors()
        {
            void A(DistrictType t, Vector2 c, float reach, float priority = 1f)
                => _anchors.Add(new Anchor { Type = t, Center = c, Reach = reach, Priority = priority });

            A(DistrictType.Downtown, DowntownCenter, 850f, 1.5f);
            A(DistrictType.Commercial, new Vector2(-2450f, 1100f), 780f);
            A(DistrictType.Commercial, new Vector2(-700f, 900f), 720f);
            A(DistrictType.Commercial, new Vector2(-1500f, -600f), 700f);
            A(DistrictType.Marigold, MarigoldCenter, 820f, 1.15f);
            A(DistrictType.Residential, new Vector2(150f, 1750f), 950f);
            A(DistrictType.Residential, new Vector2(-450f, -2500f), 900f);
            A(DistrictType.Residential, new Vector2(1350f, 450f), 780f);
            A(DistrictType.University, UniversityCenter, 620f, 1.2f);
            A(DistrictType.Park, ParkCenter, 560f, 1.3f);
            A(DistrictType.Wealthy, CrestwoodCenter, 1150f, 1.1f);
            A(DistrictType.Wealthy, new Vector2(2450f, 3250f), 850f);
            A(DistrictType.Industrial, FoundryCenter, 1050f, 1.2f);
            A(DistrictType.Industrial, new Vector2(1850f, -2650f), 800f);
            A(DistrictType.Port, PortCenter, 1000f, 1.6f);
            A(DistrictType.Marina, MarinaCenter, 620f, 1.5f);
            A(DistrictType.Airport, AirportCenter, 1150f, 1.8f);
            A(DistrictType.Suburb, new Vector2(2500f, 2450f), 1100f);
            A(DistrictType.Suburb, new Vector2(3100f, 1150f), 950f);
            A(DistrictType.Suburb, new Vector2(1900f, -3600f), 900f);
            A(DistrictType.Farmland, new Vector2(4500f, 2100f), 1700f);
            A(DistrictType.Farmland, new Vector2(3900f, -2600f), 1400f);
            A(DistrictType.Forest, new Vector2(3600f, 4200f), 2100f);
            A(DistrictType.Forest, new Vector2(6100f, 1200f), 2400f);
            A(DistrictType.Mountains, new Vector2(5600f, 4600f), 2600f);
            A(DistrictType.Mountains, new Vector2(1200f, 5800f), 2400f);
            A(DistrictType.Badlands, new Vector2(5200f, -2800f), 2300f);
            A(DistrictType.Badlands, new Vector2(2200f, -5600f), 2200f);
            A(DistrictType.Forest, new Vector2(-1200f, 4600f), 1900f);
            A(DistrictType.Farmland, new Vector2(-2600f, 3600f), 1500f);
        }

        // ------------------------------------------------------------------
        // Coastline & water
        // ------------------------------------------------------------------

        /// <summary>Western shoreline x coordinate for a given z, perturbed by noise.</summary>
        public float ShoreX(float z)
        {
            float n = Noise.Fbm(z * 0.00035f + _seedOffset, 11.3f, 4);
            float n2 = Noise.Fbm(z * 0.0012f + _seedOffset, 27.1f, 3);
            return -3550f + n * 620f + n2 * 180f;
        }

        /// <summary>Positive on land, negative in the sea. Units are roughly metres.</summary>
        public float Landness(float x, float z)
        {
            float land = x - ShoreX(z);

            // Halcyon Bay carves inland behind the port and marina.
            Vector2 bayC = new Vector2(-2850f, -700f);
            float bay = Ellipse(x, z, bayC, 1450f, 1750f);
            float bayNoise = Noise.Fbm(x * 0.0009f, z * 0.0009f + 5.2f, 3) * 260f;
            land = Mathf.Min(land, bay + bayNoise);

            // Southern ocean.
            float southShore = -4200f + Noise.Fbm(x * 0.0004f + 3.1f, 21.7f, 4) * 700f;
            land = Mathf.Min(land, z - southShore);

            // Redwater River from the north-east mountains to the bay.
            land = Mathf.Min(land, RiverDistance(x, z));

            return land;
        }

        private static float Ellipse(float x, float z, Vector2 c, float rx, float rz)
        {
            float dx = (x - c.x) / rx, dz = (z - c.y) / rz;
            return (Mathf.Sqrt(dx * dx + dz * dz) - 1f) * Mathf.Min(rx, rz);
        }

        /// <summary>Distance from the centreline of the Redwater River (metres).</summary>
        public float RiverDistance(float x, float z)
        {
            // River runs roughly from (4200, 4200) down to the bay at (-2300, -900).
            float t = Mathf.InverseLerp(4200f, -2300f, x);
            if (t < -0.15f || t > 1.15f) return 9999f;
            t = Mathf.Clamp01(t);
            float centerZ = Mathf.Lerp(4200f, -900f, t) + Noise.Fbm(x * 0.0011f + 9.4f, 3.3f, 4) * 620f;
            float d = Mathf.Abs(z - centerZ);
            float width = Mathf.Lerp(34f, 130f, t);
            return d - width;
        }

        public bool IsWater(float x, float z) => Landness(x, z) < 0f;

        public bool IsWater(Vector3 p) => IsWater(p.x, p.z);

        // ------------------------------------------------------------------
        // Terrain height
        // ------------------------------------------------------------------

        /// <summary>0 in open country, 1 inside the flattened urban core.</summary>
        public float UrbanMask(float x, float z)
        {
            float best = 0f;
            for (int i = 0; i < _anchors.Count; i++)
            {
                var a = _anchors[i];
                if (!IsUrban(a.Type)) continue;
                float d = Vector2.Distance(new Vector2(x, z), a.Center);
                float m = Mathf.Clamp01(1f - (d - a.Reach * 0.55f) / (a.Reach * 0.85f));
                if (m > best) best = m;
            }
            return Mathf.SmoothStep(0f, 1f, best);
        }

        public static bool IsUrban(DistrictType t)
        {
            switch (t)
            {
                case DistrictType.Downtown:
                case DistrictType.Commercial:
                case DistrictType.Marigold:
                case DistrictType.Residential:
                case DistrictType.Wealthy:
                case DistrictType.Industrial:
                case DistrictType.Port:
                case DistrictType.Airport:
                case DistrictType.University:
                case DistrictType.Marina:
                case DistrictType.Suburb:
                    return true;
                default:
                    return false;
            }
        }

        public float SampleHeight(float x, float z)
        {
            float land = Landness(x, z);

            // --- Sea bed ---
            if (land < 0f)
            {
                float depth = Mathf.Min(60f, -land * 0.055f + 1.5f);
                float bed = -depth - Noise.Fbm(x * 0.0022f, z * 0.0022f, 3) * 4f;
                return Mathf.Min(bed, -0.6f);
            }

            // --- Base rolling country ---
            float baseH = 8f + Noise.Fbm(x * 0.00042f + _seedOffset, z * 0.00042f, 5) * 46f;

            // Mountains (north-east and north).
            float mtn = 0f;
            mtn += MountainMass(x, z, new Vector2(5600f, 4600f), 2900f, 620f);
            mtn += MountainMass(x, z, new Vector2(1200f, 5800f), 2600f, 480f);
            mtn += MountainMass(x, z, new Vector2(6400f, -1000f), 2300f, 380f);

            // Crestwood Hills behind the city.
            float hills = Bump(x, z, CrestwoodCenter, 1500f) * 118f;
            hills += Bump(x, z, new Vector2(2450f, 3250f), 1200f) * 86f;

            // Badlands mesas.
            float mesa = 0f;
            float mesaMask = Bump(x, z, new Vector2(5200f, -2800f), 2500f) + Bump(x, z, new Vector2(2200f, -5600f), 2300f);
            if (mesaMask > 0.01f)
            {
                float step = Noise.Fbm(x * 0.0016f + 41.7f, z * 0.0016f, 3);
                float terrace = Mathf.Floor(Mathf.Clamp01(step * 0.5f + 0.5f) * 4f) / 4f;
                mesa = terrace * 150f * Mathf.Clamp01(mesaMask);
            }

            float h = baseH + mtn + hills + mesa;

            // --- Beach ramp near the shore ---
            float beach = Mathf.Clamp01(land / 190f);
            h = Mathf.Lerp(0.6f, h, Mathf.SmoothStep(0f, 1f, beach));

            // --- River valley ---
            float riverD = RiverDistance(x, z);
            if (riverD < 320f)
            {
                float t = Mathf.Clamp01((riverD - 30f) / 290f);
                float valley = Mathf.Lerp(-3.5f, h, Mathf.SmoothStep(0f, 1f, t));
                h = Mathf.Min(h, valley);
            }

            // --- Flatten the city ---
            float urban = UrbanMask(x, z);
            if (urban > 0.001f)
            {
                float plateau = 11f + Noise.Fbm(x * 0.00025f + 71f, z * 0.00025f, 2) * 7f;
                // Crestwood keeps some of its slope so the villas sit on a hillside.
                float target = Mathf.Lerp(plateau, Mathf.Max(plateau, h * 0.72f), Bump(x, z, CrestwoodCenter, 1500f));
                h = Mathf.Lerp(h, target, urban);
            }

            // Airport apron is dead flat.
            float ap = RectMask(x, z, AirportCenter, AirportSize + new Vector2(220f, 220f), 260f);
            if (ap > 0f) h = Mathf.Lerp(h, 14f, ap);

            return Mathf.Max(h, land < 24f ? 0.4f : 0.8f);
        }

        private float MountainMass(float x, float z, Vector2 c, float radius, float peak)
        {
            float b = Bump(x, z, c, radius);
            if (b <= 0.001f) return 0f;
            float ridged = Noise.Ridged(x * 0.00085f + _seedOffset, z * 0.00085f, 5);
            float detail = Noise.Fbm(x * 0.004f, z * 0.004f, 4) * 0.15f;
            return Mathf.Pow(b, 1.55f) * peak * (ridged * 0.85f + 0.25f + detail);
        }

        private static float Bump(float x, float z, Vector2 c, float radius)
        {
            float d = Vector2.Distance(new Vector2(x, z), c) / radius;
            if (d >= 1f) return 0f;
            float t = 1f - d;
            return t * t * (3f - 2f * t);
        }

        private static float RectMask(float x, float z, Vector2 c, Vector2 halfSize, float feather)
        {
            float dx = Mathf.Abs(x - c.x) - halfSize.x;
            float dz = Mathf.Abs(z - c.y) - halfSize.y;
            float d = Mathf.Max(dx, dz);
            if (d <= 0f) return 1f;
            if (d >= feather) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, d / feather);
        }

        public Vector3 SurfacePoint(float x, float z) => new Vector3(x, SampleHeight(x, z), z);

        /// <summary>Approximate surface normal, used to reject building sites on steep ground.</summary>
        public Vector3 SampleNormal(float x, float z, float e = 4f)
        {
            float hl = SampleHeight(x - e, z), hr = SampleHeight(x + e, z);
            float hd = SampleHeight(x, z - e), hu = SampleHeight(x, z + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        public float SampleSlope(float x, float z)
        {
            var n = SampleNormal(x, z);
            return Mathf.Acos(Mathf.Clamp01(n.y)) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------
        // Districts
        // ------------------------------------------------------------------

        public void BuildDistrictGrid()
        {
            _cellSize = Config.worldSize / GridRes;
            _districtGrid = new byte[GridRes * GridRes];
            float half = Config.HalfSize;
            for (int gz = 0; gz < GridRes; gz++)
            {
                float z = -half + (gz + 0.5f) * _cellSize;
                for (int gx = 0; gx < GridRes; gx++)
                {
                    float x = -half + (gx + 0.5f) * _cellSize;
                    _districtGrid[gz * GridRes + gx] = (byte)ComputeDistrict(x, z);
                }
            }
        }

        public DistrictType DistrictAt(float x, float z)
        {
            if (_districtGrid == null) return ComputeDistrict(x, z);
            float half = Config.HalfSize;
            int gx = Mathf.Clamp(Mathf.FloorToInt((x + half) / _cellSize), 0, GridRes - 1);
            int gz = Mathf.Clamp(Mathf.FloorToInt((z + half) / _cellSize), 0, GridRes - 1);
            return (DistrictType)_districtGrid[gz * GridRes + gx];
        }

        public DistrictType DistrictAt(Vector3 p) => DistrictAt(p.x, p.z);

        public DistrictProfile ProfileAt(Vector3 p) => DistrictCatalog.Get(DistrictAt(p.x, p.z));

        /// <summary>The expensive, exact district classification (cached into the grid).</summary>
        public DistrictType ComputeDistrict(float x, float z)
        {
            float land = Landness(x, z);
            if (land < 0f) return DistrictType.Ocean;
            if (land < 165f && SampleHeight(x, z) < 9f) return DistrictType.Beach;

            // Airport is an explicit rectangle so runways stay intact.
            if (RectMask(x, z, AirportCenter, AirportSize, 90f) > 0.5f) return DistrictType.Airport;

            float bestScore = 0f;
            DistrictType best = DistrictType.Forest;
            var p = new Vector2(x, z);
            for (int i = 0; i < _anchors.Count; i++)
            {
                var a = _anchors[i];
                float d = Vector2.Distance(p, a.Center);
                if (d > a.Reach * 1.9f) continue;
                float score = a.Priority * a.Reach / (d + 60f);
                if (score > bestScore) { bestScore = score; best = a.Type; }
            }

            if (bestScore < 0.55f)
            {
                float h = SampleHeight(x, z);
                if (h > 260f) return DistrictType.Mountains;
                if (h > 90f) return DistrictType.Forest;
                return DistrictType.Farmland;
            }
            return best;
        }

        public string DistrictName(Vector3 p)
        {
            var t = DistrictAt(p);
            // Use the nearest matching anchor to give sub-areas distinct names.
            return DistrictCatalog.Get(t).displayName;
        }

        /// <summary>Finds a safe, dry, walkable point near the requested position.</summary>
        public Vector3 FindGroundPoint(Vector3 near, float searchRadius = 60f)
        {
            if (!IsWater(near.x, near.z))
                return new Vector3(near.x, SampleHeight(near.x, near.z), near.z);

            var rng = new SanMonica.Core.Rng(Mathf.RoundToInt(near.x * 7f + near.z * 13f));
            for (int i = 0; i < 24; i++)
            {
                Vector2 o = rng.InsideUnitCircle() * searchRadius * (1f + i * 0.4f);
                float x = near.x + o.x, z = near.y == 0f ? near.z + o.y : near.z + o.y;
                if (!IsWater(x, z)) return new Vector3(x, SampleHeight(x, z), z);
            }
            return new Vector3(near.x, Mathf.Max(SampleHeight(near.x, near.z), 1f), near.z);
        }
    }
}
