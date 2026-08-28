using System.Collections.Generic;
using UnityEngine;
using SanMonica.AI;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Vehicles;

namespace SanMonica.Characters
{
    /// <summary>
    /// Builds and recycles pedestrians. Each pooled instance keeps the body it
    /// was born with, and variety comes from having several instances per
    /// archetype, so no mesh is ever rebuilt during play.
    /// </summary>
    public class PedFactory : MonoBehaviour
    {
        private readonly Dictionary<string, GameObjectPool> _pools = new Dictionary<string, GameObjectPool>();
        private readonly List<PedBrain> _active = new List<PedBrain>(128);
        private readonly Dictionary<Vehicle, List<PedBrain>> _occupants = new Dictionary<Vehicle, List<PedBrain>>();
        private Transform _root;
        private GameDatabase _db;
        private int _counter;

        public IReadOnlyList<PedBrain> ActivePeds => _active;
        public int ActiveCount => _active.Count;

        public void Initialize(GameDatabase db)
        {
            _db = db;
            _root = new GameObject("Pedestrians").transform;
            _root.SetParent(transform, false);
        }

        private GameObjectPool PoolFor(PedArchetype archetype)
        {
            if (_pools.TryGetValue(archetype.id, out var pool)) return pool;
            var holder = new GameObject("Pool_Ped_" + archetype.id).transform;
            holder.SetParent(_root, false);
            pool = new GameObjectPool(() => CreateInstance(archetype), holder, 0, 40);
            _pools[archetype.id] = pool;
            return pool;
        }

        private GameObject CreateInstance(PedArchetype archetype)
        {
            var rng = new Rng(archetype.id.GetHashCode() ^ (_counter++ * 7919));
            var go = new GameObject("Ped_" + archetype.id);
            go.layer = GameLayers.Ped;

            var appearance = CharacterAppearance.Random(ref rng, archetype);

            var controller = go.AddComponent<CharacterController>();
            controller.height = appearance.Height * 0.96f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.42f;
            controller.skinWidth = 0.03f;

            var rig = CharacterRigBuilder.Build(go, appearance);

            var animator = go.AddComponent<ProceduralAnimator>();
            animator.Bind(rig);

            var health = go.AddComponent<CharacterHealth>();
            health.MaxHealth = archetype.maxHealth;
            health.MaxArmour = 150f;
            health.CanDrown = true;

            var ragdoll = RagdollBuilder.Build(rig, health, controller, null);
            health.Bind(ragdoll, animator);

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(rig.Bone(HumanBone.Head), false);
            eyes.localPosition = new Vector3(0f, 0.06f, 0.09f);

            var perception = go.AddComponent<AIPerception>();
            perception.Eyes = eyes;

            var weapons = go.AddComponent<SanMonica.Weapons.WeaponHolder>();
            weapons.Initialize(rig, animator, health, false);

            go.AddComponent<PedBrain>();
            return go;
        }

        public PedBrain Spawn(PedArchetype archetype, Vector3 position, Quaternion rotation)
        {
            if (archetype == null) return null;
            var pool = PoolFor(archetype);
            var go = pool.Spawn(position, rotation);
            if (go == null) return null;
            go.transform.SetParent(_root, true);

            var brain = go.GetComponent<PedBrain>();
            var rng = new Rng(_counter++ * 104729 + Mathf.RoundToInt(position.x + position.z));
            brain.Setup(archetype, ref rng);
            brain.SetLod(0);
            if (!_active.Contains(brain)) _active.Add(brain);
            return brain;
        }

        public PedBrain SpawnById(string archetypeId, Vector3 position, Quaternion rotation)
            => Spawn(_db != null ? _db.Ped(archetypeId) : null, position, rotation);

        public void Despawn(PedBrain brain)
        {
            if (brain == null) return;
            _active.Remove(brain);
            var archetype = brain.Archetype;
            if (archetype != null && _pools.TryGetValue(archetype.id, out var pool)) pool.Despawn(brain.gameObject);
            else Destroy(brain.gameObject);
        }

        // ------------------------------------------------------------------
        /// <summary>Puts a driver behind the wheel of a freshly spawned traffic car.</summary>
        public PedBrain SpawnDriverFor(Vehicle vehicle)
        {
            if (vehicle == null || _db == null) return null;
            var rng = new Rng(vehicle.GetInstanceID());
            int hour = Services.Clock != null ? Services.Clock.Hour : 12;
            var district = Services.Map != null ? Services.Map.DistrictAt(vehicle.transform.position) : DistrictType.Residential;
            var archetype = _db.PickPed(ref rng, district, hour);
            if (archetype == null) return null;

            var brain = Spawn(archetype, vehicle.transform.position + Vector3.up * 0.5f, vehicle.transform.rotation);
            if (brain == null) return null;
            brain.SeatInVehicle(vehicle, 0);

            if (!_occupants.TryGetValue(vehicle, out var list)) { list = new List<PedBrain>(2); _occupants[vehicle] = list; }
            list.Add(brain);
            return brain;
        }

        public void DespawnOccupants(Vehicle vehicle)
        {
            if (vehicle == null) return;
            if (!_occupants.TryGetValue(vehicle, out var list)) return;
            foreach (var brain in list)
            {
                if (brain == null) continue;
                brain.ForceExitVehicle();
                Despawn(brain);
            }
            list.Clear();
            _occupants.Remove(vehicle);
        }

        public void DespawnAll()
        {
            var copy = new List<PedBrain>(_active);
            foreach (var b in copy) Despawn(b);
            _active.Clear();
            _occupants.Clear();
        }

        public void TrimPools(int keepPerPool)
        {
            foreach (var kv in _pools) kv.Value.Trim(keepPerPool);
        }

        private void LateUpdate()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i] == null) _active.RemoveAt(i);
        }
    }
}
