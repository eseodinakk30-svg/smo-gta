using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Police
{
    /// <summary>
    /// The San Monica wanted system. Crimes add heat, witnesses and officers
    /// report your position, and the level decays only once the police truly
    /// lose you - distance, line of sight, cover, changing vehicle and time all
    /// feed into whether you get away.
    /// </summary>
    public class WantedSystem : MonoBehaviour
    {
        [Header("Configuration")]
        public int MaxLevel = 5;
        public float[] LevelThresholds = { 0f, 20f, 65f, 150f, 300f, 520f };
        public float EvasionSeconds = 26f;          // unseen time needed to start losing a level
        public float HeatDecayPerSecond = 6f;
        public float SearchRadiusPerLevel = 60f;

        public int Level { get; private set; }
        public float Heat { get; private set; }
        public bool IsSearching { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public float TimeSinceSeen { get; private set; }
        public float SearchRadius => Mathf.Max(45f, SearchRadiusPerLevel * Level);
        public bool IsWanted => Level > 0;

        private float _evasionTimer;
        private float _bustedTimer;
        private float _notifyTimer;

        private void OnEnable()
        {
            GameEvents.CrimeCommitted += OnCrime;
        }

        private void OnDisable()
        {
            GameEvents.CrimeCommitted -= OnCrime;
        }

        public void ResetWanted()
        {
            Heat = 0f;
            SetLevel(0);
            _evasionTimer = 0f;
            IsSearching = false;
        }

        public void SetLevelDirect(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            Heat = level > 0 && level < LevelThresholds.Length ? LevelThresholds[level] + 1f : 0f;
            SetLevel(level);
        }

        private void OnCrime(CrimeEvent e)
        {
            var player = Services.Player;
            if (player == null || e.Perpetrator != player.gameObject) return;

            float heat = HeatFor(e.Type);
            bool observed = e.WitnessedByPolice || PoliceCanSeePlayer();

            // Unobserved petty crime barely registers; murder always does.
            if (!observed)
            {
                if (e.Type == CrimeType.Murder || e.Type == CrimeType.PoliceMurder || e.Type == CrimeType.Explosion)
                    heat *= 0.65f;
                else if (e.Type == CrimeType.WeaponFired) heat *= 0.25f;
                else heat *= 0.35f;
            }

            AddHeat(heat, e.Position);
        }

        private static float HeatFor(CrimeType type)
        {
            switch (type)
            {
                case CrimeType.Trespassing: return 6f;
                case CrimeType.Vandalism: return 8f;
                case CrimeType.RecklessDriving: return 5f;
                case CrimeType.VehicleTheft: return 24f;
                case CrimeType.Assault: return 26f;
                case CrimeType.WeaponFired: return 22f;
                case CrimeType.Robbery: return 45f;
                case CrimeType.HitAndRun: return 60f;
                case CrimeType.Murder: return 90f;
                case CrimeType.Explosion: return 130f;
                case CrimeType.PoliceAssault: return 110f;
                case CrimeType.PoliceMurder: return 190f;
                default: return 4f;
            }
        }

        public void AddHeat(float amount, Vector3 position)
        {
            if (amount <= 0f) return;
            Heat += amount;
            LastKnownPosition = position;
            TimeSinceSeen = 0f;
            _evasionTimer = 0f;
            RecomputeLevel();
        }

        /// <summary>A civilian phoned it in - the police get your last position.</summary>
        public void ReportCrimeByWitness(Vector3 position)
        {
            AddHeat(18f, position);
            if (Level == 0) SetLevelDirect(1);
        }

        private void RecomputeLevel()
        {
            int level = 0;
            for (int i = MaxLevel; i >= 1; i--)
            {
                if (i < LevelThresholds.Length && Heat >= LevelThresholds[i]) { level = i; break; }
            }
            if (level != Level) SetLevel(level);
        }

        private void SetLevel(int level)
        {
            if (Level == level) return;
            int previous = Level;
            Level = level;
            GameEvents.RaiseWanted(level);

            if (level > previous)
            {
                Services.Audio?.PlayUi("wanted_up");
                if (level == 1) GameEvents.Notify("The SMPD is looking for you", 3f);
                else if (level == 3) GameEvents.Notify("Air support dispatched", 3f);
                else if (level == 4) GameEvents.Notify("Tactical units responding", 3f);
                else if (level == 5) GameEvents.Notify("City-wide manhunt", 3.5f);
            }
            else if (level == 0)
            {
                GameEvents.Notify("You lost the police", 3f);
                Services.Audio?.PlayUi("wanted_clear");
                IsSearching = false;
            }
        }

        private void Update()
        {
            var player = Services.Player;
            if (player == null) return;
            float dt = Time.deltaTime;

            if (Level <= 0)
            {
                Heat = Mathf.Max(0f, Heat - HeatDecayPerSecond * dt * 0.5f);
                return;
            }

            bool seen = PoliceCanSeePlayer();
            if (seen)
            {
                TimeSinceSeen = 0f;
                _evasionTimer = 0f;
                LastKnownPosition = player.transform.position;
                IsSearching = false;
            }
            else
            {
                TimeSinceSeen += dt;
                IsSearching = TimeSinceSeen > 3f;
                _evasionTimer += dt;

                // Hiding in a different vehicle or indoors helps.
                float multiplier = 1f;
                if (Services.Interiors != null && Services.Interiors.IsInside) multiplier = 2.2f;
                else if (player.InVehicle) multiplier = 0.85f;

                float required = EvasionSeconds * Mathf.Lerp(1f, 1.9f, (Level - 1) / 4f);
                if (_evasionTimer * multiplier > required)
                {
                    _evasionTimer = 0f;
                    Heat = Level > 1 && Level < LevelThresholds.Length ? LevelThresholds[Level - 1] * 0.95f : 0f;
                    RecomputeLevel();
                    if (Level > 0) GameEvents.Notify("The search is winding down", 2.4f);
                }
            }

            // Slow passive cooldown even while being chased.
            Heat = Mathf.Max(0f, Heat - HeatDecayPerSecond * dt * (seen ? 0.1f : 0.6f));
            RecomputeLevel();

            UpdateBusted(dt, player);
        }

        private void UpdateBusted(float dt, SanMonica.Players.PlayerController player)
        {
            if (player.InVehicle || Services.Police == null) { _bustedTimer = 0f; return; }
            if (!Services.Police.OfficerWithin(player.transform.position, 3.2f)) { _bustedTimer = Mathf.Max(0f, _bustedTimer - dt); return; }

            bool resisting = player.Weapons != null && player.Weapons.IsWeaponDrawn;
            if (resisting) { _bustedTimer = 0f; return; }

            _bustedTimer += dt;
            _notifyTimer -= dt;
            if (_notifyTimer <= 0f)
            {
                _notifyTimer = 1.2f;
                GameEvents.Notify("Officers are moving in - run or you will be arrested", 1.5f);
            }
            if (_bustedTimer > 3.5f)
            {
                _bustedTimer = 0f;
                Services.Game?.Busted();
            }
        }

        private bool PoliceCanSeePlayer()
        {
            return Services.Police != null && Services.Police.AnyOfficerSeesPlayer();
        }
    }
}
