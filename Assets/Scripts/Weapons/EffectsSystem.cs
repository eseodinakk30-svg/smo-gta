using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Utils;

namespace SanMonica.Weapons
{
    /// <summary>
    /// Pooled visual effects: muzzle flashes, impacts, blood, smoke, sparks,
    /// explosions, tracers and decals. Everything is recycled so combat never
    /// allocates during play.
    /// </summary>
    public class EffectsSystem : MonoBehaviour
    {
        [Header("Budget")]
        public int MaxDecals = 60;
        public bool ParticlesEnabled = true;
        public float EffectScale = 1f;

        private Transform _root;
        private PoolRegistry _pools;
        private readonly Queue<GameObject> _decals = new Queue<GameObject>();
        private Mesh _quadMesh;
        private Mesh _tracerMesh;

        public void Initialize()
        {
            _root = new GameObject("Effects").transform;
            _root.SetParent(transform, false);
            _pools = new PoolRegistry(_root);
            _quadMesh = BuildQuad();
            _tracerMesh = BuildTracer();
        }

        private static Mesh BuildQuad()
        {
            var mb = new MeshBuilder(1);
            mb.AddQuad(new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                       new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), Vector2.one, 0);
            return mb.ToMesh("EffectQuad");
        }

        private static Mesh BuildTracer()
        {
            var mb = new MeshBuilder(1);
            mb.AddBox(Vector3.zero, new Vector3(0.03f, 0.03f, 1f), Quaternion.identity, 0f, 0);
            return mb.ToMesh("Tracer");
        }

        // ------------------------------------------------------------------
        private ParticleSystem CreateParticle(string key, Color colour, float size, float lifetime, int burst, float speed, bool additive, bool gravity)
        {
            var go = new GameObject("FX_" + key);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = colour;
            main.gravityModifier = gravity ? 0.6f : 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(8, burst * 2);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialLibrary.Particle(colour, additive);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var size3 = ps.sizeOverLifetime;
            size3.enabled = true;
            size3.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            go.AddComponent<AutoDespawn>();
            return ps;
        }

        private GameObjectPool ParticlePool(string key, Color colour, float size, float lifetime, int burst, float speed, bool additive, bool gravity, int limit = 24)
        {
            return _pools.GetOrCreate(key, () => CreateParticle(key, colour, size, lifetime, burst, speed, additive, gravity).gameObject, 0, limit);
        }

