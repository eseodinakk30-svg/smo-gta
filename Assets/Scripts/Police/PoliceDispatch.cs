using System.Collections.Generic;
using UnityEngine;
using SanMonica.AI;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Traffic;
using SanMonica.Vehicles;
using SanMonica.World;

namespace SanMonica.Police
{
    /// <summary>
    /// Dispatches the SMPD response: patrol cars first, then interceptors,
    /// roadblocks, a helicopter and finally tactical teams. Units spawn out of
    /// sight, converge on your last known position and search when they lose you.
    /// </summary>
    public class PoliceDispatch : MonoBehaviour
    {
        [Header("Response")]
        public int MaxUnits = 12;
        public float SpawnRadius = 140f;
        public float DespawnRadius = 320f;

        private WorldConfig _cfg;
        private WorldMap _map;
        private RoadNetwork _roads;
        private VehicleFactory _vehicles;
        private SanMonica.Characters.PedFactory _peds;
        private GameDatabase _db;
        private Rng _rng;

        private readonly List<Vehicle> _units = new List<Vehicle>(16);
        private readonly List<PedBrain> _officers = new List<PedBrain>(24);
        private readonly List<Vehicle> _helicopters = new List<Vehicle>(2);
        private readonly List<GameObject> _roadblocks = new List<GameObject>(4);
        private float _spawnTimer;
        private float _maintainTimer;

        public int ActiveUnits => _units.Count;
        public int ActiveOfficers => _officers.Count;

        public void Initialize(WorldConfig cfg, WorldMap map, RoadNetwork roads, VehicleFactory vehicles, SanMonica.Characters.PedFactory peds, GameDatabase db)
        {
            _cfg = cfg; _map = map; _roads = roads; _vehicles = vehicles; _peds = peds; _db = db;
            _rng = new Rng(cfg.seed ^ 0x900D);
            GameEvents.WantedLevelChanged += OnWantedChanged;
        }

        private void OnDestroy()
        {
            GameEvents.WantedLevelChanged -= OnWantedChanged;
        }

        private void OnWantedChanged(int level)
        {
            if (level == 0) StandDown();
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            var wanted = Services.Wanted;
            if (wanted == null) return;
            float dt = Time.deltaTime;

            _maintainTimer -= dt;
            if (_maintainTimer <= 0f)
            {
                _maintainTimer = 0.8f;
                Maintain(wanted);
            }

            if (wanted.Level <= 0) return;

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = Mathf.Lerp(4.5f, 1.4f, (wanted.Level - 1) / 4f);
                TrySpawnResponse(wanted);
            }

            UpdatePursuit(wanted);
        }

        private int DesiredCars(int level)
        {
            switch (level)
            {
                case 1: return 2;
                case 2: return 4;
                case 3: return 6;
                case 4: return 8;
                case 5: return MaxUnits;
                default: return 0;
            }
        }

        private int DesiredHelicopters(int level) => level >= 3 ? (level >= 5 ? 2 : 1) : 0;
        private bool WantsSwat(int level) => level >= 4;
        private bool WantsRoadblocks(int level) => level >= 3;

        // ------------------------------------------------------------------
        private void TrySpawnResponse(WantedSystem wanted)
        {
            Vector3 target = wanted.IsSearching ? wanted.LastKnownPosition : Services.PlayerPosition;

            if (_units.Count < DesiredCars(wanted.Level)) SpawnPatrolCar(wanted, target);
            if (_helicopters.Count < DesiredHelicopters(wanted.Level)) SpawnHelicopter(target);
            if (WantsRoadblocks(wanted.Level) && _roadblocks.Count < wanted.Level - 2) SpawnRoadblock(target);
        }

        private void SpawnPatrolCar(WantedSystem wanted, Vector3 target)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (!_roads.RandomRoadPoint(ref _rng, new Vector2(target.x, target.z), SpawnRadius * 0.6f, SpawnRadius, out var point, out int segment, out bool forward))
                    continue;
                if (Services.Traffic != null && Services.Traffic.IsVisibleToPlayer(point, 0.5f)) continue;

