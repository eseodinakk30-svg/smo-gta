using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Missions
{
    /// <summary>
    /// Plays dialogue as timed subtitles and can take over the camera for short
    /// cutscenes between objectives.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        public string CurrentLine { get; private set; }
        public float SecondsPerCharacter = 0.045f;
        public float MinimumLineTime = 1.8f;
        public float MaximumLineTime = 6.5f;

        private Coroutine _routine;
        private readonly Queue<string> _queue = new Queue<string>();

        public void PlaySequence(string[] lines, System.Action onComplete)
        {
            if (lines == null || lines.Length == 0) { onComplete?.Invoke(); return; }
            if (_routine != null) StopCoroutine(_routine);
            _queue.Clear();
            foreach (var line in lines) _queue.Enqueue(line);
            _routine = StartCoroutine(Run(onComplete));
        }

        public void Say(string line, float duration = 3f)
        {
            CurrentLine = line;
            GameEvents.Subtitle(line);
            CancelInvoke(nameof(ClearLine));
            Invoke(nameof(ClearLine), duration);
        }

        private void ClearLine()
        {
            CurrentLine = null;
            GameEvents.Subtitle(null);
        }

        private IEnumerator Run(System.Action onComplete)
        {
            IsPlaying = true;
            while (_queue.Count > 0)
            {
                string line = _queue.Dequeue();
                CurrentLine = line;
                GameEvents.Subtitle(line);
                Services.Audio?.PlayUi("dialogue");
                float duration = Mathf.Clamp(line.Length * SecondsPerCharacter, MinimumLineTime, MaximumLineTime);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    // Tapping interact skips a line.
                    if (Services.Input != null && Services.Input.InteractPressed) break;
                    yield return null;
                }
            }
            CurrentLine = null;
            GameEvents.Subtitle(null);
            IsPlaying = false;
            _routine = null;
            onComplete?.Invoke();
        }

        /// <summary>Short establishing shot that orbits a point, then hands control back.</summary>
        public void PlayCutscene(Vector3 focus, float duration, System.Action onComplete)
        {
            StartCoroutine(CutsceneRoutine(focus, duration, onComplete));
        }

        private IEnumerator CutsceneRoutine(Vector3 focus, float duration, System.Action onComplete)
        {
            var camera = Services.Camera;
            if (camera == null) { onComplete?.Invoke(); yield break; }

            Services.Game?.SetState(GameState.Cutscene);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float angle = elapsed * 0.45f;
                Vector3 position = focus + new Vector3(Mathf.Cos(angle), 0.42f, Mathf.Sin(angle)) * 9f;
                camera.SetCinematic(position, focus + Vector3.up * 1.2f);
                if (Services.Input != null && Services.Input.InteractPressed) break;
                yield return null;
            }
            camera.EndCinematic();
            Services.Game?.SetState(GameState.Playing);
            onComplete?.Invoke();
        }
    }
}
