using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;

namespace SanMonica.Audio
{
    /// <summary>
    /// Eight original San Monica stations. Each one composes its own tracks,
    /// then breaks for the host, the news and adverts, all written for this game
    /// and delivered as on-screen station text over a synthesised sting.
    /// </summary>
    public class RadioSystem : MonoBehaviour
    {
        private class Station
        {
            public RadioStationDefinition Definition;
            public readonly List<AudioClip> Tracks = new List<AudioClip>(3);
            public int NextTrack;
            public int LineCursor;
        }

        public int CurrentIndex { get; private set; } = -1;
        public bool IsOn { get; private set; }
        public string NowPlaying { get; private set; } = "";
        public string StationName => CurrentIndex >= 0 && CurrentIndex < _stations.Count ? _stations[CurrentIndex].Definition.displayName : "Radio Off";
        public Color StationColour => CurrentIndex >= 0 && CurrentIndex < _stations.Count ? _stations[CurrentIndex].Definition.accent : Color.white;
        public int StationCount => _stations.Count;

        private readonly List<Station> _stations = new List<Station>(8);
        private AudioSource _source;
        private AudioSource _sting;
        private Coroutine _scheduler;
        private bool _generating;

        public void Initialize(GameDatabase db)
        {
            foreach (var definition in db.radioStations)
                _stations.Add(new Station { Definition = definition });

            var go = new GameObject("Radio");
            go.transform.SetParent(transform, false);
            _source = go.AddComponent<AudioSource>();
            _source.loop = false;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            Services.Audio?.Register(_source, AudioBus.Music);

            var stingGo = new GameObject("RadioSting");
            stingGo.transform.SetParent(go.transform, false);
            _sting = stingGo.AddComponent<AudioSource>();
            _sting.playOnAwake = false;
            _sting.spatialBlend = 0f;
            Services.Audio?.Register(_sting, AudioBus.Music);

            GameEvents.PlayerVehicleChanged += OnVehicleChanged;
        }

        private void OnDestroy()
        {
            GameEvents.PlayerVehicleChanged -= OnVehicleChanged;
        }

        private void OnVehicleChanged(GameObject vehicle, bool entered)
        {
            if (entered)
            {
                if (CurrentIndex < 0) SetStation(0);
                else IsOn = true;
            }
            else
            {
                IsOn = false;
                if (_source != null) _source.Pause();
            }
        }

        // ------------------------------------------------------------------
        public void NextStation() => SetStation(CurrentIndex + 1);
        public void PreviousStation() => SetStation(CurrentIndex - 1);

        public void TurnOff()
        {
            IsOn = false;
            CurrentIndex = -1;
            NowPlaying = "";
            if (_source != null) _source.Stop();
            if (_scheduler != null) { StopCoroutine(_scheduler); _scheduler = null; }
        }

        public void SetStation(int index)
        {
            if (_stations.Count == 0) return;
            if (index < 0) index = _stations.Count - 1;
            if (index >= _stations.Count) index = 0;
            CurrentIndex = index;
            IsOn = true;
            Services.Audio?.PlayUi("click");

            if (_scheduler != null) StopCoroutine(_scheduler);
            _scheduler = StartCoroutine(RunStation(_stations[index]));
        }

        private IEnumerator RunStation(Station station)
        {
            NowPlaying = station.Definition.displayName + " - tuning in";
            if (_source != null) _source.Stop();

            while (true)
            {
                // Station identification / DJ line.
                string line = PickLine(station);
                NowPlaying = station.Definition.dj + ": " + line;
                PlaySting(station.Definition);
                float talkTime = Mathf.Clamp(line.Length * 0.045f, 2.5f, 7f);
                float elapsed = 0f;
                while (elapsed < talkTime) { elapsed += Time.unscaledDeltaTime; yield return null; }

                if (station.Definition.talkOnly)
                {
                    // Talk radio: keep cycling through segments.
                    continue;
                }

                // Make sure a track exists, generating it across frames.
                if (station.Tracks.Count < 2 && !_generating)
                    yield return StartCoroutine(GenerateTrack(station));

                if (station.Tracks.Count == 0) { yield return null; continue; }

                var clip = station.Tracks[station.NextTrack % station.Tracks.Count];
                station.NextTrack++;
                NowPlaying = station.Definition.displayName + " - " + station.Definition.genre;

                if (_source != null)
                {
                    _source.clip = clip;
                    _source.volume = Services.Audio != null ? Services.Audio.BusVolume(AudioBus.Music) : 0.6f;
                    _source.Play();
                }

                float duration = clip.length;
                elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (_source != null && Services.Audio != null)
                        _source.volume = IsOn ? Services.Audio.BusVolume(AudioBus.Music) : 0f;
                    yield return null;
                }
            }
        }

        private string PickLine(Station station)
        {
            var definition = station.Definition;
            station.LineCursor++;
            int bucket = station.LineCursor % 3;

            string[] pool = bucket == 0 ? definition.djLines : (bucket == 1 ? definition.adverts : definition.newsLines);
            if (pool == null || pool.Length == 0) pool = definition.djLines;
            if (pool == null || pool.Length == 0) return definition.displayName;

            string line = pool[station.LineCursor % pool.Length];
            if (bucket == 2 && Services.Clock != null)
                line = "News at " + Services.Clock.ClockText + ": " + line;
            return line;
        }

        private void PlaySting(RadioStationDefinition definition)
        {
            if (_sting == null || Services.Audio == null) return;
            float baseHz = 260f + (definition.rootNote - 40) * 8f;
            var clip = ProceduralAudio.UiTone(baseHz, 0.5f, definition.energy * 0.8f, definition.distortion > 0.5f);
            _sting.PlayOneShot(clip, 0.35f * Services.Audio.BusVolume(AudioBus.Music));
        }

        /// <summary>Composes a track without stalling the frame.</summary>
        private IEnumerator GenerateTrack(Station station)
        {
            _generating = true;
            NowPlaying = station.Definition.displayName + " - up next";

            float[] data = null;
            var thread = new System.Threading.Thread(() =>
            {
                data = MusicSynth.RenderTrack(station.Definition, station.Definition.id.GetHashCode() + station.Tracks.Count * 7919);
            });
            thread.IsBackground = true;
            thread.Start();

            while (thread.IsAlive) yield return null;

            if (data != null)
            {
                var clip = AudioClip.Create(station.Definition.id + "_track" + station.Tracks.Count, data.Length, 1, MusicSynth.SampleRate, false);
                clip.SetData(data, 0);
                station.Tracks.Add(clip);
            }
            _generating = false;
        }

        private void Update()
        {
            if (_source == null) return;
            bool shouldPlay = IsOn && Services.Player != null && Services.Player.InVehicle;
            if (shouldPlay && !_source.isPlaying && _source.clip != null) _source.UnPause();
            else if (!shouldPlay && _source.isPlaying) _source.Pause();
        }

        public int CaptureState() => CurrentIndex;

        public void RestoreState(int index)
        {
            if (index >= 0 && index < _stations.Count) SetStation(index);
        }
    }
}
