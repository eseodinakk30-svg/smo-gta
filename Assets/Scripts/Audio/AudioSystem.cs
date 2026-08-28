using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;

namespace SanMonica.Audio
{
    public enum AudioBus { Master, Music, Sfx, Ambience, Voice, Ui }

    /// <summary>
    /// Central mixer and clip library. Buses carry their own volume so the
    /// options screen can balance music, effects, ambience and interface
    /// independently, and every clip is generated on first use and cached.
    /// </summary>
    public class AudioSystem : MonoBehaviour
    {
        [Header("Bus volumes")]
        [Range(0f, 1f)] public float MasterVolume = 1f;
        [Range(0f, 1f)] public float MusicVolume = 0.65f;
        [Range(0f, 1f)] public float SfxVolume = 0.9f;
        [Range(0f, 1f)] public float AmbienceVolume = 0.6f;
        [Range(0f, 1f)] public float VoiceVolume = 1f;
        [Range(0f, 1f)] public float UiVolume = 0.8f;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(48);
        private readonly Dictionary<string, AudioClip> _engineClips = new Dictionary<string, AudioClip>(16);
        private readonly Dictionary<string, AudioClip> _shotClips = new Dictionary<string, AudioClip>(24);
        private readonly Dictionary<AudioSource, AudioBus> _sources = new Dictionary<AudioSource, AudioBus>(64);
        private readonly List<AudioSource> _oneShotPool = new List<AudioSource>(16);

        private AudioSource _uiSource;
        private AudioSource _ambienceSource;
        private Transform _root;
        private int _oneShotCursor;
        private float _ambienceTimer;

        public void Initialize()
        {
            _root = new GameObject("Audio").transform;
            _root.SetParent(transform, false);

            _uiSource = CreateSource("UI", AudioBus.Ui, false);
            _uiSource.spatialBlend = 0f;

            _ambienceSource = CreateSource("Ambience", AudioBus.Ambience, true);
            _ambienceSource.spatialBlend = 0f;
            _ambienceSource.volume = 0.4f;

            for (int i = 0; i < 12; i++)
            {
                var src = CreateSource("OneShot" + i, AudioBus.Sfx, false);
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 4f;
                src.maxDistance = 90f;
                _oneShotPool.Add(src);
            }

            ApplyVolumes();
        }

        private AudioSource CreateSource(string name, AudioBus bus, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            _sources[src] = bus;
            return src;
        }

        public void Register(AudioSource source, AudioBus bus)
        {
            if (source == null) return;
            _sources[source] = bus;
        }

        public float BusVolume(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return MasterVolume * MusicVolume;
                case AudioBus.Sfx: return MasterVolume * SfxVolume;
                case AudioBus.Ambience: return MasterVolume * AmbienceVolume;
                case AudioBus.Voice: return MasterVolume * VoiceVolume;
                case AudioBus.Ui: return MasterVolume * UiVolume;
                default: return MasterVolume;
            }
        }

        public void ApplyVolumes()
        {
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
        }

        // ------------------------------------------------------------------
        public AudioClip GetClip(string key)
        {
            if (_clips.TryGetValue(key, out var clip) && clip != null) return clip;
            clip = Generate(key);
            if (clip != null) _clips[key] = clip;
            return clip;
        }

        private AudioClip Generate(string key)
        {
            int seed = key.GetHashCode();
            switch (key)
            {
                case "rain": return ProceduralAudio.Rain(seed);
                case "wind": return ProceduralAudio.Wind(seed);
                case "thunder": return ProceduralAudio.Thunder(seed);
                case "siren": return ProceduralAudio.Siren(seed);
                case "horn": return ProceduralAudio.Horn(320f);
                case "crash": return ProceduralAudio.Impact(0.85f, seed);
                case "tyre_skid": return ProceduralAudio.Skid(seed);
                case "splash": return ProceduralAudio.Splash(seed);
                case "explosion": return ProceduralAudio.Explosion(seed);
                case "scream": return ProceduralAudio.Scream(seed);
                case "land": return ProceduralAudio.Footstep(seed, true);
                case "jump": return ProceduralAudio.UiTone(220f, 0.14f, 1.5f, false);
                case "vault": return ProceduralAudio.Footstep(seed, true);
                case "melee_swing": return ProceduralAudio.UiTone(180f, 0.18f, -2.2f, false);
                case "melee_hit": return ProceduralAudio.Impact(0.35f, seed);
                case "weapon_reload": return ProceduralAudio.Impact(0.2f, seed);
                case "weapon_switch": return ProceduralAudio.UiTone(520f, 0.10f, -1f, true);
                case "weapon_empty": return ProceduralAudio.UiTone(880f, 0.07f, -3f, true);
                case "city_light": return ProceduralAudio.CityAmbience(seed, 0.35f);
                case "city_busy": return ProceduralAudio.CityAmbience(seed, 1f);
                case "city_quiet": return ProceduralAudio.CityAmbience(seed, 0.12f);
                case "purchase": return ProceduralAudio.UiTone(660f, 0.22f, 0.6f, false);
                case "error": return ProceduralAudio.UiTone(180f, 0.24f, -0.8f, true);
                case "pickup": return ProceduralAudio.UiTone(880f, 0.20f, 1.2f, false);
                case "wanted_up": return ProceduralAudio.UiTone(300f, 0.55f, 0.9f, true);
                case "wanted_clear": return ProceduralAudio.UiTone(520f, 0.6f, -0.7f, false);
                case "dialogue": return ProceduralAudio.UiTone(440f, 0.06f, 0f, false);
                case "click": return ProceduralAudio.UiTone(900f, 0.05f, -2f, true);
                case "footstep_soft": return ProceduralAudio.Footstep(seed, false);
                case "footstep_hard": return ProceduralAudio.Footstep(seed, true);
                default: return ProceduralAudio.UiTone(440f, 0.12f, 0f, false);
            }
        }

