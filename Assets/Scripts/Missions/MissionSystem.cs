using System.Collections.Generic;
using UnityEngine;
using SanMonica.AI;
using SanMonica.Characters;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;
using SanMonica.Vehicles;
using SanMonica.World;

namespace SanMonica.Missions
{
    /// <summary>
    /// Runs the story and every side job. Objectives spawn real enemies, real
    /// vehicles and real pickups, track real timers and fail for real reasons -
    /// there are no scripted stand-ins here.
    /// </summary>
    public class MissionSystem : MonoBehaviour
    {
        public readonly List<MissionDefinition> Story = new List<MissionDefinition>();
        public readonly List<MissionDefinition> SideMissions = new List<MissionDefinition>();
        public readonly HashSet<string> Completed = new HashSet<string>();

        public MissionDefinition Active { get; private set; }
        public int ObjectiveIndex { get; private set; }
        public string CurrentObjectiveText { get; private set; }
        public float ObjectiveTimeRemaining { get; private set; }
        public Vector3 CurrentObjectivePosition { get; private set; }
        public bool HasObjectiveMarker { get; private set; }
        public int Chapter { get; private set; } = 1;
        public int Respect { get; private set; }

        private readonly Dictionary<string, Vector3> _resolvedStarts = new Dictionary<string, Vector3>();
        private readonly List<PedBrain> _spawnedPeds = new List<PedBrain>(16);
        private readonly List<Vehicle> _spawnedVehicles = new List<Vehicle>(8);
        private readonly List<GameObject> _spawnedProps = new List<GameObject>(8);
        private readonly HashSet<Faction> _hostileFactions = new HashSet<Faction>();
        private readonly List<MapBlip> _blips = new List<MapBlip>(16);

        private MapBlip _objectiveBlip;
        private PedBrain _protectTarget;
        private PedBrain _killTarget;
        private Vehicle _missionVehicle;
        private GameObject _pickup;
        private float _objectiveTimer;
        private float _surviveTimer;
        private float _waveTimer;
        private int _killCount;
        private int _destroyedCount;
        private Rng _rng;
        private bool _failing;

        // ------------------------------------------------------------------
        public void Initialize()
        {
            _rng = new Rng((Services.Config != null ? Services.Config.seed : 1) ^ 0x515);
            Story.AddRange(StoryCatalog.BuildStory());
            SideMissions.AddRange(StoryCatalog.BuildSideMissions());
            GameEvents.PlayerDied += OnPlayerDied;
            RefreshAvailability();
        }

        private void OnDestroy()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
        }

        public bool IsFactionHostile(Faction faction) => _hostileFactions.Contains(faction);

        public void MarkFactionHostile(Faction faction)
        {
            if (faction != Faction.Civilian) _hostileFactions.Add(faction);
        }

        // ------------------------------------------------------------------
        public Vector3 ResolveAnchor(in MissionAnchor anchor)
        {
            var map = Services.Map;
            var layout = Services.Landmarks != null ? Services.Landmarks.Layout : null;
            Vector3 result;

            switch (anchor.Kind)
            {
                case AnchorKind.WorldPoint:
                    result = anchor.Point;
                    break;
                case AnchorKind.DistrictCentre:
                    result = DistrictCentre(anchor.District);
                    break;
                case AnchorKind.RandomInDistrict:
                {
                    Vector3 centre = DistrictCentre(anchor.District);
                    Vector2 offset = _rng.InsideUnitCircle() * Mathf.Max(30f, anchor.Radius);
                    result = centre + new Vector3(offset.x, 0f, offset.y);
                    break;
                }
                case AnchorKind.NearestShop:
                {
                    var shop = layout != null ? layout.NearestShop(Services.PlayerPosition, anchor.Shop) : null;
                    result = shop != null ? shop.Position : Services.PlayerPosition;
                    break;
                }
                case AnchorKind.Landmark:
                {
                    Vector3 centre = LandmarkPosition(anchor.Landmark);
                    if (anchor.Radius > 0.1f)
                    {
                        Vector2 offset = _rng.InsideUnitCircle() * anchor.Radius;
                        centre += new Vector3(offset.x, 0f, offset.y);
                    }
                    result = centre;
                    break;
                }
                case AnchorKind.PlayerRelative:
                {
                    Vector2 dir = _rng.InsideUnitCircle().normalized;
                    result = Services.PlayerPosition + new Vector3(dir.x, 0f, dir.y) * Mathf.Max(20f, anchor.Radius);
                    break;
                }
                default:
                    result = Services.PlayerPosition;
                    break;
            }

            if (map != null)
            {
                if (map.IsWater(result.x, result.z) && anchor.Kind != AnchorKind.WorldPoint)
                    result = map.FindGroundPoint(result, 120f);
                else
                    result.y = Mathf.Max(map.SampleHeight(result.x, result.z), map.IsWater(result.x, result.z) ? 0.5f : 0f);
            }
            return result;
        }

