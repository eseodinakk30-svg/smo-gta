using System.Collections.Generic;
using UnityEngine;
using SanMonica.Characters;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.World;

namespace SanMonica.AI
{
    /// <summary>
    /// Keeps the streets populated. Pedestrians appear out of sight, wander,
    /// work, panic and disappear again, with the mix chosen by district and hour
    /// so the same street feels different at 8 am and at 2 am.
    /// </summary>
    public class PopulationManager : MonoBehaviour
    {
        [Header("Budget")]
        public int MaxPeds = 70;
        public float DensityScale = 1f;

        private WorldConfig _cfg;
        private WorldMap _map;
        private NavGraph _nav;
        private PedFactory _factory;
        private GameDatabase _db;
        private Rng _rng;
        private float _spawnTimer;
        private float _cullTimer;
        private readonly List<PedBrain> _corpses = new List<PedBrain>(16);

        public int PedCount => _factory != null ? _factory.ActiveCount : 0;

        public void Initialize(WorldConfig cfg, WorldMap map, NavGraph nav, PedFactory factory, GameDatabase db)
        {
            _cfg = cfg; _map = map; _nav = nav; _factory = factory; _db = db;
            _rng = new Rng(cfg.seed ^ 0x1234);
        }

        private void Update()
        {
            // Nothing is spawned until the chunks around the player exist:
            // peds and cars dropped into a world without colliders fall
            // straight through it.
            if (Services.Game == null || !Services.Game.WorldReady) return;
            if (_factory == null || _nav == null || !_nav.Ready) return;
            float dt = Time.deltaTime;
            Vector3 player = Services.PlayerPosition;

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = 0.25f;
                int budget = TargetPopulation(player);
                if (_factory.ActiveCount < budget) TrySpawn(player);
            }

            _cullTimer -= dt;
            if (_cullTimer <= 0f)
            {
                _cullTimer = 0.9f;
                Cull(player);
            }
        }

        private int TargetPopulation(Vector3 player)
        {
            var profile = _map.ProfileAt(player);
            float timeScale = 1f;
            var clock = Services.Clock;
            if (clock != null)
            {
                int hour = clock.Hour;
                if (hour >= 8 && hour < 19) timeScale = 1.2f;
                else if (hour >= 19 && hour < 23) timeScale = 0.95f;
                else timeScale = 0.42f;
            }
            return Mathf.Clamp(Mathf.RoundToInt(MaxPeds * DensityScale * profile.pedDensity * timeScale), 0, 140);
        }

        private void TrySpawn(Vector3 player)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Vector2 offset = _rng.InsideUnitCircle().normalized * _rng.Range(_cfg.pedSpawnRadius * 0.55f, _cfg.pedSpawnRadius);
                Vector3 candidate = player + new Vector3(offset.x, 0f, offset.y);
                if (!_cfg.InWorld(candidate)) continue;
                if (_map.IsWater(candidate.x, candidate.z)) continue;

                Vector3 point = _nav.SnapToWalkable(candidate, 45f);
                if (Vector3.Distance(point, player) < 22f) continue;
                if (IsVisible(point)) continue;
                if (Physics.CheckSphere(point + Vector3.up * 0.9f, 0.6f, GameLayers.CharacterMask | GameLayers.VehicleMask, QueryTriggerInteraction.Ignore)) continue;

                var district = _map.DistrictAt(point.x, point.z);
                int hour = Services.Clock != null ? Services.Clock.Hour : 12;
                var archetype = _db.PickPed(ref _rng, district, hour);
                if (archetype == null) continue;

                // SnapToWalkable already returned a point on the pavement, at the
                // right height for a bridge as well. Overwriting it with the
                // terrain sank every pedestrian into the kerb and dropped the ones
                // on a bridge into the water.
                var brain = _factory.Spawn(archetype, point, Quaternion.Euler(0f, _rng.Value * 360f, 0f));
                if (brain != null) return;
            }
        }

        private void Cull(Vector3 player)
        {
            var peds = _factory.ActivePeds;
            for (int i = peds.Count - 1; i >= 0; i--)
            {
                var brain = peds[i];
                if (brain == null) continue;
                if (brain.InVehicle) continue;

                float distance = Vector3.Distance(brain.transform.position, player);
                bool dead = brain.Health != null && !brain.Health.IsAlive;

                if (dead)
                {
                    // Bodies persist for a while, then are quietly removed.
                    if (brain.Age > 45f || distance > 90f) _factory.Despawn(brain);
                    continue;
                }

                // Never delete someone mid-firefight: a gunman who vanishes
                // because you ran forty metres is worse than no gunman at all.
                if (brain.State == PedState.Combat && distance < 160f) continue;

                if (distance > _cfg.pedDespawnRadius && !IsVisible(brain.transform.position))
                    _factory.Despawn(brain);
            }
        }

        private bool IsVisible(Vector3 position)
        {
            var cam = Services.Camera;
            if (cam == null || cam.Cam == null) return false;
            Vector3 toPoint = position - cam.Cam.transform.position;
            float distance = toPoint.magnitude;
            if (distance > 200f) return false;
            return Vector3.Dot(cam.Cam.transform.forward, toPoint / Mathf.Max(0.01f, distance)) > 0.35f;
        }

        public void NotifyDeath(PedBrain brain)
        {
            if (brain != null && !_corpses.Contains(brain)) _corpses.Add(brain);
        }

        public void ClearAll()
        {
            _factory?.DespawnAll();
            _corpses.Clear();
        }
    }
}
