using System.Collections.Generic;
using UnityEngine;
using SanMonica.AI;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.World;

namespace SanMonica.Missions
{
    public enum AmbientEventKind { Mugging, GangFight, Breakdown, StreetRaceChallenge, PoliceChase, Crash, Deal }

    /// <summary>
    /// Ambient incidents that make the city feel like it is living without the
    /// player: muggings in alleys, gang shootouts in the Quarter, broken down
    /// drivers on the interstate, deals in the docks.
    /// </summary>
    public class RandomEventSystem : MonoBehaviour
    {
        [Header("Pacing")]
        public float MinInterval = 55f;
        public float MaxInterval = 140f;
        public int MaxConcurrent = 2;

        private float _timer;
        private Rng _rng;
        private readonly List<ActiveEvent> _active = new List<ActiveEvent>(4);

        private class ActiveEvent
        {
            public AmbientEventKind Kind;
            public float Lifetime;
            public List<PedBrain> Peds = new List<PedBrain>(6);
            public List<SanMonica.Vehicles.Vehicle> Vehicles = new List<SanMonica.Vehicles.Vehicle>(2);
            public MapBlip Blip;
        }

        public void Initialize()
        {
            _rng = new Rng((Services.Config != null ? Services.Config.seed : 7) ^ 0xE7E7);
            _timer = _rng.Range(MinInterval * 0.4f, MaxInterval * 0.6f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                e.Lifetime -= dt;
                bool tooFar = Vector3.Distance(Services.PlayerPosition, EventPosition(e)) > 340f;
                if (e.Lifetime <= 0f || tooFar) { Cleanup(e); _active.RemoveAt(i); }
            }

            if (Services.Missions != null && Services.Missions.Active != null) return;
            if (_active.Count >= MaxConcurrent) return;

            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _rng.Range(MinInterval, MaxInterval);
            TrySpawnEvent();
        }

        private Vector3 EventPosition(ActiveEvent e)
        {
            foreach (var p in e.Peds) if (p != null) return p.transform.position;
            foreach (var v in e.Vehicles) if (v != null) return v.transform.position;
            return Services.PlayerPosition;
        }

        private void TrySpawnEvent()
        {
            var map = Services.Map;
            var nav = Services.Nav;
            if (map == null || nav == null || !nav.Ready) return;

            Vector2 offset = _rng.InsideUnitCircle().normalized * _rng.Range(70f, 150f);
            Vector3 origin = Services.PlayerPosition + new Vector3(offset.x, 0f, offset.y);
            if (map.IsWater(origin.x, origin.z)) return;
            origin = nav.SnapToWalkable(origin, 60f);

            var district = map.DistrictAt(origin.x, origin.z);
            var profile = DistrictCatalog.Get(district);
            var kind = ChooseKind(district, profile);

            var e = new ActiveEvent { Kind = kind, Lifetime = 120f };
            switch (kind)
            {
                case AmbientEventKind.Mugging: SpawnMugging(e, origin); break;
                case AmbientEventKind.GangFight: SpawnGangFight(e, origin, district); break;
                case AmbientEventKind.Breakdown: SpawnBreakdown(e, origin); break;
                case AmbientEventKind.Deal: SpawnDeal(e, origin); break;
                case AmbientEventKind.Crash: SpawnCrash(e, origin); break;
                default: SpawnMugging(e, origin); break;
            }

            if (e.Peds.Count == 0 && e.Vehicles.Count == 0) return;
            e.Blip = Services.Landmarks?.AddDynamic(BlipKind.RandomEvent, origin, "Incident", new Color(1f, 0.55f, 0.2f));
            _active.Add(e);
        }

        private AmbientEventKind ChooseKind(DistrictType district, DistrictProfile profile)
        {
            float roll = _rng.Value;
            if (profile.crimeRate > 0.25f)
            {
                if (roll < 0.35f) return AmbientEventKind.GangFight;
                if (roll < 0.60f) return AmbientEventKind.Mugging;
                if (roll < 0.75f) return AmbientEventKind.Deal;
            }
            if (district == DistrictType.Highway || district == DistrictType.Suburb || district == DistrictType.Farmland)
                return roll < 0.5f ? AmbientEventKind.Breakdown : AmbientEventKind.Crash;
            if (roll < 0.3f) return AmbientEventKind.Mugging;
            if (roll < 0.55f) return AmbientEventKind.Breakdown;
            return AmbientEventKind.Deal;
        }