        private Vector3 DistrictCentre(DistrictType district)
        {
            var map = Services.Map;
            if (map == null) return Vector3.zero;
            foreach (var anchor in map.Anchors)
                if (anchor.Type == district)
                    return new Vector3(anchor.Center.x, map.SampleHeight(anchor.Center.x, anchor.Center.y), anchor.Center.y);
            return Services.PlayerPosition;
        }

        private Vector3 LandmarkPosition(LandmarkKind landmark)
        {
            var map = Services.Map;
            if (map == null) return Vector3.zero;
            Vector2 p;
            switch (landmark)
            {
                case LandmarkKind.Downtown: p = map.DowntownCenter; break;
                case LandmarkKind.Port: p = map.PortCenter; break;
                case LandmarkKind.Marina: p = map.MarinaCenter; break;
                case LandmarkKind.Airport: p = map.AirportCenter; break;
                case LandmarkKind.University: p = map.UniversityCenter; break;
                case LandmarkKind.Crestwood: p = map.CrestwoodCenter; break;
                case LandmarkKind.Foundry: p = map.FoundryCenter; break;
                case LandmarkKind.Marigold: p = map.MarigoldCenter; break;
                case LandmarkKind.Park: p = map.ParkCenter; break;
                default:
                {
                    Vector3 v = DistrictCentre(landmark == LandmarkKind.Beach ? DistrictType.Beach
                        : landmark == LandmarkKind.Badlands ? DistrictType.Badlands
                        : landmark == LandmarkKind.Farmland ? DistrictType.Farmland : DistrictType.Mountains);
                    return v;
                }
            }
            return GroundLandmark(p);
        }

        /// <summary>
        /// District centres are fixed points on the map, and three of them -
        /// downtown, the port and the marina - happen to land in the water; the
        /// port is twenty-six metres under. An objective marker dropped there
        /// cannot be reached and the player is given no clue why, so a centre
        /// that is not on usable ground is moved to the nearest pavement.
        /// </summary>
        private Vector3 GroundLandmark(Vector2 p)
        {
            var map = Services.Map;
            float sea = Services.Config != null ? Services.Config.seaLevel : 0f;
            float height = map != null ? map.SampleHeight(p.x, p.y) : 0f;
            if (height > sea + 1f) return new Vector3(p.x, height, p.y);

            var roads = Services.Roads;
            if (roads != null)
            {
                int segment = roads.NearestSegment(p, 2000f);
                if (segment >= 0) return roads.SidewalkPoint(segment, true, 0.5f);
            }
            return new Vector3(p.x, sea + 1f, p.y);
        }

        // ------------------------------------------------------------------
        public IEnumerable<MissionDefinition> AvailableMissions()
        {
            foreach (var m in Story)
                if (!Completed.Contains(m.Id) && PrerequisitesMet(m)) yield return m;
            foreach (var m in SideMissions)
                if (m.RepeatableAfterCompletion || !Completed.Contains(m.Id)) yield return m;
        }

        private bool PrerequisitesMet(MissionDefinition m)
        {
            if (m.Prerequisites == null) return true;
            foreach (var p in m.Prerequisites) if (!Completed.Contains(p)) return false;
            return true;
        }

