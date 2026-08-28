using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SanMonica.Core;
using SanMonica.Utils;

namespace SanMonica.Optimization
{
    public enum QualityPreset { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    /// <summary>
    /// Four hand-tuned quality tiers plus an adaptive mode. Every setting that
    /// matters on Android is here: render scale, shadows, draw distance,
    /// streaming rings, crowd and traffic density, particles, textures and the
    /// frame rate target. Content is never removed - only detail is scaled.
    /// </summary>
    public class QualityManager : MonoBehaviour
    {
        public QualityPreset Preset { get; private set; } = QualityPreset.High;
        public bool AutoQuality = true;
        public float RenderScale { get; private set; } = 1f;
        public float DrawDistanceScale { get; private set; } = 1f;
        public float PedDensity { get; private set; } = 1f;
        public float TrafficDensity { get; private set; } = 1f;
        public int TargetFrameRate { get; private set; } = 60;

        [Header("Auto quality")]
        public float AdaptInterval = 4f;
        public float LowerThreshold = 0.82f;    // fraction of target fps
        public float RaiseThreshold = 1.08f;
        public int MinAutoTier = 0;
        public int MaxAutoTier = 3;

        private float _adaptTimer;
        private int _stableFrames;
        private UniversalRenderPipelineAsset _pipelineAsset;

        public int DeviceTier { get; private set; } = 2;

        // ------------------------------------------------------------------
        public void Initialize()
        {
            _pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            DeviceTier = ProfileDevice();
            ApplyPreset((QualityPreset)Mathf.Clamp(DeviceTier, 0, 3));
        }

        /// <summary>Picks a starting tier from memory, CPU, GPU and screen size.</summary>
        private int ProfileDevice()
        {
            int score = 0;
            int memory = SystemInfo.systemMemorySize;
            int vram = SystemInfo.graphicsMemorySize;
            int cores = SystemInfo.processorCount;
            int shader = SystemInfo.graphicsShaderLevel;

            if (memory >= 3500) score++;
            if (memory >= 6500) score++;
            if (memory >= 11000) score++;
            if (vram >= 1500) score++;
            if (cores >= 6) score++;
            if (cores >= 8) score++;
            if (shader >= 45) score++;
            if (Screen.width * Screen.height > 2400 * 1080) score--;

#if UNITY_EDITOR
            score = Mathf.Max(score, 5);
#endif
            if (score <= 1) return 0;
            if (score <= 3) return 1;
            if (score <= 5) return 2;
            return 3;
        }

