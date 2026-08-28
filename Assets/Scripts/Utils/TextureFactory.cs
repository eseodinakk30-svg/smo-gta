using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Utils
{
    public enum SurfaceKind
    {
        Asphalt, Concrete, Sidewalk, Brick, Plaster, GlassCurtain, OfficeWindows, ResidentialWindows,
        Roof, MetalPanel, RustedMetal, Wood, Grass, Sand, Rock, Dirt, Tile, ShopFront, Marble,
        CarPaint, Chrome, TireRubber, Foliage, RoadMarking, Container, Farmland, Snow, WaterFoam
    }

    /// <summary>
    /// Generates every texture in the game procedurally (albedo, normal, mask).
    /// Textures are cached, mip-mapped and compressed so streaming stays cheap.
    /// </summary>
    public static class TextureFactory
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        public static int BaseResolution = 256;

        public static void SetResolution(int res) => BaseResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(res), 32, 512);

        public static void ClearCache()
        {
            foreach (var kv in Cache) if (kv.Value != null) Object.Destroy(kv.Value);
            Cache.Clear();
        }

        public static Texture2D Get(SurfaceKind kind, int variant = 0, Color? tint = null)
        {
            Color t = tint ?? Color.white;
            string key = kind + "_" + variant + "_" + ColorKey(t) + "_" + BaseResolution;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int res = ResolutionFor(kind);
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true, false)
            {
                name = "SMO_" + key,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            var px = new Color32[res * res];
            Paint(kind, variant, t, res, px);
            tex.SetPixels32(px);
            tex.Apply(true, false);
            tex.Compress(false);
            tex.Apply(false, true);
            Cache[key] = tex;
            return tex;
        }

        /// <summary>Derives a tangent space normal map from the luminance of a generated albedo.</summary>
        public static Texture2D GetNormal(SurfaceKind kind, int variant = 0, float strength = 1f)
        {
            string key = "N_" + kind + "_" + variant + "_" + Mathf.RoundToInt(strength * 10f) + "_" + BaseResolution;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int res = ResolutionFor(kind);
            var height = new float[res * res];
            var tmp = new Color32[res * res];
            Paint(kind, variant, Color.white, res, tmp);
            for (int i = 0; i < tmp.Length; i++)
                height[i] = (tmp[i].r * 0.299f + tmp[i].g * 0.587f + tmp[i].b * 0.114f) / 255f;

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true, true)
            {
                name = "SMO_" + key,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[res * res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float hl = height[y * res + ((x - 1 + res) % res)];
                float hr = height[y * res + ((x + 1) % res)];
                float hd = height[((y - 1 + res) % res) * res + x];
                float hu = height[((y + 1) % res) * res + x];
                Vector3 n = new Vector3((hl - hr) * strength * 3f, (hd - hu) * strength * 3f, 1f).normalized;
                px[y * res + x] = new Color32(
                    (byte)((n.x * 0.5f + 0.5f) * 255f),
                    (byte)((n.y * 0.5f + 0.5f) * 255f),
                    (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            tex.Compress(false);
            tex.Apply(false, true);
            Cache[key] = tex;
            return tex;
        }

        private static int ResolutionFor(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.OfficeWindows:
                case SurfaceKind.ResidentialWindows:
                case SurfaceKind.GlassCurtain:
                case SurfaceKind.ShopFront:
                    return Mathf.Max(64, BaseResolution);
                case SurfaceKind.Chrome:
                case SurfaceKind.CarPaint:
                case SurfaceKind.RoadMarking:
                    return Mathf.Max(32, BaseResolution / 4);
                default:
                    return Mathf.Max(64, BaseResolution / 2);
            }
        }

        private static string ColorKey(Color c) =>
            ((int)(c.r * 15) << 8 | (int)(c.g * 15) << 4 | (int)(c.b * 15)).ToString("X3");

        private static void Paint(SurfaceKind kind, int variant, Color tint, int res, Color32[] px)
        {
            var rng = new SanMonica.Core.Rng((int)kind * 7919 + variant * 104729);
            float inv = 1f / res;
            switch (kind)
            {
                case SurfaceKind.OfficeWindows:
                case SurfaceKind.ResidentialWindows:
                case SurfaceKind.GlassCurtain:
                case SurfaceKind.ShopFront:
                    PaintWindows(kind, variant, tint, res, px, ref rng);
                    return;
                case SurfaceKind.Brick:
                    PaintBrick(tint, res, px, ref rng);
                    return;
                case SurfaceKind.RoadMarking:
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(240, 238, 225, 255);
                    return;
                case SurfaceKind.Container:
                    PaintContainer(tint, res, px, ref rng);
                    return;
                case SurfaceKind.Farmland:
                    PaintFarmland(tint, res, px);
                    return;
            }

            Color baseColor = BaseColorFor(kind, variant) * tint;
            float grain = GrainFor(kind);
            float scale = ScaleFor(kind);

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = x * inv, v = y * inv;
                float n = Noise.Fbm(u * scale, v * scale, 4) * 0.5f + 0.5f;
                float detail = Noise.Hash(x * 1.31f, y * 2.17f);
                float f = Mathf.Lerp(1f - grain, 1f + grain, n) + (detail - 0.5f) * grain * 0.6f;

                Color c = baseColor * f;

                switch (kind)
                {
                    case SurfaceKind.Asphalt:
                        if (Noise.Worley(u * 14f, v * 14f) < 0.09f) c *= 0.72f;
                        break;
                    case SurfaceKind.Sidewalk:
                    {
                        float gx = Mathf.Repeat(u * 6f, 1f), gy = Mathf.Repeat(v * 6f, 1f);
                        if (gx < 0.03f || gy < 0.03f) c *= 0.7f;
                        break;
                    }
                    case SurfaceKind.Tile:
                    case SurfaceKind.Marble:
                    {
                        float gx = Mathf.Repeat(u * 8f, 1f), gy = Mathf.Repeat(v * 8f, 1f);
                        if (gx < 0.04f || gy < 0.04f) c *= 0.82f;
                        if (kind == SurfaceKind.Marble)
                            c = Color.Lerp(c, Color.white, Mathf.Pow(Mathf.Abs(Noise.Fbm(u * 5f, v * 5f, 5)), 0.5f) * 0.6f);
                        break;
                    }
                    case SurfaceKind.Wood:
                    {
                        float rings = Mathf.Sin((v * 22f + Noise.Fbm(u * 3f, v * 3f, 3) * 3f) * Mathf.PI) * 0.5f + 0.5f;
                        c *= Mathf.Lerp(0.82f, 1.08f, rings);
                        break;
                    }
                    case SurfaceKind.MetalPanel:
                    {
                        float gy2 = Mathf.Repeat(v * 5f, 1f);
                        if (gy2 < 0.05f) c *= 0.75f;
                        break;
                    }
                    case SurfaceKind.RustedMetal:
                    {
                        float rust = Mathf.Clamp01(Noise.Fbm(u * 7f, v * 7f, 5) * 1.6f + 0.35f);
                        c = Color.Lerp(c, new Color(0.42f, 0.20f, 0.09f), rust * 0.85f);
                        break;
                    }
                    case SurfaceKind.Grass:
                    case SurfaceKind.Foliage:
                    {
                        float blade = Noise.Hash(x * 3.7f, y * 1.9f);
                        c *= Mathf.Lerp(0.78f, 1.18f, blade);
                        c = Color.Lerp(c, new Color(0.42f, 0.46f, 0.22f), Mathf.Clamp01(Noise.Fbm(u * 3f, v * 3f, 3)) * 0.35f);
                        break;
                    }
                    case SurfaceKind.Rock:
                    {
                        float crack = Noise.Worley(u * 9f, v * 9f);
                        c *= Mathf.Lerp(0.55f, 1.1f, Mathf.SmoothStep(0f, 0.35f, crack));
                        break;
                    }
                    case SurfaceKind.Sand:
                    {
                        float ripple = Mathf.Sin((u * 40f + Noise.Fbm(u * 4f, v * 4f, 3) * 6f) * Mathf.PI) * 0.5f + 0.5f;
                        c *= Mathf.Lerp(0.94f, 1.06f, ripple);
                        break;
                    }
                    case SurfaceKind.Roof:
                    {
                        float gy3 = Mathf.Repeat(v * 16f, 1f);
                        if (gy3 < 0.12f) c *= 0.8f;
                        break;
                    }
                    case SurfaceKind.Chrome:
                        c = Color.Lerp(new Color(0.72f, 0.75f, 0.8f), Color.white, n);
                        break;
                    case SurfaceKind.TireRubber:
                    {
                        float tread = Mathf.Repeat(v * 24f, 1f);
                        c = new Color(0.07f, 0.07f, 0.075f) * (tread < 0.45f ? 0.75f : 1f);
                        break;
                    }
                    case SurfaceKind.Snow:
                        c = Color.Lerp(new Color(0.88f, 0.91f, 0.96f), Color.white, n);
                        break;
                }

                px[y * res + x] = new Color32(
                    (byte)(Mathf.Clamp01(c.r) * 255f),
                    (byte)(Mathf.Clamp01(c.g) * 255f),
                    (byte)(Mathf.Clamp01(c.b) * 255f),
                    255);
            }
        }

        private static void PaintWindows(SurfaceKind kind, int variant, Color tint, int res, Color32[] px, ref SanMonica.Core.Rng rng)
        {
            int cols = kind == SurfaceKind.OfficeWindows ? 8 : (kind == SurfaceKind.GlassCurtain ? 12 : 5);
            int rows = kind == SurfaceKind.ShopFront ? 2 : cols;
            Color frame = kind == SurfaceKind.GlassCurtain
                ? new Color(0.18f, 0.20f, 0.23f)
                : new Color(0.30f, 0.29f, 0.28f) * tint;
            Color glassDay = new Color(0.30f, 0.42f, 0.55f);

            int cellW = Mathf.Max(2, res / cols);
            int cellH = Mathf.Max(2, res / rows);
            int border = Mathf.Max(1, cellW / 10);

            var lit = new bool[cols * rows];
            var litColor = new Color[cols * rows];
            for (int i = 0; i < lit.Length; i++)
            {
                lit[i] = rng.Chance(0.42f);
                litColor[i] = Color.Lerp(new Color(1f, 0.86f, 0.62f), new Color(0.75f, 0.86f, 1f), rng.Value * 0.6f);
            }

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                int cx = Mathf.Min(cols - 1, x / cellW), cy = Mathf.Min(rows - 1, y / cellH);
                int lx = x - cx * cellW, ly = y - cy * cellH;
                bool isFrame = lx < border || ly < border || lx > cellW - border - 1 || ly > cellH - border - 1;
                Color c;
                if (isFrame) c = frame;
                else
                {
                    int idx = cy * cols + cx;
                    // Alpha channel doubles as the emissive mask consumed by the window material.
                    float grad = 1f - (float)ly / cellH * 0.35f;
                    c = lit[idx] ? litColor[idx] * grad : glassDay * grad * tint;
                }
                byte emissive = (!isFrame && lit[cy * cols + cx]) ? (byte)255 : (byte)0;
                px[y * res + x] = new Color32(
                    (byte)(Mathf.Clamp01(c.r) * 255f),
                    (byte)(Mathf.Clamp01(c.g) * 255f),
                    (byte)(Mathf.Clamp01(c.b) * 255f),
                    emissive);
            }
        }

        private static void PaintBrick(Color tint, int res, Color32[] px, ref SanMonica.Core.Rng rng)
        {
            int rows = 16;
            int bh = Mathf.Max(2, res / rows);
            int bw = bh * 2;
            Color mortar = new Color(0.72f, 0.70f, 0.66f);
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                int row = y / bh;
                int offset = (row % 2) * (bw / 2);
                int bx = (x + offset) % bw;
                int by = y % bh;
                bool isMortar = bx < 2 || by < 2;
                Color c;
                if (isMortar) c = mortar;
                else
                {
                    var r = SanMonica.Core.Rng.FromCoords(41, (x + offset) / bw, row);
                    float shade = 0.82f + r.Value * 0.36f;
                    c = new Color(0.55f, 0.24f, 0.18f) * tint * shade;
                    c *= 0.92f + Noise.Hash(x * 0.7f, y * 0.9f) * 0.16f;
                }
                px[y * res + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 255);
            }
        }

        private static void PaintContainer(Color tint, int res, Color32[] px, ref SanMonica.Core.Rng rng)
        {
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float rib = Mathf.Repeat(x / (float)res * 18f, 1f);
                float shade = rib < 0.5f ? 0.86f : 1.05f;
                Color c = tint * shade;
                float rust = Mathf.Clamp01(Noise.Fbm(x / (float)res * 6f, y / (float)res * 6f, 4) * 1.3f);
                c = Color.Lerp(c, new Color(0.38f, 0.19f, 0.10f), Mathf.Max(0f, rust - 0.55f) * 0.9f);
                px[y * res + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 255);
            }
        }

        private static void PaintFarmland(Color tint, int res, Color32[] px)
        {
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float furrow = Mathf.Sin(x / (float)res * 60f * Mathf.PI) * 0.5f + 0.5f;
                Color c = Color.Lerp(new Color(0.36f, 0.28f, 0.18f), new Color(0.52f, 0.47f, 0.24f), furrow) * tint;
                c *= 0.9f + Noise.Fbm(x / (float)res * 8f, y / (float)res * 8f, 3) * 0.2f;
                px[y * res + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 255);
            }
        }

        private static Color BaseColorFor(SurfaceKind kind, int variant)
        {
            switch (kind)
            {
                case SurfaceKind.Asphalt: return new Color(0.155f, 0.158f, 0.168f);
                case SurfaceKind.Concrete: return new Color(0.60f, 0.60f, 0.585f);
                case SurfaceKind.Sidewalk: return new Color(0.66f, 0.65f, 0.63f);
                case SurfaceKind.Plaster: return new Color(0.80f, 0.77f, 0.70f);
                case SurfaceKind.Roof: return new Color(0.28f, 0.27f, 0.27f);
                case SurfaceKind.MetalPanel: return new Color(0.55f, 0.57f, 0.60f);
                case SurfaceKind.RustedMetal: return new Color(0.48f, 0.46f, 0.44f);
                case SurfaceKind.Wood: return new Color(0.47f, 0.33f, 0.20f);
                case SurfaceKind.Grass: return new Color(0.28f, 0.42f, 0.18f);
                case SurfaceKind.Foliage: return new Color(0.22f, 0.38f, 0.16f);
                case SurfaceKind.Sand: return new Color(0.80f, 0.72f, 0.53f);
                case SurfaceKind.Rock: return new Color(0.42f, 0.40f, 0.38f);
                case SurfaceKind.Dirt: return new Color(0.40f, 0.31f, 0.21f);
                case SurfaceKind.Tile: return new Color(0.72f, 0.72f, 0.70f);
                case SurfaceKind.Marble: return new Color(0.85f, 0.84f, 0.82f);
                case SurfaceKind.CarPaint: return Color.white;
                case SurfaceKind.Chrome: return new Color(0.78f, 0.80f, 0.83f);
                case SurfaceKind.TireRubber: return new Color(0.08f, 0.08f, 0.085f);
                case SurfaceKind.Snow: return new Color(0.92f, 0.94f, 0.98f);
                case SurfaceKind.WaterFoam: return new Color(0.86f, 0.92f, 0.95f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private static float GrainFor(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Asphalt: return 0.16f;
                case SurfaceKind.Concrete: return 0.10f;
                case SurfaceKind.Grass:
                case SurfaceKind.Foliage: return 0.22f;
                case SurfaceKind.Rock: return 0.20f;
                case SurfaceKind.CarPaint: return 0.015f;
                case SurfaceKind.Chrome: return 0.05f;
                default: return 0.09f;
            }
        }

        private static float ScaleFor(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Asphalt: return 9f;
                case SurfaceKind.Grass: return 12f;
                case SurfaceKind.Rock: return 6f;
                case SurfaceKind.Sand: return 7f;
                default: return 5f;
            }
        }
    }
}
