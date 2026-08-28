using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    /// <summary>
    /// Halcyon Bay, the Redwater River and every lake share one animated water
    /// surface. A high detail sheet follows the camera for waves and reflections
    /// while a single flat quad covers the rest of the world.
    /// </summary>
    public class WaterSystem : MonoBehaviour
    {
        public float SeaLevel = 0f;
        public float WaveHeight = 0.32f;
        public float WaveSpeed = 0.7f;
        public float WaveScale = 0.045f;
        public bool WavesEnabled = true;

        private WorldMap _map;
        private WorldConfig _cfg;
        private Mesh _localMesh;
        private Vector3[] _baseVerts;
        private Vector3[] _verts;
        private Transform _localSheet;
        private Transform _farSheet;
        private MeshFilter _localFilter;
        private float _time;
        private const int Res = 40;
        private const float SheetSize = 1400f;

        public void Initialize(WorldConfig cfg, WorldMap map)
        {
            _cfg = cfg; _map = map;
            SeaLevel = cfg.seaLevel;
            BuildLocalSheet();
            BuildFarSheet();
        }

        private Material CreateWaterMaterial(bool local)
        {
            var m = MaterialLibrary.Transparent(new Color(0.10f, 0.28f, 0.36f, local ? 0.86f : 0.95f), 0.96f);
            return m;
        }

        private void BuildLocalSheet()
        {
            var go = new GameObject("WaterSheet");
            go.transform.SetParent(transform, false);
            _localSheet = go.transform;
            _localFilter = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateWaterMaterial(true);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var builder = new MeshBuilder(1);
            float step = SheetSize / Res;
            builder.AddGrid(new Vector3(-SheetSize * 0.5f, 0f, -SheetSize * 0.5f), SheetSize, SheetSize, Res, Res, null, 0.02f, 0);
            _localMesh = builder.ToMesh("WaterMesh");
            _localMesh.MarkDynamic();
            _localFilter.sharedMesh = _localMesh;
            _baseVerts = _localMesh.vertices;
            _verts = new Vector3[_baseVerts.Length];

            // Trigger volume used to detect swimming.
            var trig = new GameObject("WaterVolume");
            trig.transform.SetParent(go.transform, false);
            trig.layer = GameLayers.Water;
            var bc = trig.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(SheetSize, 60f, SheetSize);
            bc.center = new Vector3(0f, -30f, 0f);
        }

        private void BuildFarSheet()
        {
            var go = new GameObject("FarWater");
            go.transform.SetParent(transform, false);
            _farSheet = go.transform;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var builder = new MeshBuilder(1);
            float s = _cfg.worldSize * 1.4f;
            builder.AddQuad(new Vector3(-s * 0.5f, 0f, -s * 0.5f), new Vector3(s * 0.5f, 0f, -s * 0.5f),
                            new Vector3(s * 0.5f, 0f, s * 0.5f), new Vector3(-s * 0.5f, 0f, s * 0.5f),
                            Vector2.one * 60f, 0);
            mf.sharedMesh = builder.ToMesh("FarWaterMesh");
            mr.sharedMaterial = CreateWaterMaterial(false);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.position = new Vector3(0f, SeaLevel - 0.35f, 0f);
        }

        private void Update()
        {
            _time += Time.deltaTime * WaveSpeed;
            Vector3 p = Services.PlayerPosition;
            float snap = SheetSize / Res;
            Vector3 snapped = new Vector3(Mathf.Round(p.x / snap) * snap, SeaLevel, Mathf.Round(p.z / snap) * snap);
            _localSheet.position = snapped;

            if (!WavesEnabled || _baseVerts == null) return;

            for (int i = 0; i < _baseVerts.Length; i++)
            {
                Vector3 v = _baseVerts[i];
                float wx = v.x + snapped.x, wz = v.z + snapped.z;
                float h = Mathf.Sin(wx * WaveScale + _time) * 0.55f
                        + Mathf.Sin(wz * WaveScale * 1.37f + _time * 1.21f) * 0.35f
                        + Mathf.Sin((wx + wz) * WaveScale * 0.63f - _time * 0.78f) * 0.25f;
                v.y = h * WaveHeight;
                _verts[i] = v;
            }
            _localMesh.vertices = _verts;
            _localMesh.RecalculateNormals();
        }

        /// <summary>Water surface height at a position, or float.MinValue on dry land.</summary>
        public float SurfaceAt(Vector3 pos)
        {
            if (_map == null || !_map.IsWater(pos.x, pos.z)) return float.MinValue;
            if (!WavesEnabled) return SeaLevel;
            float h = Mathf.Sin(pos.x * WaveScale + _time) * 0.55f
                    + Mathf.Sin(pos.z * WaveScale * 1.37f + _time * 1.21f) * 0.35f
                    + Mathf.Sin((pos.x + pos.z) * WaveScale * 0.63f - _time * 0.78f) * 0.25f;
            return SeaLevel + h * WaveHeight;
        }

        public bool IsSubmerged(Vector3 pos, float offset = 0f)
        {
            float s = SurfaceAt(pos);
            return s != float.MinValue && pos.y + offset < s;
        }

        /// <summary>How deep a point is under the surface (0 when above water).</summary>
        public float Depth(Vector3 pos)
        {
            float s = SurfaceAt(pos);
            if (s == float.MinValue) return 0f;
            return Mathf.Max(0f, s - pos.y);
        }
    }
}
