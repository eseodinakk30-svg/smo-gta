using System.Collections.Generic;
using UnityEngine;
using SanMonica.Utils;

namespace SanMonica.World
{
    /// <summary>
    /// Collects all the geometry of one world chunk into a single multi-submesh
    /// mesh plus a compact list of box colliders. One chunk therefore costs a
    /// handful of draw calls instead of thousands of separate renderers.
    /// </summary>
    public class ChunkGeometry
    {
        public readonly MeshBuilder Builder = new MeshBuilder(1);
        private readonly List<Material> _materials = new List<Material>(16);
        private readonly Dictionary<Material, int> _lookup = new Dictionary<Material, int>(16);

        public readonly List<BoxSpec> Boxes = new List<BoxSpec>(64);
        public readonly List<PointLightSpec> Lights = new List<PointLightSpec>(16);

        public struct BoxSpec
        {
            public Vector3 Center;
            public Vector3 Size;
            public Quaternion Rotation;
            public int Layer;
            public bool IsTrigger;
        }

        public struct PointLightSpec
        {
            public Vector3 Position;
            public Color Color;
            public float Range;
            public float Intensity;
            public bool NightOnly;
        }

        public int VertexCount => Builder.VertexCount;

        public void Clear()
        {
            Builder.Clear();
            _materials.Clear();
            _lookup.Clear();
            Boxes.Clear();
            Lights.Clear();
        }

        /// <summary>Returns the submesh index that renders with this material.</summary>
        public int Sub(Material m)
        {
            if (m == null) m = MaterialLibrary.Solid(Color.magenta);
            if (_lookup.TryGetValue(m, out int idx)) return idx;
            idx = _materials.Count;
            _materials.Add(m);
            _lookup[m] = idx;
            Builder.EnsureSubmesh(idx);
            return idx;
        }

        public void AddBoxCollider(Vector3 center, Vector3 size, int layer, Quaternion rot = default, bool trigger = false)
        {
            if (rot == default) rot = Quaternion.identity;
            Boxes.Add(new BoxSpec { Center = center, Size = size, Rotation = rot, Layer = layer, IsTrigger = trigger });
        }

        public void AddLight(Vector3 pos, Color color, float range, float intensity, bool nightOnly = true)
        {
            Lights.Add(new PointLightSpec { Position = pos, Color = color, Range = range, Intensity = intensity, NightOnly = nightOnly });
        }

        public Material[] Materials => _materials.ToArray();

        public Mesh BuildMesh(string name, bool smooth = false)
        {
            var mesh = Builder.ToMesh(name, smooth);
            return mesh;
        }

        /// <summary>Materials trimmed to the submeshes that actually received triangles.</summary>
        public Material[] UsedMaterials() => Builder.FilterMaterials(_materials.ToArray());
    }
}
