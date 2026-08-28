using System.Collections.Generic;
using UnityEngine;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.Weapons
{
    /// <summary>
    /// Builds and caches the procedural model for every weapon, so all guns in
    /// the world share one mesh per type and the palette atlas material.
    /// </summary>
    public class WeaponCatalog : MonoBehaviour
    {
        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        private GameDatabase _db;

        public void Initialize(GameDatabase db) { _db = db; }

        public WeaponDefinition Get(string id) => _db != null ? _db.Weapon(id) : null;
        public List<WeaponDefinition> All => _db != null ? _db.weapons : new List<WeaponDefinition>();

        public Mesh MeshFor(WeaponDefinition def)
        {
            if (def == null) return null;
            if (_meshes.TryGetValue(def.id, out var mesh) && mesh != null) return mesh;
            mesh = Build(def);
            _meshes[def.id] = mesh;
            return mesh;
        }

        public Material[] Materials => new[] { PaletteAtlas.Matte, PaletteAtlas.Metal };

        private static Mesh Build(WeaponDefinition def)
        {
            var mb = new MeshBuilder(2);
            const int matte = 0, metal = 1;

            void Piece(int sub, Color colour, Vector3 centre, Vector3 size, Quaternion rot)
            {
                int start = mb.VertexCount;
                mb.AddBox(centre, size, rot, 0f, sub);
                mb.SetUVRange(start, mb.VertexCount, PaletteAtlas.UV(colour));
            }

            Vector3 body = def.bodySize;
            Color bodyColour = def.bodyColor;
            Color accent = def.accentColor;

            if (def.category == WeaponCategory.Melee || def.category == WeaponCategory.Unarmed)
            {
                if (def.category == WeaponCategory.Unarmed) return mb.ToMesh("Weapon_" + def.id);
                Piece(matte, bodyColour, new Vector3(0f, 0f, body.z * 0.5f), body, Quaternion.identity);
                Piece(matte, accent, new Vector3(0f, 0f, 0.05f), new Vector3(body.x * 1.25f, body.y * 1.25f, 0.13f), Quaternion.identity);
                return mb.ToMesh("Weapon_" + def.id);
            }

            if (def.category == WeaponCategory.Thrown)
            {
                int start = mb.VertexCount;
                mb.AddSphere(new Vector3(0f, 0f, 0.02f), body.y * 0.45f, 8, 5, matte);
                mb.SetUVRange(start, mb.VertexCount, PaletteAtlas.UV(bodyColour));
                Piece(metal, new Color(0.7f, 0.7f, 0.72f), new Vector3(0f, body.y * 0.42f, 0.02f), new Vector3(0.03f, 0.05f, 0.03f), Quaternion.identity);
                return mb.ToMesh("Weapon_" + def.id);
            }

            // Receiver.
            Piece(matte, bodyColour, new Vector3(0f, 0f, body.z * 0.5f), body, Quaternion.identity);
            // Barrel.
            Piece(metal, new Color(0.30f, 0.30f, 0.33f),
                new Vector3(0f, body.y * 0.18f, body.z + def.barrelLength * 0.5f),
                new Vector3(def.barrelRadius * 2f, def.barrelRadius * 2f, def.barrelLength), Quaternion.identity);
            // Grip.
            Piece(matte, accent, new Vector3(0f, -body.y * 0.62f, body.z * 0.18f),
                new Vector3(body.x * 0.9f, body.y * 0.95f, body.z * 0.30f), Quaternion.Euler(12f, 0f, 0f));
            // Trigger guard.
            Piece(metal, new Color(0.25f, 0.25f, 0.28f), new Vector3(0f, -body.y * 0.28f, body.z * 0.30f),
                new Vector3(body.x * 0.5f, 0.02f, body.z * 0.22f), Quaternion.identity);

            if (def.hasMagazine)
                Piece(matte, new Color(0.20f, 0.20f, 0.22f), new Vector3(0f, -body.y * 0.75f, body.z * 0.42f),
                    new Vector3(body.x * 0.72f, body.y * 1.1f, body.z * 0.20f), Quaternion.Euler(8f, 0f, 0f));

            if (def.hasStock)
                Piece(matte, bodyColour, new Vector3(0f, -body.y * 0.10f, -body.z * 0.42f),
                    new Vector3(body.x * 0.85f, body.y * 0.85f, body.z * 0.85f), Quaternion.identity);

            if (def.hasForegrip)
                Piece(matte, accent, new Vector3(0f, -body.y * 0.45f, body.z + def.barrelLength * 0.25f),
                    new Vector3(body.x * 0.7f, body.y * 0.7f, 0.09f), Quaternion.Euler(-10f, 0f, 0f));

            if (def.hasScope)
            {
                Piece(metal, new Color(0.16f, 0.16f, 0.18f), new Vector3(0f, body.y * 0.72f, body.z * 0.55f),
                    new Vector3(0.045f, 0.045f, body.z * 0.5f), Quaternion.identity);
                Piece(metal, new Color(0.16f, 0.16f, 0.18f), new Vector3(0f, body.y * 0.48f, body.z * 0.42f),
                    new Vector3(0.02f, body.y * 0.32f, 0.03f), Quaternion.identity);
            }

            return mb.ToMesh("Weapon_" + def.id);
        }

        private void OnDestroy()
        {
            foreach (var kv in _meshes) if (kv.Value != null) Destroy(kv.Value);
            _meshes.Clear();
        }
    }
}
