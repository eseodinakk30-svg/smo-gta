using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Utils;

namespace SanMonica.Atmosphere
{
    public enum WeatherKind { Clear, FewClouds, Overcast, Rain, Storm, Fog, Heatwave }

    /// <summary>
    /// Weather in San Monica: sun, cloud, rain, thunderstorms, fog and desert
    /// heat, each with its own lighting, visibility, road grip, sound bed and
    /// particle effects. States drift naturally and are influenced by district.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        [Header("State")]
        public WeatherKind Current = WeatherKind.Clear;
        public WeatherKind Next = WeatherKind.FewClouds;
        public float Blend;
        public float MinStateSeconds = 180f;
        public float MaxStateSeconds = 620f;
        public float TransitionSeconds = 45f;

        [Header("Derived")]
        public float Overcast;              // 0..1 cloud cover
        public float RainIntensity;         // 0..1
        public float Wetness;               // 0..1 surface wetness, lags rain
        public float WindStrength = 0.25f;
        public float VisibilityScale = 1f;  // multiplies AI view distance and fog
        public float RoadGripMultiplier = 1f;

        public bool ParticlesEnabled = true;
        public string CurrentName => Current.ToString();

        private float _stateTimer;
        private ParticleSystem _rain;
        private ParticleSystem _splash;
        private AudioSource _ambience;
        private AudioSource _thunder;
        private float _lightningTimer;
        private Rng _rng;
        private Transform _weatherRoot;

        public void Initialize()
        {
            _rng = new Rng((Services.Config != null ? Services.Config.seed : 3) ^ 0xC10D);
            _stateTimer = _rng.Range(MinStateSeconds, MaxStateSeconds);
            _weatherRoot = new GameObject("Weather").transform;
            _weatherRoot.SetParent(transform, false);
            BuildRain();
            BuildAudio();
            ApplyImmediate(Current);
        }

        private void BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(_weatherRoot, false);
            _rain = go.AddComponent<ParticleSystem>();

            var main = _rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.1f;
            main.startSpeed = 22f;
            main.startSize = 0.045f;
            main.startColor = new Color(0.72f, 0.80f, 0.90f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1400;
            main.gravityModifier = 1.1f;

            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(46f, 1f, 46f);

            var emission = _rain.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 3.4f;
            renderer.material = MaterialLibrary.Particle(new Color(0.75f, 0.83f, 0.95f, 0.5f), false);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rain.Play();

            var splashGo = new GameObject("RainSplash");
            splashGo.transform.SetParent(_weatherRoot, false);
            _splash = splashGo.AddComponent<ParticleSystem>();
            var smain = _splash.main;
            smain.loop = true;
            smain.playOnAwake = false;
            smain.startLifetime = 0.28f;
            smain.startSpeed = 0.7f;
            smain.startSize = 0.10f;
            smain.startColor = new Color(0.85f, 0.9f, 0.95f, 0.35f);
            smain.simulationSpace = ParticleSystemSimulationSpace.World;
            smain.maxParticles = 400;
            var sshape = _splash.shape;
            sshape.shapeType = ParticleSystemShapeType.Box;
            sshape.scale = new Vector3(34f, 0.2f, 34f);
            var semission = _splash.emission;
            semission.rateOverTime = 0f;
            var srenderer = splashGo.GetComponent<ParticleSystemRenderer>();
            srenderer.material = MaterialLibrary.Particle(new Color(0.9f, 0.95f, 1f, 0.4f), false);
            srenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _splash.Play();
        }

        private void BuildAudio()
        {
            var go = new GameObject("WeatherAudio");
            go.transform.SetParent(_weatherRoot, false);
            _ambience = go.AddComponent<AudioSource>();
            _ambience.loop = true;
            _ambience.spatialBlend = 0f;
            _ambience.volume = 0f;
            _ambience.playOnAwake = false;
            Services.Audio?.Register(_ambience, SanMonica.Audio.AudioBus.Ambience);

            var thunderGo = new GameObject("Thunder");
            thunderGo.transform.SetParent(_weatherRoot, false);
            _thunder = thunderGo.AddComponent<AudioSource>();
            _thunder.loop = false;
            _thunder.spatialBlend = 0f;
            _thunder.playOnAwake = false;
            Services.Audio?.Register(_thunder, SanMonica.Audio.AudioBus.Ambience);
        }