        // ------------------------------------------------------------------
        public void ApplyPreset(QualityPreset preset)
        {
            Preset = preset;
            switch (preset)
            {
                case QualityPreset.Low:
                    RenderScale = 0.62f; DrawDistanceScale = 0.55f; PedDensity = 0.40f; TrafficDensity = 0.45f;
                    TargetFrameRate = 30;
                    ConfigureRendering(shadowDistance: 45f, shadowCascades: 1, msaa: 1, hdr: false, softShadows: false, shadows: true);
                    ConfigureWorld(high: 1, medium: 2, low: 3, lights: 6, budgetMs: 3.5f);
                    ConfigureDetail(textureRes: 128, simpleLit: true, normalMaps: false, particles: true, effectScale: 0.7f, decals: 20);
                    ConfigureAi(full: 32f, reduced: 90f, perFrame: 8);
                    break;

                case QualityPreset.Medium:
                    RenderScale = 0.80f; DrawDistanceScale = 0.80f; PedDensity = 0.70f; TrafficDensity = 0.75f;
                    TargetFrameRate = 45;
                    ConfigureRendering(shadowDistance: 70f, shadowCascades: 2, msaa: 1, hdr: false, softShadows: false, shadows: true);
                    ConfigureWorld(high: 1, medium: 3, low: 4, lights: 12, budgetMs: 4f);
                    ConfigureDetail(textureRes: 192, simpleLit: false, normalMaps: true, particles: true, effectScale: 0.85f, decals: 40);
                    ConfigureAi(full: 45f, reduced: 130f, perFrame: 12);
                    break;

                case QualityPreset.High:
                    RenderScale = 1f; DrawDistanceScale = 1f; PedDensity = 1f; TrafficDensity = 1f;
                    TargetFrameRate = 60;
                    ConfigureRendering(shadowDistance: 110f, shadowCascades: 3, msaa: 2, hdr: true, softShadows: true, shadows: true);
                    ConfigureWorld(high: 2, medium: 4, low: 6, lights: 20, budgetMs: 5f);
                    ConfigureDetail(textureRes: 256, simpleLit: false, normalMaps: true, particles: true, effectScale: 1f, decals: 60);
                    ConfigureAi(full: 55f, reduced: 165f, perFrame: 16);
                    break;

                case QualityPreset.Ultra:
                    RenderScale = 1.1f; DrawDistanceScale = 1.45f; PedDensity = 1.45f; TrafficDensity = 1.4f;
                    TargetFrameRate = 60;
                    ConfigureRendering(shadowDistance: 170f, shadowCascades: 4, msaa: 4, hdr: true, softShadows: true, shadows: true);
                    ConfigureWorld(high: 3, medium: 5, low: 8, lights: 32, budgetMs: 6f);
                    ConfigureDetail(textureRes: 384, simpleLit: false, normalMaps: true, particles: true, effectScale: 1.25f, decals: 110);
                    ConfigureAi(full: 70f, reduced: 210f, perFrame: 22);
                    break;
            }
            ApplyFrameRate();
        }

        private void ConfigureRendering(float shadowDistance, int shadowCascades, int msaa, bool hdr, bool softShadows, bool shadows)
        {
            if (_pipelineAsset == null) _pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_pipelineAsset != null)
            {
                _pipelineAsset.renderScale = Mathf.Clamp(RenderScale, 0.4f, 2f);
                _pipelineAsset.shadowDistance = shadowDistance * DrawDistanceScale;
                _pipelineAsset.shadowCascadeCount = Mathf.Clamp(shadowCascades, 1, 4);
                _pipelineAsset.msaaSampleCount = Mathf.Max(1, msaa);
                _pipelineAsset.supportsHDR = hdr;
            }
            QualitySettings.shadows = shadows ? (softShadows ? ShadowQuality.All : ShadowQuality.HardOnly) : ShadowQuality.Disable;
            QualitySettings.shadowResolution = softShadows ? ShadowResolution.Medium : ShadowResolution.Low;
            QualitySettings.skinWeights = SkinWeights.OneBone;
            QualitySettings.softParticles = false;
            QualitySettings.billboardsFaceCameraPosition = true;
            QualitySettings.lodBias = Mathf.Lerp(0.6f, 1.6f, DrawDistanceScale / 1.5f);
            QualitySettings.maximumLODLevel = 0;
        }

        private void ConfigureWorld(int high, int medium, int low, int lights, float budgetMs)
        {
            var streamer = Services.Streamer;
            if (streamer != null)
            {
                int h = Mathf.Max(1, Mathf.RoundToInt(high * DrawDistanceScale));
                int m = Mathf.Max(h, Mathf.RoundToInt(medium * DrawDistanceScale));
                int l = Mathf.Max(m, Mathf.RoundToInt(low * DrawDistanceScale));
                streamer.ApplyQuality(h, m, l, lights, budgetMs);
            }
            var camera = Services.Camera;
            if (camera != null && camera.Cam != null)
                camera.Cam.farClipPlane = Mathf.Lerp(700f, 2200f, Mathf.Clamp01(DrawDistanceScale / 1.5f));
        }

