using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Utils
{
    /// <summary>
    /// Reusable dynamic mesh assembler. Everything visible in San Monica -
    /// buildings, roads, vehicles, characters, props - is assembled through this
    /// class, which keeps the repository free of binary art assets while still
    /// producing real, collidable, lightmappable geometry.
    /// </summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>(512);
        private readonly List<Vector3> _normals = new List<Vector3>(512);
        private readonly List<Vector2> _uv = new List<Vector2>(512);
        private readonly List<Color32> _colors = new List<Color32>(512);
        private readonly List<List<int>> _submeshes = new List<List<int>>();

        public int VertexCount => _verts.Count;
        public int SubmeshCount => _submeshes.Count;
        public Color32 Tint = new Color32(255, 255, 255, 255);

        public MeshBuilder(int submeshes = 1)
        {
            for (int i = 0; i < Mathf.Max(1, submeshes); i++)
                _submeshes.Add(new List<int>(768));
        }

        public void Clear()
        {
            _verts.Clear(); _normals.Clear(); _uv.Clear(); _colors.Clear();
            for (int i = 0; i < _submeshes.Count; i++) _submeshes[i].Clear();
        }

        public void EnsureSubmesh(int index)
        {
            while (_submeshes.Count <= index) _submeshes.Add(new List<int>(256));
        }

        private List<int> Tris(int sub)
        {
            EnsureSubmesh(sub);
            return _submeshes[sub];
        }

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            _verts.Add(p); _normals.Add(n); _uv.Add(uv); _colors.Add(Tint);
            return _verts.Count - 1;
        }

        public void AddTriangle(int a, int b, int c, int sub = 0)
        {
            var t = Tris(sub);
            t.Add(a); t.Add(b); t.Add(c);
        }

        /// <summary>Adds a quad with counter clockwise winding when viewed from the normal side.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvScale, int sub = 0, Vector2 uvOffset = default)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i0 = AddVertex(a, n, uvOffset);
            int i1 = AddVertex(b, n, uvOffset + new Vector2(uvScale.x, 0f));
            int i2 = AddVertex(c, n, uvOffset + uvScale);
            int i3 = AddVertex(d, n, uvOffset + new Vector2(0f, uvScale.y));
            var t = Tris(sub);
            t.Add(i0); t.Add(i1); t.Add(i2);
            t.Add(i0); t.Add(i2); t.Add(i3);
        }

        public void AddTriangleFan(Vector3 center, Vector3 normal, IList<Vector3> ring, float uvScale = 1f, int sub = 0)
        {
            int c = AddVertex(center, normal, new Vector2(0.5f, 0.5f));
            int first = -1, prev = -1;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 p = ring[i];
                Vector2 uv = new Vector2(p.x, p.z) * uvScale + new Vector2(0.5f, 0.5f);
                int idx = AddVertex(p, normal, uv);
                if (first < 0) first = idx;
                else AddTriangle(c, prev, idx, sub);
                prev = idx;
            }
            if (first >= 0 && prev >= 0) AddTriangle(c, prev, first, sub);
        }

        /// <summary>Axis aligned box (optionally rotated) with per-face uv derived from world size.</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, float uvScale = 1f, int sub = 0)
        {
            Vector3 h = size * 0.5f;
            Vector3 rx = rot * Vector3.right * h.x;
            Vector3 ry = rot * Vector3.up * h.y;
            Vector3 rz = rot * Vector3.forward * h.z;

            // +Z / -Z
            AddQuad(center - rx - ry + rz, center + rx - ry + rz, center + rx + ry + rz, center - rx + ry + rz, new Vector2(size.x, size.y) * uvScale, sub);
            AddQuad(center + rx - ry - rz, center - rx - ry - rz, center - rx + ry - rz, center + rx + ry - rz, new Vector2(size.x, size.y) * uvScale, sub);
            // +X / -X
            AddQuad(center + rx - ry + rz, center + rx - ry - rz, center + rx + ry - rz, center + rx + ry + rz, new Vector2(size.z, size.y) * uvScale, sub);
            AddQuad(center - rx - ry - rz, center - rx - ry + rz, center - rx + ry + rz, center - rx + ry - rz, new Vector2(size.z, size.y) * uvScale, sub);
            // +Y / -Y
            AddQuad(center - rx + ry + rz, center + rx + ry + rz, center + rx + ry - rz, center - rx + ry - rz, new Vector2(size.x, size.z) * uvScale, sub);
            AddQuad(center - rx - ry - rz, center + rx - ry - rz, center + rx - ry + rz, center - rx - ry + rz, new Vector2(size.x, size.z) * uvScale, sub);
        }

        public void AddBox(Vector3 center, Vector3 size, float uvScale = 1f, int sub = 0)
            => AddBox(center, size, Quaternion.identity, uvScale, sub);

        /// <summary>Convex prism defined by a closed 2D footprint extruded along +Y.</summary>
        public void AddExtrusion(IList<Vector2> footprint, float baseY, float height, float uvScale, int wallSub, int capSub, bool addBottom = false)
        {
            int n = footprint.Count;
            if (n < 3) return;
            float topY = baseY + height;
            float u = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = footprint[i];
                Vector2 p1 = footprint[(i + 1) % n];
                float segLen = Vector2.Distance(p0, p1);
                Vector3 a = new Vector3(p0.x, baseY, p0.y);
                Vector3 b = new Vector3(p1.x, baseY, p1.y);
                Vector3 c = new Vector3(p1.x, topY, p1.y);
                Vector3 d = new Vector3(p0.x, topY, p0.y);
                Vector3 nrm = Vector3.Cross(b - a, d - a).normalized;
                int i0 = AddVertex(a, nrm, new Vector2(u, 0f) * uvScale);
                int i1 = AddVertex(b, nrm, new Vector2(u + segLen, 0f) * uvScale);
                int i2 = AddVertex(c, nrm, new Vector2(u + segLen, height) * uvScale);
                int i3 = AddVertex(d, nrm, new Vector2(u, height) * uvScale);
                var t = Tris(wallSub);
                t.Add(i0); t.Add(i1); t.Add(i2);
                t.Add(i0); t.Add(i2); t.Add(i3);
                u += segLen;
            }

            // Fan triangulation is valid for the convex footprints we generate.
            int c0 = AddVertex(new Vector3(footprint[0].x, topY, footprint[0].y), Vector3.up, footprint[0] * uvScale);
            int prev = AddVertex(new Vector3(footprint[1].x, topY, footprint[1].y), Vector3.up, footprint[1] * uvScale);
            for (int i = 2; i < n; i++)
            {
                int cur = AddVertex(new Vector3(footprint[i].x, topY, footprint[i].y), Vector3.up, footprint[i] * uvScale);
                AddTriangle(c0, prev, cur, capSub);
                prev = cur;
            }

            if (addBottom)
            {
                int b0 = AddVertex(new Vector3(footprint[0].x, baseY, footprint[0].y), Vector3.down, footprint[0] * uvScale);
                int bprev = AddVertex(new Vector3(footprint[1].x, baseY, footprint[1].y), Vector3.down, footprint[1] * uvScale);
                for (int i = 2; i < n; i++)
                {
                    int cur = AddVertex(new Vector3(footprint[i].x, baseY, footprint[i].y), Vector3.down, footprint[i] * uvScale);
                    AddTriangle(b0, cur, bprev, capSub);
                    bprev = cur;
                }
            }
        }

        public void AddCylinder(Vector3 center, float radius, float height, int segments = 12, int sub = 0, bool caps = true, float uvScale = 1f)
        {
            segments = Mathf.Max(3, segments);
            float half = height * 0.5f;
            int baseIdx = _verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p = center + dir * radius;
                AddVertex(p + Vector3.up * half, dir, new Vector2((float)i / segments * radius * 6f, height) * uvScale);
                AddVertex(p - Vector3.up * half, dir, new Vector2((float)i / segments * radius * 6f, 0f) * uvScale);
            }
            var t = Tris(sub);
            for (int i = 0; i < segments; i++)
            {
                int a = baseIdx + i * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 3);
                t.Add(a); t.Add(a + 3); t.Add(a + 2);
            }
            if (caps)
            {
                var ring = new Vector3[segments];
                for (int i = 0; i < segments; i++)
                {
                    float a = (float)i / segments * Mathf.PI * 2f;
                    ring[i] = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * half;
                }
                AddTriangleFan(center + Vector3.up * half, Vector3.up, ring, uvScale, sub);
                for (int i = 0; i < segments; i++) ring[i] -= Vector3.up * height;
                System.Array.Reverse(ring);
                AddTriangleFan(center - Vector3.up * half, Vector3.down, ring, uvScale, sub);
            }
        }

        public void AddSphere(Vector3 center, float radius, int segments = 10, int rings = 6, int sub = 0)
        {
            segments = Mathf.Max(4, segments); rings = Mathf.Max(3, rings);
            int baseIdx = _verts.Count;
            for (int y = 0; y <= rings; y++)
            {
                float v = (float)y / rings;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= segments; x++)
                {
                    float u = (float)x / segments;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    AddVertex(center + n * radius, n, new Vector2(u, 1f - v));
                }
            }
            var t = Tris(sub);
            int stride = segments + 1;
            for (int y = 0; y < rings; y++)
            for (int x = 0; x < segments; x++)
            {
                int a = baseIdx + y * stride + x;
                int b = a + stride;
                t.Add(a); t.Add(b); t.Add(a + 1);
                t.Add(a + 1); t.Add(b); t.Add(b + 1);
            }
        }

        /// <summary>Capsule aligned to +Y, used for stylised limbs and torsos.</summary>
        public void AddCapsule(Vector3 center, float radius, float height, int segments = 8, int sub = 0)
        {
            float cyl = Mathf.Max(0f, height - radius * 2f);
            AddCylinder(center, radius, cyl, segments, sub, false);
            AddSphere(center + Vector3.up * (cyl * 0.5f), radius, segments, Mathf.Max(3, segments / 2), sub);
            AddSphere(center - Vector3.up * (cyl * 0.5f), radius, segments, Mathf.Max(3, segments / 2), sub);
        }

        /// <summary>Tapered box - the workhorse for vehicle bodies and roof shapes.</summary>
        public void AddTaperedBox(Vector3 center, Vector3 size, float topScaleX, float topScaleZ, Quaternion rot, int sub = 0, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            Vector3 R(Vector3 v) => center + rot * v;
            float tx = h.x * topScaleX, tz = h.z * topScaleZ;

            Vector3 b0 = R(new Vector3(-h.x, -h.y, -h.z)), b1 = R(new Vector3(h.x, -h.y, -h.z));
            Vector3 b2 = R(new Vector3(h.x, -h.y, h.z)), b3 = R(new Vector3(-h.x, -h.y, h.z));
            Vector3 t0 = R(new Vector3(-tx, h.y, -tz)), t1 = R(new Vector3(tx, h.y, -tz));
            Vector3 t2 = R(new Vector3(tx, h.y, tz)), t3 = R(new Vector3(-tx, h.y, tz));

            AddQuad(b1, b0, t0, t1, new Vector2(size.x, size.y) * uvScale, sub);
            AddQuad(b3, b2, t2, t3, new Vector2(size.x, size.y) * uvScale, sub);
            AddQuad(b2, b1, t1, t2, new Vector2(size.z, size.y) * uvScale, sub);
            AddQuad(b0, b3, t3, t0, new Vector2(size.z, size.y) * uvScale, sub);
            AddQuad(t0, t3, t2, t1, new Vector2(size.x, size.z) * uvScale, sub);
            AddQuad(b0, b1, b2, b3, new Vector2(size.x, size.z) * uvScale, sub);
        }

        /// <summary>Flat horizontal grid, optionally displaced by a height function.</summary>
        public void AddGrid(Vector3 origin, float sizeX, float sizeZ, int cellsX, int cellsZ, System.Func<float, float, float> height, float uvScale, int sub = 0)
        {
            cellsX = Mathf.Max(1, cellsX); cellsZ = Mathf.Max(1, cellsZ);
            int baseIdx = _verts.Count;
            float stepX = sizeX / cellsX, stepZ = sizeZ / cellsZ;
            for (int z = 0; z <= cellsZ; z++)
            for (int x = 0; x <= cellsX; x++)
            {
                float wx = origin.x + x * stepX;
                float wz = origin.z + z * stepZ;
                float wy = origin.y + (height != null ? height(wx, wz) : 0f);
                AddVertex(new Vector3(wx, wy, wz), Vector3.up, new Vector2(wx, wz) * uvScale);
            }
            var t = Tris(sub);
            int stride = cellsX + 1;
            for (int z = 0; z < cellsZ; z++)
            for (int x = 0; x < cellsX; x++)
            {
                int a = baseIdx + z * stride + x;
                int b = a + stride;
                t.Add(a); t.Add(b); t.Add(a + 1);
                t.Add(a + 1); t.Add(b); t.Add(b + 1);
            }
        }

        /// <summary>Overwrites the UVs of a vertex range - used for palette atlas mapping.</summary>
        public void SetUVRange(int start, int end, Vector2 uv)
        {
            for (int i = Mathf.Max(0, start); i < Mathf.Min(end, _uv.Count); i++) _uv[i] = uv;
        }

        /// <summary>Applies a flat tint to a vertex range.</summary>
        public void SetColorRange(int start, int end, Color32 color)
        {
            for (int i = Mathf.Max(0, start); i < Mathf.Min(end, _colors.Count); i++) _colors[i] = color;
        }

        public Vector3 GetVertex(int index) => _verts[index];

        public void RecalculateSmoothNormals()
        {
            for (int i = 0; i < _normals.Count; i++) _normals[i] = Vector3.zero;
            foreach (var sm in _submeshes)
            {
                for (int i = 0; i + 2 < sm.Count; i += 3)
                {
                    int a = sm[i], b = sm[i + 1], c = sm[i + 2];
                    Vector3 n = Vector3.Cross(_verts[b] - _verts[a], _verts[c] - _verts[a]);
                    _normals[a] += n; _normals[b] += n; _normals[c] += n;
                }
            }
            for (int i = 0; i < _normals.Count; i++)
            {
                Vector3 n = _normals[i];
                _normals[i] = n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up;
            }
        }

        public Mesh ToMesh(string name, bool smoothNormals = false, bool markNoLongerReadable = false)
        {
            if (smoothNormals) RecalculateSmoothNormals();
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uv);
            mesh.SetColors(_colors);

            int used = 0;
            for (int i = 0; i < _submeshes.Count; i++) if (_submeshes[i].Count > 0) used++;
            mesh.subMeshCount = Mathf.Max(1, used);
            int slot = 0;
            for (int i = 0; i < _submeshes.Count; i++)
            {
                if (_submeshes[i].Count == 0) continue;
                mesh.SetTriangles(_submeshes[i], slot++);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (markNoLongerReadable) mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Maps the builder's submesh slots to the materials that are actually used.</summary>
        public Material[] FilterMaterials(Material[] all)
        {
            var list = new List<Material>();
            for (int i = 0; i < _submeshes.Count; i++)
            {
                if (_submeshes[i].Count == 0) continue;
                list.Add(i < all.Length ? all[i] : all[all.Length - 1]);
            }
            if (list.Count == 0) list.Add(all.Length > 0 ? all[0] : null);
            return list.ToArray();
        }
    }
}