        // ------------------------------------------------------------------
        public void SetWeather(WeatherKind kind, bool immediate = false)
        {
            Next = kind;
            if (immediate) ApplyImmediate(kind);
            else Blend = 0f;
        }

        private void ApplyImmediate(WeatherKind kind)
        {
            Current = kind;
            Next = kind;
            Blend = 1f;
            var target = Targets(kind);
            Overcast = target.overcast;
            RainIntensity = target.rain;
            WindStrength = target.wind;
            VisibilityScale = target.visibility;
            Wetness = target.rain > 0.1f ? 1f : 0f;
            GameEvents.RaiseWeatherChanged(kind.ToString());
        }

        private struct WeatherTargets
        {
            public float overcast, rain, wind, visibility;
        }

        private static WeatherTargets Targets(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Clear: return new WeatherTargets { overcast = 0.04f, rain = 0f, wind = 0.18f, visibility = 1.15f };
                case WeatherKind.FewClouds: return new WeatherTargets { overcast = 0.32f, rain = 0f, wind = 0.28f, visibility = 1f };
                case WeatherKind.Overcast: return new WeatherTargets { overcast = 0.82f, rain = 0f, wind = 0.42f, visibility = 0.85f };
                case WeatherKind.Rain: return new WeatherTargets { overcast = 0.90f, rain = 0.55f, wind = 0.55f, visibility = 0.62f };
                case WeatherKind.Storm: return new WeatherTargets { overcast = 1f, rain = 1f, wind = 0.95f, visibility = 0.42f };
                case WeatherKind.Fog: return new WeatherTargets { overcast = 0.62f, rain = 0f, wind = 0.10f, visibility = 0.28f };
                case WeatherKind.Heatwave: return new WeatherTargets { overcast = 0.02f, rain = 0f, wind = 0.12f, visibility = 0.92f };
                default: return new WeatherTargets { overcast = 0.2f, rain = 0f, wind = 0.2f, visibility = 1f };
            }
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            float dt = Time.deltaTime;

            _stateTimer -= dt;
            if (_stateTimer <= 0f && Blend >= 1f)
            {
                _stateTimer = _rng.Range(MinStateSeconds, MaxStateSeconds);
                Next = PickNext();
                Blend = 0f;
                GameEvents.RaiseWeatherChanged(Next.ToString());
                GameEvents.Notify("Weather: " + Describe(Next), 3f);
            }

            if (Blend < 1f)
            {
                Blend = Mathf.Clamp01(Blend + dt / Mathf.Max(1f, TransitionSeconds));
                var from = Targets(Current);
                var to = Targets(Next);
                Overcast = Mathf.Lerp(from.overcast, to.overcast, Blend);
                RainIntensity = Mathf.Lerp(from.rain, to.rain, Blend);
                WindStrength = Mathf.Lerp(from.wind, to.wind, Blend);
                VisibilityScale = Mathf.Lerp(from.visibility, to.visibility, Blend);
                if (Blend >= 1f) Current = Next;
            }

            Wetness = Mathf.MoveTowards(Wetness, RainIntensity > 0.08f ? 1f : 0f, dt * (RainIntensity > 0.08f ? 0.25f : 0.05f));
            RoadGripMultiplier = Mathf.Lerp(1f, 0.78f, Wetness);