        private void ConfigureDetail(int textureRes, bool simpleLit, bool normalMaps, bool particles, float effectScale, int decals)
        {
            bool rebuildMaterials = MaterialLibrary.UseSimpleLit != simpleLit || MaterialLibrary.NormalMapsEnabled != normalMaps;
            MaterialLibrary.UseSimpleLit = simpleLit;
            MaterialLibrary.NormalMapsEnabled = normalMaps;
            if (TextureFactory.BaseResolution != textureRes)
            {
                TextureFactory.SetResolution(textureRes);
                rebuildMaterials = true;
            }
            if (rebuildMaterials)
            {
                // Existing chunks keep their materials until they are rebuilt,
                // which happens naturally as the player moves.
                PaletteAtlas.ResetMaterials();
            }

            var effects = Services.Effects;
            if (effects != null)
            {
                effects.ParticlesEnabled = particles;
                effects.EffectScale = effectScale;
                effects.MaxDecals = decals;
            }

            var water = Services.Water;
            if (water != null) water.WavesEnabled = Preset != QualityPreset.Low;

            var weather = Services.Weather;
            if (weather != null) weather.ParticlesEnabled = particles;

            var ui = Services.UI;
            if (ui != null && ui.Hud != null && ui.Hud.MinimapView != null)
                ui.Hud.MinimapView.SetQuality(Preset == QualityPreset.Low ? 128 : (Preset == QualityPreset.Ultra ? 256 : 192),
                    Preset == QualityPreset.Low ? 4 : (Preset == QualityPreset.Ultra ? 1 : 2),
                    Preset == QualityPreset.Ultra ? 120f : 95f);
        }

        private void ConfigureAi(float full, float reduced, int perFrame)
        {
            Services.AiLod?.ApplyQuality(full, reduced, perFrame);
            var population = Services.Population;
            if (population != null) population.DensityScale = PedDensity;
            var traffic = Services.Traffic;
            if (traffic != null) traffic.DensityScale = TrafficDensity;
        }

        private void ApplyFrameRate()
        {
            Application.targetFrameRate = TargetFrameRate;
            QualitySettings.vSyncCount = 0;
        }

        // ------------------------------------------------------------------
        public void SetRenderScale(float value)
        {
            RenderScale = Mathf.Clamp(value, 0.4f, 1.5f);
            if (_pipelineAsset != null) _pipelineAsset.renderScale = RenderScale;
        }

        public void SetDrawDistance(float value)
        {
            DrawDistanceScale = Mathf.Clamp(value, 0.4f, 2.2f);
            ApplyPreset(Preset);
        }

        public void SetPedDensity(float value)
        {
            PedDensity = Mathf.Clamp(value, 0.05f, 2.5f);
            var population = Services.Population;
            if (population != null) population.DensityScale = PedDensity;
        }

        public void SetTrafficDensity(float value)
        {
            TrafficDensity = Mathf.Clamp(value, 0.05f, 2.5f);
            var traffic = Services.Traffic;
            if (traffic != null) traffic.DensityScale = TrafficDensity;
        }

        public void SetTargetFrameRate(int value)
        {
            TargetFrameRate = Mathf.Clamp(value, 24, 144);
            ApplyFrameRate();
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            if (!AutoQuality) return;
            var perf = Services.Perf;
            if (perf == null) return;

            _adaptTimer -= Time.unscaledDeltaTime;
            if (_adaptTimer > 0f) return;
            _adaptTimer = AdaptInterval;

            float ratio = perf.SmoothedFps / Mathf.Max(1f, TargetFrameRate);
            int tier = (int)Preset;

            if (ratio < LowerThreshold && tier > MinAutoTier)
            {
                _stableFrames = 0;
                ApplyPreset((QualityPreset)(tier - 1));
                GameEvents.Notify("Graphics lowered to " + Preset + " to hold " + TargetFrameRate + " fps", 3f);
            }
            else if (ratio > RaiseThreshold && tier < MaxAutoTier)
            {
                _stableFrames++;
                if (_stableFrames >= 4)
                {
                    _stableFrames = 0;
                    ApplyPreset((QualityPreset)(tier + 1));
                    GameEvents.Notify("Graphics raised to " + Preset, 3f);
                }
            }
            else _stableFrames = 0;
        }
    }
}
