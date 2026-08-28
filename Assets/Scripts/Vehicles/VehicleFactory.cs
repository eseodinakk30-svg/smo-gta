using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;

namespace SanMonica.Vehicles
{
    /// <summary>
    /// Creates and recycles every vehicle in the world. Meshes are cached per
    /// (definition, paint) pair and instances are pooled, so traffic can churn
    /// continuously without allocating.
    /// </summary>
    public class VehicleFactory : MonoBehaviour
    {
        private readonly Dictionary<string, GameObjectPool> _pools = new Dictionary<string, GameObjectPool>();
        private readonly List<Vehicle> _active = new List<Vehicle>(128);
        private Transform _root;
        private GameDatabase _db;
        private int _spawnCounter;

        public IReadOnlyList<Vehicle> ActiveVehicles => _active;
        public int ActiveCount => _active.Count;

        public void Initialize(GameDatabase db)
        {
            _db = db;
            _root = new GameObject("Vehicles").transform;
            _root.SetParent(transform, false);
        }

        private GameObjectPool PoolFor(VehicleDefinition def, Color paint)
        {
            string key = def.id + "#" + Mathf.RoundToInt(paint.r * 15f) + Mathf.RoundToInt(paint.g * 15f) + Mathf.RoundToInt(paint.b * 15f);
            if (_pools.TryGetValue(key, out var pool)) return pool;

            var holder = new GameObject("Pool_" + key).transform;
            holder.SetParent(_root, false);
            int seed = key.GetHashCode();
            pool = new GameObjectPool(() =>
            {
                var go = new GameObject("Veh_" + def.id);
                var vehicle = go.AddComponent<Vehicle>();
                vehicle.Construct(def, paint, seed);
                return go;
            }, holder, 0, 24);
            _pools[key] = pool;
            return pool;
        }

        public Vehicle Spawn(VehicleDefinition def, Vector3 position, Quaternion rotation, Color? paint = null)
        {
            if (def == null) return null;
            Color colour = paint ?? PickPaint(def);
            var pool = PoolFor(def, colour);
            var go = pool.Spawn(position, rotation);
            if (go == null) return null;
            go.transform.SetParent(_root, true);
            var vehicle = go.GetComponent<Vehicle>();
            if (vehicle == null) return null;
            vehicle.OnSpawned();
            if (!_active.Contains(vehicle)) _active.Add(vehicle);
            _spawnCounter++;
            return vehicle;
        }

        public Vehicle SpawnById(string id, Vector3 position, Quaternion rotation, Color? paint = null)
            => Spawn(_db != null ? _db.Vehicle(id) : null, position, rotation, paint);

        public void Despawn(Vehicle vehicle)
        {
            if (vehicle == null) return;
            _active.Remove(vehicle);
            string key = KeyOf(vehicle);
            if (key != null && _pools.TryGetValue(key, out var pool)) pool.Despawn(vehicle.gameObject);
            else Destroy(vehicle.gameObject);
        }

        private string KeyOf(Vehicle v)
        {
            if (v.Definition == null) return null;
            var p = v.Paint;
            return v.Definition.id + "#" + Mathf.RoundToInt(p.r * 15f) + Mathf.RoundToInt(p.g * 15f) + Mathf.RoundToInt(p.b * 15f);
        }

        public Color PickPaint(VehicleDefinition def)
        {
            var rng = new Rng(_spawnCounter * 7919 + def.id.GetHashCode());
            if (def.paintOptions != null && def.paintOptions.Length > 0)
            {
                var c = rng.Pick(def.paintOptions);
                float v = rng.Range(0.92f, 1.08f);
                return new Color(Mathf.Clamp01(c.r * v), Mathf.Clamp01(c.g * v), Mathf.Clamp01(c.b * v));
            }
            return new Color(rng.Range(0.15f, 0.9f), rng.Range(0.15f, 0.9f), rng.Range(0.15f, 0.9f));
        }

        /// <summary>Spawns a vehicle sitting still in a parking bay, engine off.</summary>
        public Vehicle SpawnParked(Vector3 position, float yaw, DistrictType district, ref Rng rng)
        {
            var def = _db.PickParkedVehicle(ref rng, district);
            if (def == null) return null;
            var v = Spawn(def, position + Vector3.up * (def.wheelRadius + 0.05f), Quaternion.Euler(0f, yaw, 0f));
            if (v != null)
            {
                v.EngineRunning = false;
                v.HasOwner = true;
            }
            return v;
        }

        public void DespawnAll()
        {
            var copy = new List<Vehicle>(_active);
            foreach (var v in copy) Despawn(v);
            _active.Clear();
        }

        public void TrimPools(int keepPerPool)
        {
            foreach (var kv in _pools) kv.Value.Trim(keepPerPool);
        }

        /// <summary>Nearest vehicle to a point, used by AI and missions.</summary>
        public Vehicle NearestVehicle(Vector3 position, float maxDistance, bool requireEmpty = false)
        {
            Vehicle best = null;
            float bestD = maxDistance * maxDistance;
            for (int i = 0; i < _active.Count; i++)
            {
                var v = _active[i];
                if (v == null || v.IsDestroyed) continue;
                if (requireEmpty && v.HasDriver) continue;
                float d = (v.transform.position - position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        private void LateUpdate()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i] == null) _active.RemoveAt(i);
        }
    }
}