            UpdateParticles();
            UpdateAudio(dt);
            UpdateLightning(dt);
        }

        private WeatherKind PickNext()
        {
            var district = Services.Map != null ? Services.Map.DistrictAt(Services.PlayerPosition) : SanMonica.Data.DistrictType.Downtown;
            bool desert = district == SanMonica.Data.DistrictType.Badlands;
            bool coast = district == SanMonica.Data.DistrictType.Beach || district == SanMonica.Data.DistrictType.Marina
                      || district == SanMonica.Data.DistrictType.Port || district == SanMonica.Data.DistrictType.Ocean;

            float roll = _rng.Value;
            if (desert) return roll < 0.55f ? WeatherKind.Clear : (roll < 0.85f ? WeatherKind.Heatwave : WeatherKind.FewClouds);
            if (coast && roll < 0.18f) return WeatherKind.Fog;

            switch (Current)
            {
                case WeatherKind.Clear: return roll < 0.55f ? WeatherKind.FewClouds : (roll < 0.85f ? WeatherKind.Clear : WeatherKind.Overcast);
                case WeatherKind.FewClouds: return roll < 0.35f ? WeatherKind.Clear : (roll < 0.75f ? WeatherKind.Overcast : WeatherKind.Rain);
                case WeatherKind.Overcast: return roll < 0.35f ? WeatherKind.Rain : (roll < 0.6f ? WeatherKind.FewClouds : (roll < 0.75f ? WeatherKind.Fog : WeatherKind.Overcast));
                case WeatherKind.Rain: return roll < 0.30f ? WeatherKind.Storm : (roll < 0.75f ? WeatherKind.Overcast : WeatherKind.Rain);
                case WeatherKind.Storm: return roll < 0.7f ? WeatherKind.Rain : WeatherKind.Overcast;
                case WeatherKind.Fog: return roll < 0.6f ? WeatherKind.Overcast : WeatherKind.FewClouds;
                default: return WeatherKind.Clear;
            }
        }

        private static string Describe(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Clear: return "clear skies";
                case WeatherKind.FewClouds: return "scattered cloud";
                case WeatherKind.Overcast: return "overcast";
                case WeatherKind.Rain: return "rain moving in";
                case WeatherKind.Storm: return "thunderstorm warning";
                case WeatherKind.Fog: return "heavy fog";
                case WeatherKind.Heatwave: return "heat advisory";
                default: return kind.ToString();
            }
        }

        private void UpdateParticles()
        {
            if (_rain == null) return;
            var cam = Services.Camera;
            Vector3 follow = cam != null && cam.Cam != null ? cam.Cam.transform.position : Services.PlayerPosition;

            bool indoors = Services.Interiors != null && Services.Interiors.IsInside;
            float amount = ParticlesEnabled && !indoors ? RainIntensity : 0f;

            _rain.transform.position = follow + Vector3.up * 16f;
            var emission = _rain.emission;
            emission.rateOverTime = amount * 1100f;

            _splash.transform.position = follow;
            var splashEmission = _splash.emission;
            splashEmission.rateOverTime = amount * 260f;

            var main = _rain.main;
            main.startSpeed = 18f + WindStrength * 14f;
        }

        private void UpdateAudio(float dt)
        {
            if (_ambience == null || Services.Audio == null) return;
            var clip = RainIntensity > 0.08f ? Services.Audio.GetClip("rain") : Services.Audio.GetClip("wind");
            if (_ambience.clip != clip)
            {
                _ambience.clip = clip;
                if (clip != null) _ambience.Play();
            }
            float target = RainIntensity > 0.08f ? Mathf.Lerp(0.15f, 0.55f, RainIntensity) : Mathf.Lerp(0.04f, 0.22f, WindStrength);
            if (Services.Interiors != null && Services.Interiors.IsInside) target *= 0.25f;
            _ambience.volume = Mathf.MoveTowards(_ambience.volume, target, dt * 0.35f);
        }

        private void UpdateLightning(float dt)
        {
            if (Current != WeatherKind.Storm && Next != WeatherKind.Storm) return;
            _lightningTimer -= dt;
            if (_lightningTimer > 0f) return;
            _lightningTimer = _rng.Range(5f, 18f);

            StartCoroutine(LightningFlash());
            if (_thunder != null && Services.Audio != null)
            {
                var clip = Services.Audio.GetClip("thunder");
                if (clip != null) _thunder.PlayOneShot(clip, 0.8f);
            }
        }

        private System.Collections.IEnumerator LightningFlash()
        {
            var sky = Services.Sky;
            if (sky == null || sky.Sun == null) yield break;
            float original = sky.Sun.intensity;
            Color originalColour = sky.Sun.color;
            for (int i = 0; i < 2; i++)
            {
                sky.Sun.enabled = true;
                sky.Sun.intensity = 2.6f;
                sky.Sun.color = new Color(0.85f, 0.9f, 1f);
                yield return new WaitForSeconds(0.06f);
                sky.Sun.intensity = original;
                sky.Sun.color = originalColour;
                yield return new WaitForSeconds(0.08f);
            }
        }

        public WeatherSaveState CaptureState() => new WeatherSaveState { current = (int)Current, next = (int)Next, blend = Blend, timer = _stateTimer };

        public void RestoreState(WeatherSaveState state)
        {
            if (state == null) return;
            Current = (WeatherKind)state.current;
            Next = (WeatherKind)state.next;
            Blend = state.blend;
            _stateTimer = state.timer;
            ApplyImmediate(Current);
        }
    }

    [System.Serializable]
    public class WeatherSaveState
    {
        public int current;
        public int next;
        public float blend;
        public float timer;
    }
}