        private void Emit(string key, Vector3 position, Quaternion rotation, Color colour, float size, float lifetime, int burst, float speed, bool additive, bool gravity, float lifeSeconds = 1.6f)
        {
            if (!ParticlesEnabled) return;
            var pool = ParticlePool(key, colour, size, lifetime, burst, speed, additive, gravity);
            var go = pool.Spawn(position, rotation);
            if (go == null) return;
            var auto = go.GetComponent<AutoDespawn>();
            if (auto != null) auto.Begin(pool, lifeSeconds);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) { ps.Clear(); ps.Play(); }
        }

        // ------------------------------------------------------------------
        public void SpawnMuzzleFlash(Vector3 position, Vector3 direction)
        {
            Emit("muzzle", position, Quaternion.LookRotation(direction), new Color(1f, 0.85f, 0.45f),
                0.28f * EffectScale, 0.06f, 6, 7f, true, false, 0.3f);
        }

        public void SpawnImpact(Vector3 position, Vector3 normal, bool metallic)
        {
            Emit(metallic ? "spark" : "dust", position, Quaternion.LookRotation(normal),
                metallic ? new Color(1f, 0.82f, 0.35f) : new Color(0.66f, 0.62f, 0.56f),
                metallic ? 0.06f : 0.18f, metallic ? 0.35f : 0.6f, metallic ? 10 : 8,
                metallic ? 6f : 2.2f, metallic, true, 1.2f);
            SpawnDecal(position, normal);
        }

        public void SpawnBlood(Vector3 position, Vector3 direction)
        {
            Emit("blood", position, Quaternion.LookRotation(direction), new Color(0.55f, 0.05f, 0.05f),
                0.13f * EffectScale, 0.55f, 9, 3.4f, false, true, 1.2f);
        }

        public void SpawnSmoke(Vector3 position, float intensity)
        {
            Emit("smoke", position, Quaternion.LookRotation(Vector3.up), new Color(0.22f, 0.22f, 0.24f, 0.7f),
                Mathf.Lerp(0.5f, 1.6f, intensity) * EffectScale, 2.2f, 4, 1.4f, false, false, 3f);
        }

        public void SpawnExplosion(Vector3 position, float radius)
        {
            Emit("explosion_core", position, Quaternion.identity, new Color(1f, 0.65f, 0.2f),
                radius * 0.55f * EffectScale, 0.7f, 14, 9f, true, false, 1.6f);
            Emit("explosion_smoke", position, Quaternion.LookRotation(Vector3.up), new Color(0.16f, 0.15f, 0.15f, 0.85f),
                radius * 0.7f * EffectScale, 2.6f, 10, 3.2f, false, false, 3.4f);

            var light = _pools.GetOrCreate("explosion_light", () =>
            {
                var go = new GameObject("ExplosionLight");
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.62f, 0.25f);
                l.range = 26f;
                l.intensity = 8f;
                l.shadows = LightShadows.None;
                go.AddComponent<AutoDespawn>();
                return go;
            }, 0, 6);
            var lightGo = light.Spawn(position, Quaternion.identity);
            if (lightGo != null) lightGo.GetComponent<AutoDespawn>()?.Begin(light, 0.6f);

            Services.Camera?.Shake(Mathf.Clamp01(radius / 12f), 0.6f);
        }

        public void SpawnTracer(Vector3 from, Vector3 to)
        {
            if (!ParticlesEnabled) return;
            var pool = _pools.GetOrCreate("tracer", () =>
            {
                var go = new GameObject("Tracer");
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = _tracerMesh;
                mr.sharedMaterial = MaterialLibrary.Unlit(new Color(1f, 0.9f, 0.55f, 0.85f), true);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<AutoDespawn>();
                return go;
            }, 0, 24);

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.6f) return;
            var go2 = pool.Spawn(from + delta * 0.5f, Quaternion.LookRotation(delta));
            if (go2 == null) return;
            go2.transform.localScale = new Vector3(1f, 1f, length);
            go2.GetComponent<AutoDespawn>()?.Begin(pool, 0.06f);
        }

        public void SpawnDecal(Vector3 position, Vector3 normal)
        {
            if (!ParticlesEnabled || MaxDecals <= 0) return;
            var go = new GameObject("Decal");
            go.transform.SetParent(_root, false);
            go.transform.position = position + normal * 0.012f;
            go.transform.rotation = Quaternion.LookRotation(-normal);
            go.transform.localScale = Vector3.one * Random.Range(0.10f, 0.18f);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = _quadMesh;
            mr.sharedMaterial = MaterialLibrary.Unlit(new Color(0.05f, 0.05f, 0.06f, 0.85f), true);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _decals.Enqueue(go);
            while (_decals.Count > MaxDecals)
            {
                var old = _decals.Dequeue();
                if (old != null) Destroy(old);
            }
        }

        public void ClearDecals()
        {
            while (_decals.Count > 0)
            {
                var d = _decals.Dequeue();
                if (d != null) Destroy(d);
            }
        }
    }

    /// <summary>Returns a pooled effect to its pool after a delay.</summary>
    public class AutoDespawn : MonoBehaviour
    {
        private GameObjectPool _pool;
        private float _timer;

        public void Begin(GameObjectPool pool, float seconds)
        {
            _pool = pool;
            _timer = seconds;
        }

        private void Update()
        {
            if (_pool == null) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                var pool = _pool;
                _pool = null;
                pool.Despawn(gameObject);
            }
        }
    }
}