        public void RefreshAvailability()
        {
            var landmarks = Services.Landmarks;
            if (landmarks == null) return;
            foreach (var blip in _blips) landmarks.RemoveDynamic(blip);
            _blips.Clear();
            _resolvedStarts.Clear();

            foreach (var m in AvailableMissions())
            {
                Vector3 start = ResolveAnchor(m.StartAnchor);
                _resolvedStarts[m.Id] = start;
                var blip = landmarks.AddDynamic(BlipKind.MissionGiver, start, m.Title,
                    m.Kind == MissionKind.Story ? new Color(1f, 0.85f, 0.25f) : new Color(0.45f, 0.85f, 1f));
                _blips.Add(blip);
            }
        }

        /// <summary>Called when the player presses interact - starts a mission if standing on its marker.</summary>
        public bool TryInteract(Vector3 position)
        {
            if (Active != null) return false;
            foreach (var kv in _resolvedStarts)
            {
                if ((kv.Value - position).sqrMagnitude > 36f) continue;
                var mission = FindMission(kv.Key);
                if (mission == null) continue;
                StartMission(mission);
                return true;
            }
            return false;
        }

        public MissionDefinition FindMission(string id)
        {
            foreach (var m in Story) if (m.Id == id) return m;
            foreach (var m in SideMissions) if (m.Id == id) return m;
            return null;
        }

        // ------------------------------------------------------------------
        public void StartMission(MissionDefinition mission)
        {
            if (mission == null || Active != null) return;
            Active = mission;
            ObjectiveIndex = -1;
            _failing = false;
            _killCount = 0;
            _destroyedCount = 0;

            GameEvents.RaiseMissionStarted(mission.Id);
            GameEvents.Notify("Mission started: " + mission.Title, 3f);
            if (mission.TurnsHostile != Faction.Civilian) MarkFactionHostile(mission.TurnsHostile);

            if (mission.IntroLines != null && mission.IntroLines.Length > 0)
                Services.Dialogue?.PlaySequence(mission.IntroLines, () => AdvanceObjective());
            else AdvanceObjective();
        }

        public void AbandonMission(bool silent = false)
        {
            if (Active == null) return;
            CleanupSpawned();
            if (!silent) GameEvents.Notify("Mission abandoned", 2.5f);
            GameEvents.RaiseMissionEnded(Active.Id, false);
            Active = null;
            CurrentObjectiveText = null;
            HasObjectiveMarker = false;
            ClearObjectiveBlip();
            RefreshAvailability();
        }

        private void CompleteMission()
        {
            var mission = Active;
            if (mission == null) return;

            CleanupSpawned(keepRewards: true);
            Completed.Add(mission.Id);
            Respect += mission.RewardRespect;
            if (mission.RewardCash > 0) Services.Economy?.AddMoney(mission.RewardCash, "Mission: " + mission.Title);
            if (mission.Kind == MissionKind.Story) Chapter = Mathf.Max(Chapter, mission.Chapter + 1);

            Active = null;
            CurrentObjectiveText = null;
            HasObjectiveMarker = false;
            ClearObjectiveBlip();

            GameEvents.RaiseMissionEnded(mission.Id, true);
            GameEvents.Notify("Mission complete: " + mission.Title + "  +$" + mission.RewardCash.ToString("N0"), 4f);

            if (mission.OutroLines != null && mission.OutroLines.Length > 0)
                Services.Dialogue?.PlaySequence(mission.OutroLines, null);

            RefreshAvailability();
            Services.Save?.AutoSave();
        }

        public void FailMission(string reason)
        {
            if (Active == null || _failing) return;
            _failing = true;
            var mission = Active;
            CleanupSpawned();
            Active = null;
            CurrentObjectiveText = null;
            HasObjectiveMarker = false;
            ClearObjectiveBlip();
            GameEvents.RaiseMissionEnded(mission.Id, false);
            GameEvents.Notify("Mission failed: " + reason, 4f);
            RefreshAvailability();
        }

        private void OnPlayerDied()
        {
            if (Active != null) FailMission("You died");
        }

