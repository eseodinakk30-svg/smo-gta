using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Utils
{
    /// <summary>
    /// Central cache of URP materials. Every material is created with GPU
    /// instancing enabled so the SRP batcher and instanced draws can collapse
    /// the thousands of procedurally generated renderers into few draw calls.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit, _simpleLit, _unlit, _particle, _transparent;

        public static bool UseSimpleLit;      // toggled by the quality manager on low-end devices
        public static bool NormalMapsEnabled = true;

        /// <summary>
        /// Resolves a shader through its keeper material in Resources. Those
        /// materials exist only so the shaders survive build-time stripping -
        /// nothing renders with them - and asking through the material is more
        /// reliable in a player than Shader.Find, which only ever finds what the
        /// build already contains.
        /// </summary>
        private static Shader Keeper(string assetName)
        {
            var material = Resources.Load<Material>("Shaders/" + assetName);
            return material != null ? material.shader : null;
        }

        public static Shader LitShader
        {
            get
            {
                if (_lit == null)
                    _lit = Keeper("Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                return _lit;
            }
        }

        public static Shader SimpleLitShader
        {
            get
            {
                if (_simpleLit == null)
                    _simpleLit = Keeper("SimpleLit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? LitShader;
                return _simpleLit;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlit == null)
                    _unlit = Keeper("Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                return _unlit;
            }
        }

        public static Shader ParticleShader
        {
            get
            {
                if (_particle == null)
                    _particle = Keeper("ParticleUnlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? UnlitShader;
                return _particle;
            }
        }

        private static Shader SurfaceShader => UseSimpleLit ? SimpleLitShader : LitShader;

        public static void ClearCache()
        {
            foreach (var kv in Cache) if (kv.Value != null) Object.Destroy(kv.Value);
            Cache.Clear();
        }

        /// <summary>Standard opaque PBR surface backed by a procedural albedo + derived normal map.</summary>
        public static Material Surface(SurfaceKind kind, int variant = 0, Color? tint = null, float smoothness = 0.2f, float metallic = 0f, float tiling = 1f)
        {
            Color t = tint ?? Color.white;
            string key = "S:" + kind + ":" + variant + ":" + t.ToString() + ":" + smoothness.ToString("F2") + ":" + metallic.ToString("F2") + ":" + tiling.ToString("F2") + ":" + UseSimpleLit;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;

            m = new Material(SurfaceShader) { name = "M_" + kind + variant, enableInstancing = true };
            m.SetTexture("_BaseMap", TextureFactory.Get(kind, variant));
            m.SetColor("_BaseColor", t);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (tiling != 1f) m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            if (NormalMapsEnabled && !UseSimpleLit && SupportsNormal(kind))
            {
                m.SetTexture("_BumpMap", TextureFactory.GetNormal(kind, variant, NormalStrength(kind)));
                m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Window / facade material whose albedo alpha channel drives night time emission.</summary>
        public static Material Windows(SurfaceKind kind, int variant, Color tint, float emission)
        {
            string key = "W:" + kind + ":" + variant + ":" + tint + ":" + emission.ToString("F2");
            if (Cache.TryGetValue(key, out var m) && m != null) return m;

            m = new Material(SurfaceShader) { name = "M_Win" + variant, enableInstancing = true };
            var tex = TextureFactory.Get(kind, variant, tint);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.82f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Solid colour PBR material - vehicle paint, character skin/clothing, props.</summary>
        public static Material Solid(Color color, float smoothness = 0.3f, float metallic = 0f, string tag = "")
        {
            string key = "C:" + ColorKey(color) + ":" + smoothness.ToString("F2") + ":" + metallic.ToString("F2") + ":" + tag + ":" + UseSimpleLit;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(SurfaceShader) { name = "M_Solid" + tag, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        public static Material Emissive(Color color, float intensity = 2f)
        {
            string key = "E:" + ColorKey(color) + ":" + intensity.ToString("F2");
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(SurfaceShader) { name = "M_Emissive", enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Cache[key] = m;
            return m;
        }

        public static Material Transparent(Color color, float smoothness = 0.9f)
        {
            string key = "T:" + ColorKey(color) + ":" + smoothness.ToString("F2");
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            if (_transparent == null) _transparent = LitShader;
            m = new Material(_transparent) { name = "M_Glass", enableInstancing = true };
            m.SetColor("_BaseColor", color);
            SetTransparent(m);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.2f);
            Cache[key] = m;
            return m;
        }

        public static Material Unlit(Color color, bool transparent = false)
        {
            string key = "U:" + ColorKey(color) + ":" + transparent;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(UnlitShader) { name = "M_Unlit", enableInstancing = true };
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (transparent) SetTransparent(m);
            Cache[key] = m;
            return m;
        }

        public static Material Particle(Color color, bool additive)
        {
            string key = "P:" + ColorKey(color) + ":" + additive;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(ParticleShader) { name = "M_Particle" };
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.renderQueue = 3000;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 1f : 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", additive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            Cache[key] = m;
            return m;
        }

        /// <summary>Foliage material with alpha clipping and vertex colour tinting.</summary>
        public static Material Foliage(Color tint, int variant = 0)
        {
            string key = "F:" + ColorKey(tint) + ":" + variant;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(SurfaceShader) { name = "M_Foliage", enableInstancing = true };
            m.SetTexture("_BaseMap", TextureFactory.Get(SurfaceKind.Foliage, variant));
            m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            Cache[key] = m;
            return m;
        }

        private static void SetTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static bool SupportsNormal(SurfaceKind k)
        {
            switch (k)
            {
                case SurfaceKind.Brick:
                case SurfaceKind.Asphalt:
                case SurfaceKind.Concrete:
                case SurfaceKind.Sidewalk:
                case SurfaceKind.Rock:
                case SurfaceKind.MetalPanel:
                case SurfaceKind.RustedMetal:
                case SurfaceKind.Container:
                case SurfaceKind.Wood:
                case SurfaceKind.Roof:
                case SurfaceKind.Sand:
                    return true;
                default:
                    return false;
            }
        }

        private static float NormalStrength(SurfaceKind k)
        {
            switch (k)
            {
                case SurfaceKind.Brick: return 1.6f;
                case SurfaceKind.Rock: return 1.8f;
                case SurfaceKind.Container: return 1.4f;
                default: return 1f;
            }
        }

        private static string ColorKey(Color c) =>
            Mathf.RoundToInt(c.r * 255) + "_" + Mathf.RoundToInt(c.g * 255) + "_" + Mathf.RoundToInt(c.b * 255) + "_" + Mathf.RoundToInt(c.a * 255);
    }
}