        // ------------------------------------------------------------------
        private void SpawnMugging(ActiveEvent e, Vector3 origin)
        {
            var db = Services.Database;
            var peds = Services.Peds;
            if (db == null || peds == null) return;

            var victim = peds.Spawn(db.Ped("citizen"), origin, Quaternion.identity);
            var mugger = peds.Spawn(db.Ped("mugger"), origin + new Vector3(1.6f, 0f, 0.8f), Quaternion.identity);
            if (victim != null) { e.Peds.Add(victim); victim.EnterState(PedState.Cower); }
            if (mugger != null)
            {
                e.Peds.Add(mugger);
                mugger.EnterState(PedState.Combat);
            }
            GameEvents.Notify("Something is happening nearby", 2f);
        }

        private void SpawnGangFight(ActiveEvent e, Vector3 origin, DistrictType district)
        {
            var db = Services.Database;
            var peds = Services.Peds;
            if (db == null || peds == null) return;

            string aId = district == DistrictType.Port ? "ironbay" : "serrano";
            string bId = district == DistrictType.Residential ? "callenueve" : "callenueve";

            for (int i = 0; i < 3; i++)
            {
                var a = peds.Spawn(db.Ped(aId), origin + new Vector3(_rng.Range(-4f, -1f), 0f, _rng.Range(-3f, 3f)), Quaternion.identity);
                var b = peds.Spawn(db.Ped(bId), origin + new Vector3(_rng.Range(1f, 4f), 0f, _rng.Range(-3f, 3f)), Quaternion.identity);
                if (a != null) { e.Peds.Add(a); a.EnterState(PedState.Combat); }
                if (b != null) { e.Peds.Add(b); b.EnterState(PedState.Combat); }
            }
            e.Lifetime = 90f;
        }

        private void SpawnBreakdown(ActiveEvent e, Vector3 origin)
        {
            var factory = Services.Vehicles;
            var db = Services.Database;
            var peds = Services.Peds;
            if (factory == null || db == null) return;

            var rngLocal = _rng;
            var district = Services.Map != null ? Services.Map.DistrictAt(origin.x, origin.z) : DistrictType.Suburb;
            var def = db.PickTrafficVehicle(ref rngLocal, district);
            _rng = rngLocal;
            if (def == null) return;

            var vehicle = factory.Spawn(def, origin + Vector3.up * 0.4f, Quaternion.Euler(0f, _rng.Value * 360f, 0f));
            if (vehicle != null)
            {
                vehicle.EngineRunning = false;
                vehicle.Health = def.maxHealth * 0.35f;
                e.Vehicles.Add(vehicle);
            }
            var owner = peds?.Spawn(db.Ped("citizen"), origin + new Vector3(2.2f, 0f, 0f), Quaternion.identity);
            if (owner != null) e.Peds.Add(owner);
        }

        private void SpawnDeal(ActiveEvent e, Vector3 origin)
        {
            var db = Services.Database;
            var peds = Services.Peds;
            if (db == null || peds == null) return;
            for (int i = 0; i < 3; i++)
            {
                var ped = peds.Spawn(db.Ped(i == 0 ? "serrano" : "ironbay"),
                    origin + new Vector3(Mathf.Cos(i * 2.1f) * 1.6f, 0f, Mathf.Sin(i * 2.1f) * 1.6f), Quaternion.identity);
                if (ped != null) { e.Peds.Add(ped); ped.EnterState(PedState.Talking); }
            }
            e.Lifetime = 80f;
        }

        private void SpawnCrash(ActiveEvent e, Vector3 origin)
        {
            var factory = Services.Vehicles;
            var db = Services.Database;
            if (factory == null || db == null) return;
            var rngLocal = _rng;
            var district = Services.Map != null ? Services.Map.DistrictAt(origin.x, origin.z) : DistrictType.Suburb;
            for (int i = 0; i < 2; i++)
            {
                var def = db.PickTrafficVehicle(ref rngLocal, district);
                if (def == null) continue;
                var v = factory.Spawn(def, origin + new Vector3(i * 3.4f, 0.4f, i * 1.2f), Quaternion.Euler(0f, 40f + i * 130f, 0f));
                if (v == null) continue;
                v.EngineRunning = false;
                v.Health = def.maxHealth * 0.18f;
                e.Vehicles.Add(v);
            }
            _rng = rngLocal;
            Services.Effects?.SpawnSmoke(origin + Vector3.up, 0.8f);
        }

        private void Cleanup(ActiveEvent e)
        {
            foreach (var p in e.Peds)
                if (p != null && p.Health != null && p.Health.IsAlive) Services.Peds?.Despawn(p);
            foreach (var v in e.Vehicles)
                if (v != null && !v.DriverIsPlayer) Services.Vehicles?.Despawn(v);
            if (e.Blip != null) Services.Landmarks?.RemoveDynamic(e.Blip);
        }
    }
}
