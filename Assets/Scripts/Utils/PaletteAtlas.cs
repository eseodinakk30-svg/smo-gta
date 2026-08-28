using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Utils
{
    /// <summary>
    /// A single 256-colour atlas shared by every character and vehicle in the
    /// game. Each body panel or car part simply points its UVs at one cell, so
    /// an entire pedestrian or car renders with one material - which is what
    /// makes hundreds of unique-looking agents affordable on a phone.
    /// </summary>
    public static class PaletteAtlas
    {
        public const int Cells = 16;               // 16 x 16 = 256 palette entries
        private const int CellPixels = 16;
        private const int Size = Cells * CellPixels;

        private static Texture2D _texture;
        private static Color[] _palette;
        private static readonly Dictionary<int, int> _lookup = new Dictionary<int, int>(512);

        private static Material _matte, _glossy, _metal, _transparent, _emissive;

        public static Texture2D Texture
        {
            get { if (_texture == null) Build(); return _texture; }
        }

        private static void Build()
        {
            _palette = new Color[Cells * Cells];
            int i = 0;

            // 0-15 neutral ramp
            for (int k = 0; k < 16; k++) _palette[i++] = Color.Lerp(new Color(0.03f, 0.03f, 0.04f), Color.white, k / 15f);
            // 16-31 skin tones
            Color[] skins = {
                new Color(0.98f,0.86f,0.76f), new Color(0.96f,0.83f,0.72f), new Color(0.93f,0.79f,0.67f),
                new Color(0.90f,0.75f,0.62f), new Color(0.85f,0.70f,0.56f), new Color(0.80f,0.63f,0.48f),
                new Color(0.73f,0.56f,0.42f), new Color(0.66f,0.48f,0.35f), new Color(0.58f,0.42f,0.30f),
                new Color(0.50f,0.35f,0.25f), new Color(0.43f,0.30f,0.21f), new Color(0.36f,0.25f,0.18f),
                new Color(0.30f,0.21f,0.15f), new Color(0.27f,0.19f,0.14f), new Color(0.23f,0.16f,0.12f),
                new Color(0.19f,0.13f,0.10f)
            };
            for (int k = 0; k < skins.Length; k++) _palette[i++] = skins[k];
            // 32-47 hair
            Color[] hair = {
                new Color(0.06f,0.05f,0.05f), new Color(0.12f,0.09f,0.07f), new Color(0.20f,0.13f,0.08f),
                new Color(0.28f,0.18f,0.10f), new Color(0.36f,0.24f,0.13f), new Color(0.48f,0.34f,0.18f),
                new Color(0.62f,0.48f,0.26f), new Color(0.76f,0.64f,0.38f), new Color(0.86f,0.80f,0.62f),
                new Color(0.55f,0.24f,0.10f), new Color(0.70f,0.70f,0.72f), new Color(0.86f,0.86f,0.88f),
                new Color(0.35f,0.10f,0.45f), new Color(0.10f,0.35f,0.50f), new Color(0.55f,0.10f,0.20f),
                new Color(0.15f,0.45f,0.25f)
            };
            for (int k = 0; k < hair.Length; k++) _palette[i++] = hair[k];

            // 48-255 hue/saturation/value grid
            int hues = 13, sats = 4, vals = 4;
            for (int h = 0; h < hues && i < _palette.Length; h++)
            for (int s = 0; s < sats && i < _palette.Length; s++)
            for (int v = 0; v < vals && i < _palette.Length; v++)
            {
                float hue = h / (float)hues;
                float sat = Mathf.Lerp(0.15f, 1f, s / (float)(sats - 1));
                float val = Mathf.Lerp(0.14f, 1f, v / (float)(vals - 1));
                _palette[i++] = Color.HSVToRGB(hue, sat, val);
            }
            while (i < _palette.Length) _palette[i++] = Color.grey;

            _texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            {
                name = "SMO_PaletteAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int cx = x / CellPixels, cy = y / CellPixels;
                Color c = _palette[cy * Cells + cx];
                px[y * Size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
            }
            _texture.SetPixels32(px);
            _texture.Apply(true, false);
        }

        public static Color ColorOf(int index)
        {
            if (_palette == null) Build();
            return _palette[Mathf.Clamp(index, 0, _palette.Length - 1)];
        }

        /// <summary>Quantises a colour to the nearest atlas cell and returns its index.</summary>
        public static int Register(Color c)
        {
            if (_palette == null) Build();
            int key = (Mathf.RoundToInt(c.r * 63f) << 12) | (Mathf.RoundToInt(c.g * 63f) << 6) | Mathf.RoundToInt(c.b * 63f);
            if (_lookup.TryGetValue(key, out int cached)) return cached;

            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < _palette.Length; i++)
            {
                float dr = _palette[i].r - c.r, dg = _palette[i].g - c.g, db = _palette[i].b - c.b;
                float d = dr * dr * 0.9f + dg * dg * 1.2f + db * db * 0.7f;
                if (d < bestD) { bestD = d; best = i; }
            }
            _lookup[key] = best;
            return best;
        }

        /// <summary>UV at the centre of a palette cell.</summary>
        public static Vector2 UV(int index)
        {
            index = Mathf.Clamp(index, 0, Cells * Cells - 1);
            int cx = index % Cells, cy = index / Cells;
            return new Vector2((cx + 0.5f) / Cells, (cy + 0.5f) / Cells);
        }

        public static Vector2 UV(Color c) => UV(Register(c));

        // ------------------------------------------------------------------
        public static Material Matte => _matte != null ? _matte : (_matte = Create("Atlas_Matte", 0.10f, 0f, false, false));
        public static Material Glossy => _glossy != null ? _glossy : (_glossy = Create("Atlas_Glossy", 0.72f, 0.05f, false, false));
        public static Material Metal => _metal != null ? _metal : (_metal = Create("Atlas_Metal", 0.80f, 0.85f, false, false));
        public static Material Transparent => _transparent != null ? _transparent : (_transparent = Create("Atlas_Glass", 0.94f, 0.2f, true, false));
        public static Material Emissive => _emissive != null ? _emissive : (_emissive = Create("Atlas_Emissive", 0.4f, 0f, false, true));

        private static Material Create(string name, float smoothness, float metallic, bool transparent, bool emissive)
        {
            var shader = MaterialLibrary.UseSimpleLit ? MaterialLibrary.SimpleLitShader : MaterialLibrary.LitShader;
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetTexture("_BaseMap", Texture);
            m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.45f));
            }
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", Texture);
                m.SetColor("_EmissionColor", Color.white * 2.2f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>Material slot indices used consistently by characters and vehicles.</summary>
        public const int SlotMatte = 0;
        public const int SlotGlossy = 1;
        public const int SlotMetal = 2;
        public const int SlotGlass = 3;
        public const int SlotEmissive = 4;

        public static Material[] StandardSet => new[] { Matte, Glossy, Metal, Transparent, Emissive };

        public static void ResetMaterials()
        {
            _matte = _glossy = _metal = _transparent = _emissive = null;
        }
    }
}