        public AudioClip GetEngineClip(VehicleDefinition definition)
        {
            if (definition == null) return null;
            if (_engineClips.TryGetValue(definition.id, out var clip) && clip != null) return clip;
            int cylinders = definition.IsBike ? 2 : (definition.mass > 5000f ? 6 : 4);
            clip = ProceduralAudio.EngineLoop(definition.engineBaseHz, definition.engineHarshness, cylinders, definition.id.GetHashCode());
            _engineClips[definition.id] = clip;
            return clip;
        }

        public AudioClip GetShotClip(WeaponDefinition definition)
        {
            if (definition == null) return null;
            if (_shotClips.TryGetValue(definition.id, out var clip) && clip != null) return clip;
            clip = ProceduralAudio.Gunshot(definition.shotPitch, definition.shotBody, definition.shotTail, definition.id.GetHashCode());
            _shotClips[definition.id] = clip;
            return clip;
        }

        // ------------------------------------------------------------------
        public void PlayOneShot(string key, Vector3 position, float volume = 1f)
        {
            var clip = GetClip(key);
            if (clip == null || _oneShotPool.Count == 0) return;
            var src = _oneShotPool[_oneShotCursor];
            _oneShotCursor = (_oneShotCursor + 1) % _oneShotPool.Count;
            src.transform.position = position;
            src.pitch = Random.Range(0.93f, 1.07f);
            src.PlayOneShot(clip, Mathf.Clamp01(volume) * BusVolume(AudioBus.Sfx));
        }

        public void PlayFootstep(Vector3 position, bool running)
        {
            PlayOneShot(running ? "footstep_hard" : "footstep_soft", position, running ? 0.35f : 0.22f);
        }

        public void PlayUi(string key)
        {
            var clip = GetClip(key);
            if (clip == null || _uiSource == null) return;
            _uiSource.PlayOneShot(clip, BusVolume(AudioBus.Ui));
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            _ambienceTimer -= Time.unscaledDeltaTime;
            if (_ambienceTimer > 0f) return;
            _ambienceTimer = 2.5f;
            UpdateAmbience();
        }

        private void UpdateAmbience()
        {
            if (_ambienceSource == null || Services.Map == null) return;

            var profile = Services.Map.ProfileAt(Services.PlayerPosition);
            bool night = Services.Clock != null && Services.Clock.IsNight;
            float density = Mathf.Clamp01(profile.pedDensity * 0.45f + profile.trafficDensity * 0.3f) * (night ? 0.55f : 1f);

            string key = density > 0.75f ? "city_busy" : (density > 0.30f ? "city_light" : "city_quiet");
            var clip = GetClip(key);
            if (_ambienceSource.clip != clip)
            {
                _ambienceSource.clip = clip;
                if (clip != null) _ambienceSource.Play();
            }
            float target = Mathf.Lerp(0.10f, 0.42f, density) * BusVolume(AudioBus.Ambience);
            if (Services.Interiors != null && Services.Interiors.IsInside) target *= 0.35f;
            _ambienceSource.volume = Mathf.MoveTowards(_ambienceSource.volume, target, Time.unscaledDeltaTime * 0.3f);
        }

        public void SetPaused(bool paused)
        {
            foreach (var kv in _sources)
            {
                if (kv.Key == null) continue;
                if (kv.Value == AudioBus.Ui) continue;
                if (paused) kv.Key.Pause(); else kv.Key.UnPause();
            }
        }
    }
}
