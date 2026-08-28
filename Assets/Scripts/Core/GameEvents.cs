using System;
using UnityEngine;

namespace SanMonica.Core
{
    public enum CrimeType
    {
        None, Trespassing, Vandalism, VehicleTheft, Assault, WeaponFired, Murder,
        PoliceAssault, PoliceMurder, Explosion, RecklessDriving, HitAndRun, Robbery, Escape
    }

    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Loudness;      // metres at which the sound is still audible to AI
        public GameObject Source;
        public bool IsGunshot;
    }

    public struct CrimeEvent
    {
        public CrimeType Type;
        public Vector3 Position;
        public GameObject Perpetrator;
        public bool WitnessedByPolice;
        public int WitnessCount;
    }

    /// <summary>
    /// Global, low-garbage event bus. Systems talk through this hub instead of
    /// holding hard references to each other, which keeps the dependency graph flat.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<CrimeEvent> CrimeCommitted;
        public static event Action<NoiseEvent> NoiseMade;
        public static event Action<int> WantedLevelChanged;
        public static event Action<long, long> MoneyChanged;             // newBalance, delta
        public static event Action<string, float> Notification;          // message, duration
        public static event Action<string> SubtitleRequested;
        public static event Action<GameObject, GameObject> PedKilled;    // victim, killer
        public static event Action<GameObject> VehicleDestroyed;
        public static event Action<GameObject, bool> PlayerVehicleChanged; // vehicle, entered
        public static event Action<string> MissionStarted;
        public static event Action<string, bool> MissionEnded;           // id, success
        public static event Action<GameState> GameStateChanged;
        public static event Action<float> WorldStreamProgress;
        public static event Action PlayerDied;
        public static event Action PlayerRespawned;
        public static event Action<Vector3, float> ExplosionOccurred;
        public static event Action<int> HourChanged;
        public static event Action<string> WeatherChanged;

        public static void RaiseCrime(CrimeEvent e) => CrimeCommitted?.Invoke(e);
        public static void RaiseNoise(NoiseEvent e) => NoiseMade?.Invoke(e);
        public static void RaiseWanted(int level) => WantedLevelChanged?.Invoke(level);
        public static void RaiseMoney(long balance, long delta) => MoneyChanged?.Invoke(balance, delta);
        public static void Notify(string message, float duration = 3.5f) => Notification?.Invoke(message, duration);
        public static void Subtitle(string line) => SubtitleRequested?.Invoke(line);
        public static void RaisePedKilled(GameObject victim, GameObject killer) => PedKilled?.Invoke(victim, killer);
        public static void RaiseVehicleDestroyed(GameObject v) => VehicleDestroyed?.Invoke(v);
        public static void RaisePlayerVehicleChanged(GameObject v, bool entered) => PlayerVehicleChanged?.Invoke(v, entered);
        public static void RaiseMissionStarted(string id) => MissionStarted?.Invoke(id);
        public static void RaiseMissionEnded(string id, bool success) => MissionEnded?.Invoke(id, success);
        public static void RaiseGameState(GameState s) => GameStateChanged?.Invoke(s);
        public static void RaiseStreamProgress(float p) => WorldStreamProgress?.Invoke(p);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        public static void RaiseExplosion(Vector3 pos, float radius) => ExplosionOccurred?.Invoke(pos, radius);
        public static void RaiseHourChanged(int hour) => HourChanged?.Invoke(hour);
        public static void RaiseWeatherChanged(string weather) => WeatherChanged?.Invoke(weather);

        /// <summary>Clears every subscription. Called when the game shuts down or reloads.</summary>
        public static void ResetAll()
        {
            CrimeCommitted = null; NoiseMade = null; WantedLevelChanged = null; MoneyChanged = null;
            Notification = null; SubtitleRequested = null; PedKilled = null; VehicleDestroyed = null;
            PlayerVehicleChanged = null; MissionStarted = null; MissionEnded = null; GameStateChanged = null;
            WorldStreamProgress = null; PlayerDied = null; PlayerRespawned = null; ExplosionOccurred = null;
            HourChanged = null; WeatherChanged = null;
        }
    }

    public enum GameState
    {
        Booting,
        GeneratingWorld,
        Playing,
        Paused,
        InMenu,
        Cutscene,
        Dead,
        Busted,
        Shopping
    }
}