                string id = wanted.Level >= 3 && _rng.Chance(0.45f) ? "interceptor" : "patrol";
                if (WantsSwat(wanted.Level) && _rng.Chance(0.3f)) id = "enforcer";
                var def = _db.Vehicle(id);
                if (def == null) continue;

                var seg = _roads.Segments[segment];
                Vector2 dir = forward ? seg.Dir : -seg.Dir;
                var car = _vehicles.Spawn(def, point + Vector3.up * 0.25f, Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up));
                if (car == null) continue;

                car.HasOwner = true;
                car.SetSiren(true);
                car.LightsOn = true;

                var driver = Services.Traffic?.AttachDriver(car, true, Services.Player != null ? Services.Player.transform : null);
                if (driver != null) driver.AggressionScale = Mathf.Lerp(0.9f, 1.35f, (wanted.Level - 1) / 4f);

                int crew = id == "enforcer" ? 4 : 2;
                for (int i = 0; i < crew; i++)
                {
                    var archetype = _db.Ped(WantsSwat(wanted.Level) && i > 0 ? "swat" : "cop");
                    if (archetype == null) continue;
                    var officer = _peds.Spawn(archetype, car.transform.position + Vector3.up, car.transform.rotation);
                    if (officer == null) continue;
                    officer.SeatInVehicle(car, i);
                    _officers.Add(officer);
                }

                _units.Add(car);
                return;
            }
        }

        private void SpawnHelicopter(Vector3 target)
        {
            var def = _db.Vehicle("heli-police");
            if (def == null) return;
            Vector2 offset = _rng.InsideUnitCircle().normalized * 220f;
            Vector3 spawn = new Vector3(target.x + offset.x, 0f, target.z + offset.y);
            spawn.y = Mathf.Max(_map.SampleHeight(spawn.x, spawn.z), 0f) + 110f;

            var heli = _vehicles.Spawn(def, spawn, Quaternion.LookRotation((target - spawn).normalized, Vector3.up));
            if (heli == null) return;
            heli.SetSiren(true);
            var pilot = heli.gameObject.GetComponent<PoliceHelicopterPilot>();
            if (pilot == null) pilot = heli.gameObject.AddComponent<PoliceHelicopterPilot>();
            pilot.Bind(heli);
            _helicopters.Add(heli);
            GameEvents.Notify("SMPD Vigil is overhead", 2.6f);
        }

        private void SpawnRoadblock(Vector3 target)
        {
            var player = Services.Player;
            if (player == null || _roads == null) return;

            // Place the block on the road ahead of where the player is heading.
            Vector3 ahead = player.transform.position + (player.CurrentVehicle != null
                ? player.CurrentVehicle.transform.forward : player.transform.forward) * 160f;
            int segment = _roads.NearestSegment(new Vector2(ahead.x, ahead.z), 160f);
            if (segment < 0) return;

            var seg = _roads.Segments[segment];
            Vector2 mid = seg.Point(0.5f);
            Vector3 centre = new Vector3(mid.x, _map.SampleHeight(mid.x, mid.y), mid.y);
            if (Services.Traffic != null && Services.Traffic.IsVisibleToPlayer(centre, 0.5f)) return;

            var root = new GameObject("Roadblock");
            root.transform.position = centre;
            Vector3 right = new Vector3(seg.Right.x, 0f, seg.Right.y);

            var def = _db.Vehicle("patrol");
            for (int i = -1; i <= 1; i++)
            {
                Vector3 spot = centre + right * (i * 3.4f);
                spot.y = _map.SampleHeight(spot.x, spot.z) + 0.3f;
                var car = _vehicles.Spawn(def, spot, Quaternion.LookRotation(right, Vector3.up));
                if (car == null) continue;
                car.SetSiren(true);
                car.transform.SetParent(root.transform, true);
                _units.Add(car);

                var archetype = _db.Ped("cop");
                if (archetype != null)
                {
                    var officer = _peds.Spawn(archetype, spot + right.normalized * 2.5f + Vector3.up * 0.2f, Quaternion.LookRotation(-right));
                    if (officer != null) _officers.Add(officer);
                }
            }

            _roadblocks.Add(root);
            GameEvents.Notify("Roadblock ahead", 2.4f);
        }

        // ------------------------------------------------------------------
        private void UpdatePursuit(WantedSystem wanted)
        {
            var player = Services.Player;
            if (player == null) return;
            Vector3 chasePoint = wanted.IsSearching ? wanted.LastKnownPosition : player.transform.position;

            for (int i = 0; i < _units.Count; i++)
            {
                var car = _units[i];
                if (car == null || car.IsDestroyed) continue;
                var driver = car.GetComponent<TrafficDriver>();
                if (driver == null) continue;

                if (wanted.IsSearching)
                {
                    driver.ChaseTarget = null;
                    // Search pattern: drive toward the last known position.
                    if (Vector3.Distance(car.transform.position, chasePoint) > 40f)
                        driver.Mood = DriverMood.Hurried;
                }
                else driver.ChaseTarget = player.transform;
            }

            for (int i = 0; i < _officers.Count; i++)
            {
                var officer = _officers[i];
                if (officer == null || officer.Health == null || !officer.Health.IsAlive) continue;
                if (officer.InVehicle)
                {
                    // Bail out and fight when close enough or when the player is on foot.
                    float distance = Vector3.Distance(officer.transform.position, player.transform.position);
                    if (!player.InVehicle && distance < 26f) officer.ForceExitVehicle();
                }
                else if (officer.State != PedState.Combat)
                {
                    officer.SetDestination(chasePoint);
                    if (officer.State != PedState.Investigate) officer.EnterState(PedState.Investigate);
                }
            }
        }

        private void Maintain(WantedSystem wanted)
        {
            Vector3 player = Services.PlayerPosition;

            for (int i = _units.Count - 1; i >= 0; i--)
            {
                var car = _units[i];
                if (car == null) { _units.RemoveAt(i); continue; }
                float d = Vector3.Distance(car.transform.position, player);
                if (wanted.Level <= 0 || d > DespawnRadius)
                {
                    Services.Traffic?.DetachDriver(car);
                    _vehicles.Despawn(car);
                    _units.RemoveAt(i);
                }
            }

            for (int i = _officers.Count - 1; i >= 0; i--)
            {
                var officer = _officers[i];
                if (officer == null) { _officers.RemoveAt(i); continue; }
                float d = Vector3.Distance(officer.transform.position, player);
                bool dead = officer.Health != null && !officer.Health.IsAlive;
                if ((wanted.Level <= 0 && !dead) || d > DespawnRadius)
                {
                    _peds.Despawn(officer);
                    _officers.RemoveAt(i);
                }
                else if (dead && officer.Age > 40f)
                {
                    _peds.Despawn(officer);
                    _officers.RemoveAt(i);
                }
            }

            for (int i = _helicopters.Count - 1; i >= 0; i--)
            {
                var heli = _helicopters[i];
                if (heli == null) { _helicopters.RemoveAt(i); continue; }
                if (wanted.Level < 3 || heli.IsDestroyed)
                {
                    _vehicles.Despawn(heli);
                    _helicopters.RemoveAt(i);
                }
            }

            if (wanted.Level < 3)
            {
                for (int i = _roadblocks.Count - 1; i >= 0; i--)
                {
                    if (_roadblocks[i] != null) Destroy(_roadblocks[i]);
                    _roadblocks.RemoveAt(i);
                }
            }
        }

        public void StandDown()
        {
            foreach (var car in _units)
            {
                if (car == null) continue;
                car.SetSiren(false);
                Services.Traffic?.DetachDriver(car);
                _vehicles.Despawn(car);
            }
            _units.Clear();

            foreach (var officer in _officers) if (officer != null) _peds.Despawn(officer);
            _officers.Clear();

            foreach (var heli in _helicopters) if (heli != null) _vehicles.Despawn(heli);
            _helicopters.Clear();

            foreach (var block in _roadblocks) if (block != null) Destroy(block);
            _roadblocks.Clear();
        }

        // ------------------------------------------------------------------
        public bool AnyOfficerSeesPlayer()
        {
            var player = Services.Player;
            if (player == null) return false;
            for (int i = 0; i < _officers.Count; i++)
            {
                var officer = _officers[i];
                if (officer == null || officer.Perception == null) continue;
                if (officer.Health != null && !officer.Health.IsAlive) continue;
                if (officer.Perception.CanSeePlayer) return true;
            }
            for (int i = 0; i < _units.Count; i++)
            {
                var car = _units[i];
                if (car == null || car.IsDestroyed) continue;
                float d = Vector3.Distance(car.transform.position, player.transform.position);
                if (d < 55f && !Physics.Linecast(car.transform.position + Vector3.up, player.transform.position + Vector3.up,
                        GameLayers.VisionBlockMask, QueryTriggerInteraction.Ignore)) return true;
            }
            for (int i = 0; i < _helicopters.Count; i++)
            {
                var heli = _helicopters[i];
                if (heli == null || heli.IsDestroyed) continue;
                if (Vector3.Distance(heli.transform.position, player.transform.position) < 130f) return true;
            }
            return false;
        }

        public bool OfficerWithin(Vector3 position, float radius)
        {
            float sqr = radius * radius;
            for (int i = 0; i < _officers.Count; i++)
            {
                var officer = _officers[i];
                if (officer == null || officer.InVehicle) continue;
                if (officer.Health != null && !officer.Health.IsAlive) continue;
                if ((officer.transform.position - position).sqrMagnitude < sqr) return true;
            }
            return false;
        }
    }

    /// <summary>Keeps the police helicopter circling the pursuit at a safe height.</summary>
    public class PoliceHelicopterPilot : MonoBehaviour
    {
        private Vehicle _heli;
        private float _angle;

        public void Bind(Vehicle heli) { _heli = heli; }

        private void FixedUpdate()
        {
            if (_heli == null || _heli.IsDestroyed || _heli.Body == null) return;
            var wanted = Services.Wanted;
            if (wanted == null) return;

            Vector3 target = wanted.IsSearching ? wanted.LastKnownPosition : Services.PlayerPosition;
            _angle += Time.fixedDeltaTime * 0.35f;
            Vector3 orbit = target + new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * 55f;
            float ground = Services.Map != null ? Services.Map.SampleHeight(orbit.x, orbit.z) : 0f;
            orbit.y = ground + 75f;

            Vector3 delta = orbit - _heli.transform.position;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);

            // Steer with the same controls a player would use.
            Vector3 local = _heli.transform.InverseTransformDirection(flat.normalized);
            float pitch = Mathf.Clamp(local.z, -1f, 1f) * Mathf.Clamp01(flat.magnitude / 40f);
            float roll = Mathf.Clamp(local.x, -1f, 1f) * Mathf.Clamp01(flat.magnitude / 40f);
            float collective = Mathf.Clamp(delta.y / 18f, -1f, 1f);

            float yawError = Vector3.SignedAngle(_heli.transform.forward, flat.sqrMagnitude > 1f ? flat.normalized : _heli.transform.forward, Vector3.up);
            _heli.SetInput(0f, 0f, Mathf.Clamp(yawError / 45f, -1f, 1f), false);
            _heli.SetAirInput(-pitch, roll, collective);
        }
    }
}
