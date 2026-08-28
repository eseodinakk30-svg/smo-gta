using UnityEngine;

namespace SanMonica.Core
{
    /// <summary>
    /// Entry point. San Monica builds itself at runtime, so the game starts from
    /// any scene - press Play in an empty scene and the whole city boots.
    /// </summary>
    public static class GameBootstrap
    {
        public static int OverrideSeed;
        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (_started) return;
            if (Object.FindAnyObjectByType<GameManager>() != null) { _started = true; return; }
            _started = true;

            Application.runInBackground = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;
            Physics.defaultSolverIterations = 6;
            Physics.defaultSolverVelocityIterations = 2;
            Time.fixedDeltaTime = 1f / 50f;
            Time.maximumDeltaTime = 0.1f;

            var root = new GameObject("SanMonica");
            Object.DontDestroyOnLoad(root);
            var manager = root.AddComponent<GameManager>();

            int seed = OverrideSeed != 0 ? OverrideSeed : 20260823;
            manager.Boot(seed);
        }

        /// <summary>Restarts the game with a different world seed.</summary>
        public static void RestartWithSeed(int seed)
        {
            OverrideSeed = seed;
            _started = false;
            GameEvents.ResetAll();
            Services.Clear();
            var existing = Object.FindAnyObjectByType<GameManager>();
            if (existing != null) Object.Destroy(existing.gameObject);
            Launch();
        }
    }
}
