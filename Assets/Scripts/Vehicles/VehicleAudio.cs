using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Vehicles
{
    /// <summary>
    /// Engine, tyre, horn and siren voices for one vehicle. All clips come from
    /// the procedural audio library, so every engine note is synthesised from
    /// the vehicle's own data rather than sampled.
    /// </summary>
    public class VehicleAudio : MonoBehaviour
    {
        private Vehicle _vehicle;
        private AudioSource _engine;
        private AudioSource _tyres;
        private AudioSource _oneShot;
        private AudioSource _siren;
        private float _pitchSmoothed = 1f;

        public void Bind(Vehicle vehicle)
        {
            _vehicle = vehicle;
            var audio = Services.Audio;
            if (audio == null) return;

            _engine = CreateSource("Engine", true, 0.55f, 34f);
            _tyres = CreateSource("Tyres", true, 0f, 22f);
            _oneShot = CreateSource("VehicleSfx", false, 0.9f, 60f);
            _siren = CreateSource("Siren", true, 0f, 140f);

            _engine.clip = audio.GetEngineClip(vehicle.Definition);
            _tyres.clip = audio.GetClip("tyre_skid");
            _siren.clip = audio.GetClip("siren");
            if (_engine.clip != null) _engine.Play();
            if (_tyres.clip != null) _tyres.Play();
        }

        private AudioSource CreateSource(string name, bool loop, float volume, float maxDistance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = loop;
            src.volume = volume;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 3.5f;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0.6f;
            Services.Audio?.Register(src, SanMonica.Audio.AudioBus.Sfx);
            return src;
        }

        private void Update()
        {
            if (_vehicle == null || _engine == null) return;

            bool running = _vehicle.EngineRunning && !_vehicle.IsDestroyed;
            float rpm = _vehicle.EngineRpmNormalised;
            float targetPitch = running ? Mathf.Lerp(0.62f, 2.35f, rpm) : 0f;
            _pitchSmoothed = Mathf.Lerp(_pitchSmoothed, targetPitch, Time.deltaTime * 7f);
            _engine.pitch = Mathf.Max(0.05f, _pitchSmoothed);

            float distanceFade = 1f;
            float volume = running ? Mathf.Lerp(0.18f, 0.62f, rpm) * distanceFade : 0f;

            // Turbocharged cars whine on top of the engine as the revs come up.
            // turboWhistle had been catalogue data nothing ever read.
            float turbo = _vehicle.Definition != null ? _vehicle.Definition.turboWhistle : 0f;
            if (running && turbo > 0.01f)
            {
                float spool = Mathf.Clamp01((rpm - 0.42f) / 0.5f) * Mathf.Clamp01(_vehicle.Throttle + 0.15f);
                _engine.pitch += turbo * spool * 0.55f;
                volume *= 1f + turbo * spool * 0.35f;
            }

            _engine.volume = Mathf.Lerp(_engine.volume, volume, Time.deltaTime * 6f);

            if (_tyres != null)
            {
                float slip = _vehicle.Motor != null ? _vehicle.Motor.SlipAmount : 0f;
                float skid = Mathf.Clamp01((slip - 0.25f) * 2.2f) * Mathf.Clamp01(_vehicle.AbsSpeedKph / 25f);
                _tyres.volume = Mathf.Lerp(_tyres.volume, skid * 0.55f, Time.deltaTime * 8f);
                _tyres.pitch = 0.85f + skid * 0.4f;
            }
        }

        public void PlayHorn()
        {
            var clip = Services.Audio?.GetClip("horn");
            if (clip != null && _oneShot != null) _oneShot.PlayOneShot(clip, 0.8f);
        }

        public void PlayImpact(float strength)
        {
            var clip = Services.Audio?.GetClip("crash");
            if (clip != null && _oneShot != null) _oneShot.PlayOneShot(clip, Mathf.Clamp01(0.3f + strength));
        }

        public void SetSiren(bool on)
        {
            if (_siren == null) return;
            if (on)
            {
                if (!_siren.isPlaying && _siren.clip != null) _siren.Play();
                _siren.volume = 0.7f;
            }
            else
            {
                _siren.volume = 0f;
                if (_siren.isPlaying) _siren.Stop();
            }
        }
    }
}
