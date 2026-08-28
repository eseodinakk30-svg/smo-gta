using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Vehicles;
using SanMonica.World;

namespace SanMonica.Traffic
{
    /// <summary>
    /// Keeps San Monica's streets busy: spawns traffic just out of sight,
    /// recycles it once it falls behind, parks cars in bays near the player and
    /// scales everything to the district, the time of day and the device tier.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public readonly TrafficSignals Signals = new TrafficSignals();

        [Header("Density")]
        public float DensityScale = 1f;
        public int MaxTrafficVehicles = 40;
        public int MaxParkedVehicles = 26;

        private WorldConfig _cfg;
        private WorldMap _map;
        private RoadNetwork _roads;
        private CityLayout _layout;
        private VehicleFactory _factory;
        private GameDatabase _db;

        private readonly List<TrafficDriver> _traffic = new List<TrafficDriver>(64);
        private readonly List<Vehicle> _parked = new List<Vehicle>(48);
        private readonly RoundRobinScheduler _lodScheduler = new RoundRobinScheduler();
        private float _spawnTimer;
        private float _parkTimer;
        private Rng _rng;

        public int TrafficCount => _traffic.Count;
        public int ParkedCount => _parked.Count;

        public void Initialize(WorldConfig cfg, WorldMap map, RoadNetwork roads, CityLayout layout, VehicleFactory factory, GameDatabase db)
        {
            _cfg = cfg; _map = map; _roads = roads; _layout = layout; _factory = factory; _db = db;
            _rng = new Rng(cfg.seed ^ 0x7A4F);
            Signals.Initialize(roads);
            GameEvents.NoiseMade += OnNoise;
            GameEvents.ExplosionOccurred += OnExplosion;
        }

        private void OnDestroy()
        {
            GameEvents.NoiseMade -= OnNoise;
            GameEvents.ExplosionOccurred -= OnExplosion;
        }

        private void Update()
        {
            // Nothing is spawned until the chunks around the player exist:
            // peds and cars dropped into a world without colliders fall
            // straight through it.
            if (Services.Game == null || !Services.Game.WorldReady) return;
            if (_roads == null) return;
            float dt = Time.deltaTime;
            Signals.Tick(dt);

            Vector3 player = Services.PlayerPosition;

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = 0.35f;
                CullTraffic(player);
                int budget = Mathf.RoundToInt(MaxTrafficVehicles * DensityScale * TimeOfDayScale() * DistrictScale(player));
                if (_traffic.Count < budget) TrySpawnTraffic(player);
            }

            _parkTimer -= dt;
            if (_parkTimer <= 0f)
            {
                _parkTimer = 1.1f;
                UpdateParkedCars(player);
            }

