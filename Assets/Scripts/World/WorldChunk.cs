using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;

namespace SanMonica.World
{
    /// <summary>One streamed 256 m tile of San Monica.</summary>
    public class WorldChunk : MonoBehaviour
    {
        public Vector2Int Coord;
        public int Lod = -1;
        public Bounds Bounds;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private MeshCollider _collider;
        private readonly List<GameObject> _colliderHosts = new List<GameObject>(4);

        public readonly List<ChunkGeometry.PointLightSpec> LightSpecs = new List<ChunkGeometry.PointLightSpec>(16);

        public void Setup(Vector2Int coord, Transform parent, float chunkSize)
        {
            Coord = coord;
            transform.SetParent(parent, false);
            gameObject.name = "Chunk_" + coord.x + "_" + coord.y;
            gameObject.isStatic = false;
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _renderer.receiveShadows = true;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _renderer.allowOcclusionWhenDynamic = false;
        }

        public void Apply(ChunkGeometry geo, int lod, bool buildMeshCollider)
        {
            Lod = lod;
            ClearColliders();

            if (_mesh != null) Object.Destroy(_mesh);
            _mesh = geo.BuildMesh("ChunkMesh_" + Coord.x + "_" + Coord.y);
            _filter.sharedMesh = _mesh;
            _renderer.sharedMaterials = geo.UsedMaterials();
            Bounds = _mesh.bounds;

            if (buildMeshCollider)
            {
                if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();
                _collider.sharedMesh = null;
                _collider.sharedMesh = _mesh;
                _collider.convex = false;
                gameObject.layer = GameLayers.Ground;
            }
            else if (_collider != null)
            {
                _collider.sharedMesh = null;
                _collider.enabled = false;
            }

            // Box colliders grouped by rotation keep the physics scene cheap.
            var byRotation = new Dictionary<int, List<ChunkGeometry.BoxSpec>>();
            for (int i = 0; i < geo.Boxes.Count; i++)
            {
                var b = geo.Boxes[i];
                var e = b.Rotation.eulerAngles;
                int key = Mathf.RoundToInt(e.x) * 1000000 + Mathf.RoundToInt(e.y) * 1000 + Mathf.RoundToInt(e.z);
                key = key * 64 + b.Layer * 2 + (b.IsTrigger ? 1 : 0);
                if (!byRotation.TryGetValue(key, out var list)) { list = new List<ChunkGeometry.BoxSpec>(16); byRotation[key] = list; }
                list.Add(b);
            }

            foreach (var kv in byRotation)
            {
                var first = kv.Value[0];
                var host = new GameObject("Col");
                host.transform.SetParent(transform, false);
                host.transform.localRotation = first.Rotation;
                host.layer = first.Layer;
                var inv = Quaternion.Inverse(first.Rotation);
                foreach (var b in kv.Value)
                {
                    var bc = host.AddComponent<BoxCollider>();
                    bc.center = inv * b.Center;
                    bc.size = b.Size;
                    bc.isTrigger = b.IsTrigger;
                }
                _colliderHosts.Add(host);
            }

            LightSpecs.Clear();
            LightSpecs.AddRange(geo.Lights);
        }

        private void ClearColliders()
        {
            for (int i = 0; i < _colliderHosts.Count; i++)
                if (_colliderHosts[i] != null) Object.Destroy(_colliderHosts[i]);
            _colliderHosts.Clear();
        }

        public void Release()
        {
            ClearColliders();
            LightSpecs.Clear();
            if (_mesh != null) { Object.Destroy(_mesh); _mesh = null; }
            if (_filter != null) _filter.sharedMesh = null;
            if (_collider != null) _collider.sharedMesh = null;
            Lod = -1;
        }

        public void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible;
        }
    }
}
