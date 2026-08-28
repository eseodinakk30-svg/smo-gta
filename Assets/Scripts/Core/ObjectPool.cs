using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Core
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Generic GameObject pool. Every transient object in San Monica - bullets,
    /// impact effects, pedestrians, traffic cars, props - is recycled through here
    /// so that mobile GC pressure stays near zero during play.
    /// </summary>
    public class GameObjectPool
    {
        private readonly Stack<GameObject> _idle = new Stack<GameObject>();
        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();
        private readonly System.Func<GameObject> _factory;
        private readonly Transform _root;
        private readonly int _hardLimit;

        public int ActiveCount => _active.Count;
        public int IdleCount => _idle.Count;
        public int TotalCount => ActiveCount + IdleCount;

        public GameObjectPool(System.Func<GameObject> factory, Transform root, int prewarm = 0, int hardLimit = 512)
        {
            _factory = factory;
            _root = root;
            _hardLimit = hardLimit;
            for (int i = 0; i < prewarm; i++)
            {
                var go = Create();
                if (go == null) break;
                go.SetActive(false);
                _idle.Push(go);
            }
        }

        private GameObject Create()
        {
            var go = _factory();
            if (go == null) return null;
            if (_root != null) go.transform.SetParent(_root, false);
            return go;
        }

        public GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            GameObject go = null;
            while (_idle.Count > 0 && go == null) go = _idle.Pop();
            if (go == null)
            {
                if (TotalCount >= _hardLimit) return null;
                go = Create();
                if (go == null) return null;
            }
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            _active.Add(go);
            var p = go.GetComponent<IPoolable>();
            p?.OnSpawned();
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (go == null) return;
            if (!_active.Remove(go)) return;
            var p = go.GetComponent<IPoolable>();
            p?.OnDespawned();
            go.SetActive(false);
            if (_root != null && go.transform.parent != _root) go.transform.SetParent(_root, false);
            _idle.Push(go);
        }

        public void DespawnAll()
        {
            if (_active.Count == 0) return;
            var tmp = new List<GameObject>(_active);
            foreach (var go in tmp) Despawn(go);
        }

        /// <summary>Destroys idle instances above a watermark to release memory.</summary>
        public void Trim(int keep)
        {
            while (_idle.Count > keep)
            {
                var go = _idle.Pop();
                if (go != null) Object.Destroy(go);
            }
        }

        public IEnumerable<GameObject> ActiveObjects => _active;
    }

    /// <summary>Named registry of pools so any system can reuse the same recycled objects.</summary>
    public class PoolRegistry
    {
        private readonly Dictionary<string, GameObjectPool> _pools = new Dictionary<string, GameObjectPool>();
        private readonly Transform _root;

        public PoolRegistry(Transform root) { _root = root; }

        public GameObjectPool GetOrCreate(string key, System.Func<GameObject> factory, int prewarm = 0, int hardLimit = 512)
        {
            if (_pools.TryGetValue(key, out var pool)) return pool;
            var holder = new GameObject("Pool_" + key).transform;
            holder.SetParent(_root, false);
            pool = new GameObjectPool(factory, holder, prewarm, hardLimit);
            _pools[key] = pool;
            return pool;
        }

        public bool TryGet(string key, out GameObjectPool pool) => _pools.TryGetValue(key, out pool);

        public void DespawnAll()
        {
            foreach (var kv in _pools) kv.Value.DespawnAll();
        }

        public void TrimAll(int keep)
        {
            foreach (var kv in _pools) kv.Value.Trim(keep);
        }

        public int TotalActive
        {
            get
            {
                int n = 0;
                foreach (var kv in _pools) n += kv.Value.ActiveCount;
                return n;
            }
        }
    }
}