        // ------------------------------------------------------------------
        private void AdvanceObjective()
        {
            if (Active == null) return;
            ObjectiveIndex++;
            if (ObjectiveIndex >= Active.Objectives.Count) { CompleteMission(); return; }

            var objective = Active.Objectives[ObjectiveIndex];
            CurrentObjectiveText = objective.Description;
            _objectiveTimer = objective.TimeLimit;
            _surviveTimer = objective.SurviveSeconds;
            _waveTimer = 0f;
            _killCount = 0;
            _destroyedCount = 0;

            Vector3 target = ResolveAnchor(objective.Anchor);
            CurrentObjectivePosition = target;
            HasObjectiveMarker = objective.ShowMarker && objective.Type != ObjectiveType.LoseWanted && objective.Type != ObjectiveType.Wait;

            SetObjectiveBlip(target, objective.Description);

            if (objective.WantedLevelOnStart > 0)
                Services.Wanted?.SetLevelDirect(Mathf.Max(Services.Wanted.Level, objective.WantedLevelOnStart));

            switch (objective.Type)
            {
                case ObjectiveType.KillAll:
                case ObjectiveType.KillTarget:
                    SpawnEnemies(objective, target);
                    break;
                case ObjectiveType.StealVehicle:
                case ObjectiveType.EnterVehicle:
                    SpawnMissionVehicle(objective, target);
                    break;
                case ObjectiveType.DestroyVehicle:
                    SpawnTargetVehicles(objective, target);
                    break;
                case ObjectiveType.Collect:
                    SpawnPickup(target);
                    break;
                case ObjectiveType.Protect:
                    SpawnProtectTarget(objective, target);
                    break;
                case ObjectiveType.Survive:
                    _waveTimer = 2f;
                    break;
            }

            if (objective.Lines != null && objective.Lines.Length > 0)
                Services.Dialogue?.PlaySequence(objective.Lines, null);
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            if (Active == null) return;
            var objective = Active.Objectives[Mathf.Clamp(ObjectiveIndex, 0, Active.Objectives.Count - 1)];
            float dt = Time.deltaTime;

            if (objective.TimeLimit > 0f)
            {
                _objectiveTimer -= dt;
                ObjectiveTimeRemaining = Mathf.Max(0f, _objectiveTimer);
                if (_objectiveTimer <= 0f) { FailMission("Out of time"); return; }
            }
            else ObjectiveTimeRemaining = 0f;

            var player = Services.Player;
            if (player == null) return;

            switch (objective.Type)
            {
                case ObjectiveType.GoTo:
                case ObjectiveType.Deliver:
                case ObjectiveType.Escape:
                case ObjectiveType.Race:
                    if (Reached(player, objective, false)) AdvanceObjective();
                    break;

                case ObjectiveType.GoToInVehicle:
                    if (Reached(player, objective, true)) AdvanceObjective();
                    break;

                case ObjectiveType.EnterVehicle:
                    if (player.InVehicle && (_missionVehicle == null || player.CurrentVehicle == _missionVehicle)) AdvanceObjective();
                    break;

                case ObjectiveType.ExitVehicle:
                    if (!player.InVehicle) AdvanceObjective();
                    break;

                case ObjectiveType.StealVehicle:
                    if (player.InVehicle && player.CurrentVehicle == _missionVehicle)
                    {
                        _missionVehicle.IsMissionVehicle = true;
                        AdvanceObjective();
                    }
                    else if (_missionVehicle != null && _missionVehicle.IsDestroyed) FailMission("The vehicle was destroyed");
                    break;

                case ObjectiveType.DeliverVehicle:
                    if (_missionVehicle != null && _missionVehicle.IsDestroyed) { FailMission("The vehicle was destroyed"); break; }
                    if (player.InVehicle && Vector3.Distance(player.transform.position, CurrentObjectivePosition) < Mathf.Max(10f, objective.Radius))
                        AdvanceObjective();
                    break;

                case ObjectiveType.KillTarget:
                    if (_killTarget == null || _killTarget.Health == null || !_killTarget.Health.IsAlive) AdvanceObjective();
                    break;

                case ObjectiveType.KillAll:
                    if (AllEnemiesDown()) AdvanceObjective();
                    break;

                case ObjectiveType.DestroyVehicle:
                    if (_destroyedCount >= Mathf.Max(1, objective.Count)) AdvanceObjective();
                    else UpdateDestroyTargets(objective);
                    break;

                case ObjectiveType.Collect:
                    if (_pickup == null) AdvanceObjective();
                    else if (Vector3.Distance(player.transform.position, _pickup.transform.position) < 2.6f)
                    {
                        Destroy(_pickup);
                        _pickup = null;
                        Services.Audio?.PlayUi("pickup");
                        AdvanceObjective();
                    }
                    break;

                case ObjectiveType.Survive:
                    _surviveTimer -= dt;
                    ObjectiveTimeRemaining = Mathf.Max(0f, _surviveTimer);
                    SpawnWaves(objective, dt);
                    if (_surviveTimer <= 0f) AdvanceObjective();
                    break;

                case ObjectiveType.Protect:
                    if (_protectTarget == null || _protectTarget.Health == null || !_protectTarget.Health.IsAlive)
                    { FailMission("Your client was killed"); break; }
                    _surviveTimer -= dt;
                    ObjectiveTimeRemaining = Mathf.Max(0f, _surviveTimer);
                    SpawnWaves(objective, dt);
                    if (_surviveTimer <= 0f) AdvanceObjective();
                    break;

                case ObjectiveType.LoseWanted:
                    if (Services.Wanted == null || Services.Wanted.Level == 0) AdvanceObjective();
                    break;

                case ObjectiveType.Wait:
                    _surviveTimer -= dt;
                    if (_surviveTimer <= 0f) AdvanceObjective();
                    break;

                case ObjectiveType.Follow:
                    if (Reached(player, objective, false)) AdvanceObjective();
                    break;
            }

            if (_objectiveBlip != null) _objectiveBlip.Position = CurrentObjectivePosition;
        }