            UpdateLevelsOfDetail(player);
        }

        private float TimeOfDayScale()
        {
            var clock = Services.Clock;
            if (clock == null) return 1f;
            int hour = clock.Hour;
            if (hour >= 7 && hour < 10) return 1.35f;     // morning commute
            if (hour >= 16 && hour < 19) return 1.45f;    // evening rush
            if (hour >= 23 || hour < 5) return 0.42f;     // night
            return 1f;
        }

        private float DistrictScale(Vector3 position)
        {
            var profile = _map.ProfileAt(position);
            return Mathf.Clamp(profile.trafficDensity, 0.1f, 2.4f);
        }

        // ------------------------------------------------------------------
        private void TrySpawnTraffic(Vector3 player)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (!_roads.RandomRoadPoint(ref _rng, new Vector2(player.x, player.z),
                        _cfg.vehicleSpawnRadius * 0.55f, _cfg.vehicleSpawnRadius, out var point, out int segment, out bool forward))
                    continue;

                if (_map.IsWater(point.x, point.z)) continue;
                if (IsVisibleToPlayer(point, 0.35f)) continue;
                if (IsOccupied(point, 6f)) continue;

                var district = _map.DistrictAt(point.x, point.z);
                var def = _db.PickTrafficVehicle(ref _rng, district);
                if (def == null) continue;

                var seg = _roads.Segments[segment];
                Vector2 dir2 = forward ? seg.Dir : -seg.Dir;
                var rotation = Quaternion.LookRotation(new Vector3(dir2.x, 0f, dir2.y), Vector3.up);

                var vehicle = _factory.Spawn(def, point + Vector3.up * 0.2f, rotation);
                if (vehicle == null) continue;

                var driver = vehicle.gameObject.GetComponent<TrafficDriver>();
                if (driver == null) driver = vehicle.gameObject.AddComponent<TrafficDriver>();
                driver.enabled = true;
                driver.ChaseTarget = null;
                driver.IsPolice = false;
                driver.Mood = _rng.Chance(0.15f) ? DriverMood.Hurried : (_rng.Chance(0.12f) ? DriverMood.Cautious : DriverMood.Normal);
                driver.Initialize(vehicle, _roads, segment, forward, _rng.Range(0, seg.LanesPerDirection), 0f);
                vehicle.HasOwner = true;
                vehicle.LightsOn = false;

                // Occupy the driver's seat with a simple pedestrian so the car is not empty.
                Services.Peds?.SpawnDriverFor(vehicle);

                _traffic.Add(driver);
                return;
            }
        }

        private void CullTraffic(Vector3 player)
        {
            for (int i = _traffic.Count - 1; i >= 0; i--)
            {
                var driver = _traffic[i];
                if (driver == null || driver.Vehicle == null) { _traffic.RemoveAt(i); continue; }
                var vehicle = driver.Vehicle;
                if (vehicle.DriverIsPlayer || vehicle.IsMissionVehicle)
                {
                    driver.enabled = false;
                    _traffic.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(vehicle.transform.position, player);
                if (distance > _cfg.vehicleDespawnRadius && !IsVisibleToPlayer(vehicle.transform.position, 0f))
                {
                    Services.Peds?.DespawnOccupants(vehicle);
                    _factory.Despawn(vehicle);
                    _traffic.RemoveAt(i);
                }
                else if (vehicle.IsDestroyed && distance > 90f)
                {
                    _factory.Despawn(vehicle);
                    _traffic.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------
        private void UpdateParkedCars(Vector3 player)
        {
            for (int i = _parked.Count - 1; i >= 0; i--)
            {
                var v = _parked[i];
                if (v == null) { _parked.RemoveAt(i); continue; }
                if (v.DriverIsPlayer || v.IsPlayerOwned || v.IsMissionVehicle) { _parked.RemoveAt(i); continue; }
                float d = Vector3.Distance(v.transform.position, player);
                if (d > _cfg.vehicleDespawnRadius * 1.1f && !IsVisibleToPlayer(v.transform.position, 0f))
                {
                    _factory.Despawn(v);
                    _parked.RemoveAt(i);
                }
            }

            int budget = Mathf.RoundToInt(MaxParkedVehicles * DensityScale);
            if (_parked.Count >= budget || _layout == null) return;

            for (int attempt = 0; attempt < 6 && _parked.Count < budget; attempt++)
            {
                int index = _rng.Range(0, _layout.ParkingSpots.Count);
                if (_layout.ParkingSpots.Count == 0) return;
                var spot = _layout.ParkingSpots[index];
                float distance = Vector3.Distance(spot.Position, player);
                if (distance > _cfg.vehicleSpawnRadius || distance < 12f) continue;
                if (IsOccupied(spot.Position, 3.2f)) continue;
                if (IsVisibleToPlayer(spot.Position, 0.5f)) continue;

                var district = _map.DistrictAt(spot.Position.x, spot.Position.z);
                var vehicle = _factory.SpawnParked(spot.Position, spot.Yaw, district, ref _rng);
                if (vehicle != null) _parked.Add(vehicle);
            }
        }

        // ------------------------------------------------------------------
        private void UpdateLevelsOfDetail(Vector3 player)
        {
            _lodScheduler.Slice(_traffic.Count, Mathf.Max(4, _traffic.Count / 4), out int start, out int count);
            for (int i = 0; i < count; i++)
            {
                var driver = _traffic[start + i];
                if (driver == null || driver.Vehicle == null) continue;
                float d = Vector3.Distance(driver.transform.position, player);
                int lod = d < 55f ? 0 : (d < 150f ? 1 : 2);
                driver.SetLod(lod);

                var body = driver.Vehicle.Body;
                if (body != null)
                {
                    // Far away cars do not need continuous collision detection.
                    var mode = lod == 0 ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
                    if (body.collisionDetectionMode != mode && !body.isKinematic) body.collisionDetectionMode = mode;
                }
            }
        }

        // ------------------------------------------------------------------
        public bool IsOccupied(Vector3 position, float radius)
        {
            return Physics.CheckSphere(position + Vector3.up * 0.8f, radius, GameLayers.VehicleMask, QueryTriggerInteraction.Ignore);
        }

        public bool IsVisibleToPlayer(Vector3 position, float minDot)
        {
            var cam = Services.Camera;
            if (cam == null || cam.Cam == null) return false;
            Vector3 toPoint = position - cam.Cam.transform.position;
            float distance = toPoint.magnitude;
            if (distance > 260f) return false;
            return Vector3.Dot(cam.Cam.transform.forward, toPoint / Mathf.Max(0.01f, distance)) > minDot;
        }

        /// <summary>Registers an externally created vehicle (police, mission) with the traffic AI.</summary>
        public TrafficDriver AttachDriver(Vehicle vehicle, bool police, Transform chaseTarget = null)
        {
            if (vehicle == null) return null;
            var driver = vehicle.gameObject.GetComponent<TrafficDriver>();
            if (driver == null) driver = vehicle.gameObject.AddComponent<TrafficDriver>();
            driver.enabled = true;
            driver.IsPolice = police;
            driver.ChaseTarget = chaseTarget;
            driver.Mood = police ? DriverMood.Pursuing : DriverMood.Normal;

            int segment = _roads.NearestSegment(new Vector2(vehicle.transform.position.x, vehicle.transform.position.z), 300f);
            if (segment >= 0)
            {
                var seg = _roads.Segments[segment];
                RoadNetwork.DistanceToSegment(new Vector2(vehicle.transform.position.x, vehicle.transform.position.z), in seg, out float t);
                bool forward = Vector2.Dot(seg.Dir, new Vector2(vehicle.transform.forward.x, vehicle.transform.forward.z)) >= 0f;
                driver.Initialize(vehicle, _roads, segment, forward, 0, forward ? t : 1f - t);
            }
            if (!_traffic.Contains(driver)) _traffic.Add(driver);
            return driver;
        }

        public void DetachDriver(Vehicle vehicle)
        {
            if (vehicle == null) return;
            var driver = vehicle.gameObject.GetComponent<TrafficDriver>();
            if (driver == null) return;
            driver.enabled = false;
            _traffic.Remove(driver);
        }

        // ------------------------------------------------------------------
        private void OnNoise(NoiseEvent e)
        {
            if (!e.IsGunshot) return;
            for (int i = 0; i < _traffic.Count; i++)
            {
                var d = _traffic[i];
                if (d == null || d.Vehicle == null || d.IsPolice) continue;
                if ((d.transform.position - e.Position).sqrMagnitude < e.Loudness * e.Loudness)
                    d.Panic(10f);
            }
        }

        private void OnExplosion(Vector3 position, float radius)
        {
            for (int i = 0; i < _traffic.Count; i++)
            {
                var d = _traffic[i];
                if (d == null || d.Vehicle == null) continue;
                float distance = Vector3.Distance(d.transform.position, position);
                if (distance < radius * 1.6f)
                {
                    d.Vehicle.ApplyDamage(SanMonica.Characters.DamageInfo.Simple(
                        Mathf.Lerp(320f, 40f, distance / (radius * 1.6f)), SanMonica.Characters.DamageKind.Explosion,
                        null, position, (d.transform.position - position).normalized, 900f));
                }
                else if (distance < 120f) d.Panic(12f);
            }
        }

        public void ClearAll()
        {
            foreach (var d in _traffic)
                if (d != null && d.Vehicle != null) _factory.Despawn(d.Vehicle);
            _traffic.Clear();
            foreach (var v in _parked) if (v != null) _factory.Despawn(v);
            _parked.Clear();
        }
    }
}