        private bool Reached(SanMonica.Players.PlayerController player, MissionObjective objective, bool requireVehicle)
        {
            if (requireVehicle && !player.InVehicle) return false;
            float radius = Mathf.Max(4f, objective.Radius);
            return Vector3.Distance(player.transform.position, CurrentObjectivePosition) < radius;
        }

        // ------------------------------------------------------------------
        private void SpawnEnemies(MissionObjective objective, Vector3 centre)
        {
            var db = Services.Database;
            var peds = Services.Peds;
            if (db == null || peds == null) return;

            int count = objective.Type == ObjectiveType.KillTarget ? 1 : Mathf.Max(1, objective.EnemyCount);
            var archetype = db.Ped(string.IsNullOrEmpty(objective.PedArchetype) ? "serrano" : objective.PedArchetype);
            if (archetype == null) return;

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = _rng.InsideUnitCircle() * Mathf.Max(6f, objective.Radius);
                Vector3 point = centre + new Vector3(offset.x, 0f, offset.y);
                if (Services.Map != null) point.y = Services.Map.SampleHeight(point.x, point.z) + 0.2f;

                var brain = peds.Spawn(archetype, point, Quaternion.Euler(0f, _rng.Value * 360f, 0f));
                if (brain == null) continue;
                MarkFactionHostile(objective.EnemyFaction);
                brain.Faction = objective.EnemyFaction;
                if (Services.Player != null) brain.SetThreat(Services.Player.transform);
                else brain.EnterState(PedState.Combat);
                _spawnedPeds.Add(brain);
                if (objective.Type == ObjectiveType.KillTarget && _killTarget == null)
                {
                    _killTarget = brain;
                    if (brain.Health != null)
                    {
                        brain.Health.MaxHealth *= 2.2f;
                        brain.Health.Health = brain.Health.MaxHealth;
                    }
                }
            }
        }

        private void SpawnWaves(MissionObjective objective, float dt)
        {
            _waveTimer -= dt;
            if (_waveTimer > 0f) return;
            _waveTimer = 9f;

            int alive = 0;
            foreach (var p in _spawnedPeds) if (p != null && p.Health != null && p.Health.IsAlive) alive++;
            if (alive >= Mathf.Max(2, objective.EnemyCount / 2)) return;

            var wave = new MissionObjective
            {
                Type = ObjectiveType.KillAll,
                EnemyCount = Mathf.Max(2, objective.EnemyCount / 3),
                PedArchetype = objective.PedArchetype,
                EnemyFaction = objective.EnemyFaction,
                Radius = 30f
            };
            Vector3 origin = Services.PlayerPosition + new Vector3(_rng.Range(-40f, 40f), 0f, _rng.Range(-40f, 40f));
            SpawnEnemies(wave, origin);
        }

        private bool AllEnemiesDown()
        {
            for (int i = 0; i < _spawnedPeds.Count; i++)
            {
                var p = _spawnedPeds[i];
                if (p != null && p.Health != null && p.Health.IsAlive) return false;
            }
            return _spawnedPeds.Count > 0;
        }

        private void SpawnMissionVehicle(MissionObjective objective, Vector3 position)
        {
            var factory = Services.Vehicles;
            var db = Services.Database;
            if (factory == null || db == null) return;
            var def = db.Vehicle(string.IsNullOrEmpty(objective.VehicleId) ? "meridian" : objective.VehicleId);
            if (def == null) return;

            Vector3 spawn = position;
            if (def.IsWatercraft)
            {
                spawn.y = 0.4f;
            }
            else if (Services.Map != null)
            {
                spawn = Services.Map.FindGroundPoint(position, 60f);
                spawn.y += def.wheelRadius + 0.15f;
            }

            _missionVehicle = factory.Spawn(def, spawn, Quaternion.Euler(0f, _rng.Value * 360f, 0f));
            if (_missionVehicle != null)
            {
                _missionVehicle.IsMissionVehicle = true;
                _missionVehicle.HasOwner = false;
                _spawnedVehicles.Add(_missionVehicle);
                CurrentObjectivePosition = _missionVehicle.transform.position;
                SetObjectiveBlip(CurrentObjectivePosition, objective.Description);
            }
        }

        private void SpawnTargetVehicles(MissionObjective objective, Vector3 centre)
        {
            var factory = Services.Vehicles;
            var db = Services.Database;
            if (factory == null || db == null) return;
            var def = db.Vehicle(string.IsNullOrEmpty(objective.VehicleId) ? "cartel-runner" : objective.VehicleId);
            if (def == null) return;

            for (int i = 0; i < Mathf.Max(1, objective.Count); i++)
            {
                Vector2 offset = _rng.InsideUnitCircle() * 30f;
                Vector3 point = centre + new Vector3(offset.x, 0f, offset.y);
                if (Services.Map != null) point = Services.Map.FindGroundPoint(point, 60f);
                point.y += def.wheelRadius + 0.2f;
                var vehicle = factory.Spawn(def, point, Quaternion.Euler(0f, _rng.Value * 360f, 0f));
                if (vehicle == null) continue;
                vehicle.IsMissionVehicle = true;
                _spawnedVehicles.Add(vehicle);
                Services.Traffic?.AttachDriver(vehicle, false, null);
            }
        }

        private void UpdateDestroyTargets(MissionObjective objective)
        {
            int destroyed = 0;
            for (int i = 0; i < _spawnedVehicles.Count; i++)
            {
                var v = _spawnedVehicles[i];
                if (v == null || v.IsDestroyed) destroyed++;
            }
            _destroyedCount = destroyed;
        }

        private void SpawnPickup(Vector3 position)
        {
            var go = new GameObject("MissionPickup");
            if (Services.Map != null) position.y = Services.Map.SampleHeight(position.x, position.z);
            go.transform.position = position + Vector3.up * 0.8f;

            var mb = new MeshBuilder(1);
            mb.AddBox(Vector3.zero, new Vector3(0.45f, 0.32f, 0.28f), Quaternion.identity, 0f, 0);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mb.ToMesh("Pickup");
            mr.sharedMaterial = MaterialLibrary.Emissive(new Color(1f, 0.85f, 0.25f), 2.4f);
            go.AddComponent<MissionPickupSpin>();

            _pickup = go;
            _spawnedProps.Add(go);
            CurrentObjectivePosition = go.transform.position;
            SetObjectiveBlip(CurrentObjectivePosition, "Pick it up");
        }

        private void SpawnProtectTarget(MissionObjective objective, Vector3 position)
        {
            var db = Services.Database;
            var peds = Services.Peds;
            if (db == null || peds == null) return;
            var archetype = db.Ped("citizen");
            if (archetype == null) return;

            if (Services.Map != null) position.y = Services.Map.SampleHeight(position.x, position.z) + 0.2f;
            _protectTarget = peds.Spawn(archetype, position, Quaternion.identity);
            if (_protectTarget != null)
            {
                _spawnedPeds.Add(_protectTarget);
                if (_protectTarget.Health != null)
                {
                    _protectTarget.Health.MaxHealth = 160f;
                    _protectTarget.Health.Health = 160f;
                }
                Services.Landmarks?.AddDynamic(BlipKind.Mission, position, "Client", new Color(0.3f, 1f, 0.5f));
            }
            _waveTimer = 3f;
        }

        // ------------------------------------------------------------------
        public void NotifyKillOrHit(GameObject victim, bool killed)
        {
            if (Active == null || !killed) return;
            for (int i = 0; i < _spawnedPeds.Count; i++)
                if (_spawnedPeds[i] != null && _spawnedPeds[i].gameObject == victim) { _killCount++; return; }
        }

        private void SetObjectiveBlip(Vector3 position, string label)
        {
            var landmarks = Services.Landmarks;
            if (landmarks == null) return;
            ClearObjectiveBlip();
            _objectiveBlip = landmarks.AddDynamic(BlipKind.Mission, position, label, new Color(1f, 0.9f, 0.3f));
        }

        private void ClearObjectiveBlip()
        {
            if (_objectiveBlip != null)
            {
                Services.Landmarks?.RemoveDynamic(_objectiveBlip);
                _objectiveBlip = null;
            }
        }

        private void CleanupSpawned(bool keepRewards = false)
        {
            foreach (var ped in _spawnedPeds)
                if (ped != null) Services.Peds?.Despawn(ped);
            _spawnedPeds.Clear();

            foreach (var v in _spawnedVehicles)
            {
                if (v == null) continue;
                if (keepRewards && Services.Player != null && Services.Player.CurrentVehicle == v)
                {
                    v.IsMissionVehicle = false;
                    v.HasOwner = false;
                    continue;
                }
                Services.Vehicles?.Despawn(v);
            }
            _spawnedVehicles.Clear();

            foreach (var p in _spawnedProps) if (p != null) Destroy(p);
            _spawnedProps.Clear();

            Services.Landmarks?.ClearDynamic(BlipKind.Mission);
            _protectTarget = null;
            _killTarget = null;
            _missionVehicle = null;
            _pickup = null;
        }

        // ------------------------------------------------------------------
        public MissionSaveState CaptureState()
        {
            return new MissionSaveState
            {
                completed = new List<string>(Completed),
                chapter = Chapter,
                respect = Respect,
                hostileFactions = new List<int>(FactionInts())
            };
        }

        private IEnumerable<int> FactionInts()
        {
            foreach (var f in _hostileFactions) yield return (int)f;
        }

        public void RestoreState(MissionSaveState state)
        {
            if (state == null) return;
            Completed.Clear();
            if (state.completed != null) foreach (var id in state.completed) Completed.Add(id);
            Chapter = Mathf.Max(1, state.chapter);
            Respect = state.respect;
            _hostileFactions.Clear();
            if (state.hostileFactions != null) foreach (var f in state.hostileFactions) _hostileFactions.Add((Faction)f);
            RefreshAvailability();
        }
    }

    [System.Serializable]
    public class MissionSaveState
    {
        public List<string> completed = new List<string>();
        public int chapter = 1;
        public int respect;
        public List<int> hostileFactions = new List<int>();
    }

    public class MissionPickupSpin : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            transform.position += Vector3.up * Mathf.Sin(Time.time * 2f) * 0.0025f;
        }
    }
}
