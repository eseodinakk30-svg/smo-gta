using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    /// <summary>
    /// Real, walkable interiors for shops and owned property. Rooms are built
    /// directly underneath their building so world streaming stays coherent, and
    /// the transition is a short fade rather than a loading screen.
    /// </summary>
    public class InteriorSystem : MonoBehaviour
    {
        private const float InteriorDepth = -620f;

        private readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();
        private Transform _root;

        public bool IsInside { get; private set; }
        public ShopInstance CurrentShop { get; private set; }
        public PropertyInstance CurrentProperty { get; private set; }
        public Vector3 ExitWorldPosition { get; private set; }
        public float ExitHeading { get; private set; }
        public Vector3 ClerkPosition { get; private set; }

        public void Initialize()
        {
            _root = new GameObject("Interiors").transform;
            _root.SetParent(transform, false);
        }

        public Vector3 EnterShop(ShopInstance shop)
        {
            if (shop == null) return Vector3.zero;
            ExitWorldPosition = shop.Position + shop.Forward * 1.6f;
            ExitHeading = Mathf.Atan2(shop.Forward.x, shop.Forward.z) * Mathf.Rad2Deg;
            CurrentShop = shop;
            CurrentProperty = null;
            IsInside = true;

            string key = "shop_" + shop.Index;
            var go = GetOrBuild(key, () => BuildShopRoom(shop));
            go.SetActive(true);
            return go.transform.position + new Vector3(0f, 0.2f, -RoomDepth(shop.Definition.type) * 0.5f + 2.0f);
        }

        public Vector3 EnterProperty(PropertyInstance property)
        {
            if (property == null) return Vector3.zero;
            ExitWorldPosition = property.Definition.position + Vector3.up * 0.2f;
            ExitHeading = property.Definition.heading + 180f;
            CurrentProperty = property;
            CurrentShop = null;
            IsInside = true;

            string key = "prop_" + property.Index;
            var go = GetOrBuild(key, () => BuildPropertyRoom(property));
            go.SetActive(true);
            return go.transform.position + new Vector3(0f, 0.2f, -7f);
        }

        public Vector3 Exit()
        {
            IsInside = false;
            foreach (var kv in _cache) if (kv.Value != null) kv.Value.SetActive(false);
            var pos = ExitWorldPosition;
            CurrentShop = null;
            CurrentProperty = null;
            return pos;
        }

        private GameObject GetOrBuild(string key, System.Func<GameObject> factory)
        {
            if (_cache.TryGetValue(key, out var go) && go != null) return go;
            go = factory();
            _cache[key] = go;
            if (_cache.Count > 8) TrimCache(key);
            return go;
        }

        private void TrimCache(string keep)
        {
            var remove = new List<string>();
            foreach (var kv in _cache)
            {
                if (kv.Key == keep) continue;
                if (kv.Value != null && kv.Value.activeSelf) continue;
                remove.Add(kv.Key);
                if (_cache.Count - remove.Count <= 6) break;
            }
            foreach (var k in remove)
            {
                if (_cache[k] != null) Destroy(_cache[k]);
                _cache.Remove(k);
            }
        }

        private static float RoomWidth(ShopType t)
        {
            switch (t)
            {
                case ShopType.Dealership: return 34f;
                case ShopType.Mechanic: return 26f;
                case ShopType.Hospital:
                case ShopType.PoliceStation: return 28f;
                case ShopType.Nightclub: return 30f;
                default: return 16f;
            }
        }

        private static float RoomDepth(ShopType t)
        {
            switch (t)
            {
                case ShopType.Dealership: return 26f;
                case ShopType.Mechanic: return 22f;
                case ShopType.Hospital:
                case ShopType.PoliceStation: return 22f;
                case ShopType.Nightclub: return 24f;
                default: return 14f;
            }
        }

        // ------------------------------------------------------------------
        private GameObject BuildShopRoom(ShopInstance shop)
        {
            var def = shop.Definition;
            float w = RoomWidth(def.type), d = RoomDepth(def.type), h = def.type == ShopType.Dealership ? 7f : 4.2f;
            var go = new GameObject("Interior_" + def.id + "_" + shop.Index);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(shop.Position.x, InteriorDepth, shop.Position.z);

            var geo = new ChunkGeometry();
            var rng = new Rng(shop.Index * 7919 + 13);
            BuildShell(geo, w, d, h, InteriorPalette(def.type), ref rng);

            switch (def.type)
            {
                case ShopType.GunStore: BuildGunStore(geo, w, d, ref rng); break;
                case ShopType.ClothingStore: BuildClothingStore(geo, w, d, ref rng); break;
                case ShopType.Barber: BuildBarber(geo, w, d, ref rng); break;
                case ShopType.Mechanic: BuildMechanic(geo, w, d, ref rng); break;
                case ShopType.Dealership:
                case ShopType.Marine:
                case ShopType.Aviation: BuildShowroom(geo, w, d, ref rng); break;
                case ShopType.GasStation:
                case ShopType.ConvenienceStore:
                case ShopType.Pharmacy:
                case ShopType.Hardware: BuildStoreAisles(geo, w, d, ref rng); break;
                case ShopType.Restaurant: BuildRestaurant(geo, w, d, ref rng); break;
                case ShopType.Nightclub: BuildNightclub(geo, w, d, ref rng); break;
                case ShopType.Hospital: BuildClinic(geo, w, d, ref rng); break;
                case ShopType.PoliceStation: BuildPrecinct(geo, w, d, ref rng); break;
                default: BuildStoreAisles(geo, w, d, ref rng); break;
            }

            // Service counter and clerk position.
            var counterMat = MaterialLibrary.Surface(SurfaceKind.Wood, 0, new Color(0.42f, 0.30f, 0.20f), 0.3f);
            geo.Builder.AddBox(new Vector3(0f, 0.55f, d * 0.5f - 3.0f), new Vector3(w * 0.5f, 1.1f, 0.8f), Quaternion.identity, 0.5f, geo.Sub(counterMat));
            geo.AddBoxCollider(new Vector3(0f, 0.55f, d * 0.5f - 3.0f), new Vector3(w * 0.5f, 1.1f, 0.8f), GameLayers.Prop);
            ClerkPosition = go.transform.position + new Vector3(0f, 0f, d * 0.5f - 1.9f);

            Materialise(go, geo, "ShopInterior");
            AddExitTrigger(go, new Vector3(0f, 1.1f, -d * 0.5f + 0.6f), new Vector3(3.2f, 2.4f, 1.2f));
            return go;
        }

        private GameObject BuildPropertyRoom(PropertyInstance prop)
        {
            var def = prop.Definition;
            bool big = def.kind == PropertyKind.Villa || def.kind == PropertyKind.Penthouse || def.kind == PropertyKind.Warehouse;
            float w = big ? 26f : 15f, d = big ? 20f : 13f, h = big ? 4.6f : 3.4f;

            var go = new GameObject("Interior_" + def.id);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(def.position.x, InteriorDepth, def.position.z);

            var geo = new ChunkGeometry();
            var rng = new Rng(prop.Index * 104729 + 7);
            BuildShell(geo, w, d, h, new Color(0.86f, 0.84f, 0.80f), ref rng);

            var wood = MaterialLibrary.Surface(SurfaceKind.Wood, 1, new Color(0.48f, 0.34f, 0.22f), 0.28f);
            var fabric = MaterialLibrary.Solid(new Color(0.30f, 0.34f, 0.42f), 0.12f, 0f, "sofa");
            int ws = geo.Sub(wood), fs = geo.Sub(fabric);

            // Bed - the save point.
            geo.Builder.AddBox(new Vector3(-w * 0.3f, 0.30f, d * 0.3f), new Vector3(2.1f, 0.6f, 1.5f), Quaternion.identity, 0.5f, ws);
            geo.Builder.AddBox(new Vector3(-w * 0.3f, 0.68f, d * 0.3f), new Vector3(2.0f, 0.22f, 1.4f), Quaternion.identity, 0.5f, fs);
            geo.AddBoxCollider(new Vector3(-w * 0.3f, 0.35f, d * 0.3f), new Vector3(2.1f, 0.7f, 1.5f), GameLayers.Prop);

            // Sofa, table, wardrobe.
            geo.Builder.AddBox(new Vector3(w * 0.22f, 0.35f, -d * 0.2f), new Vector3(2.4f, 0.7f, 0.9f), Quaternion.identity, 0.5f, fs);
            geo.Builder.AddBox(new Vector3(w * 0.22f, 0.42f, 0.4f), new Vector3(1.4f, 0.1f, 0.8f), Quaternion.identity, 0.5f, ws);
            geo.Builder.AddBox(new Vector3(w * 0.42f, 1.05f, d * 0.35f), new Vector3(1.6f, 2.1f, 0.6f), Quaternion.identity, 0.4f, ws);
            geo.AddBoxCollider(new Vector3(w * 0.42f, 1.05f, d * 0.35f), new Vector3(1.6f, 2.1f, 0.6f), GameLayers.Prop);

            if (def.garageSlots > 0)
            {
                var concrete = MaterialLibrary.Surface(SurfaceKind.Concrete, 0, new Color(0.62f, 0.62f, 0.60f), 0.15f);
                geo.Builder.AddBox(new Vector3(0f, 0.02f, -d * 0.36f), new Vector3(w * 0.9f, 0.05f, d * 0.25f), Quaternion.identity, 0.2f, geo.Sub(concrete));
            }

            Materialise(go, geo, "PropertyInterior");
            AddExitTrigger(go, new Vector3(0f, 1.1f, -d * 0.5f + 0.6f), new Vector3(3.2f, 2.4f, 1.2f));
            return go;
        }

        // ------------------------------------------------------------------
        private static Color InteriorPalette(ShopType t)
        {
            switch (t)
            {
                case ShopType.GunStore: return new Color(0.42f, 0.40f, 0.38f);
                case ShopType.Nightclub: return new Color(0.16f, 0.13f, 0.22f);
                case ShopType.Hospital: return new Color(0.90f, 0.93f, 0.94f);
                case ShopType.PoliceStation: return new Color(0.68f, 0.72f, 0.78f);
                case ShopType.Mechanic: return new Color(0.52f, 0.53f, 0.55f);
                default: return new Color(0.84f, 0.82f, 0.78f);
            }
        }

        private void BuildShell(ChunkGeometry geo, float w, float d, float h, Color wallTint, ref Rng rng)
        {
            var floor = MaterialLibrary.Surface(SurfaceKind.Tile, 0, new Color(0.72f, 0.71f, 0.68f), 0.35f);
            var wall = MaterialLibrary.Surface(SurfaceKind.Plaster, 0, wallTint, 0.12f);
            var ceil = MaterialLibrary.Surface(SurfaceKind.Concrete, 0, new Color(0.88f, 0.88f, 0.86f), 0.08f);

            geo.Builder.AddBox(new Vector3(0f, -0.1f, 0f), new Vector3(w, 0.2f, d), Quaternion.identity, 0.3f, geo.Sub(floor));
            geo.AddBoxCollider(new Vector3(0f, -0.1f, 0f), new Vector3(w, 0.2f, d), GameLayers.Ground);
            geo.Builder.AddBox(new Vector3(0f, h + 0.1f, 0f), new Vector3(w, 0.2f, d), Quaternion.identity, 0.3f, geo.Sub(ceil));
            geo.AddBoxCollider(new Vector3(0f, h + 0.1f, 0f), new Vector3(w, 0.2f, d), GameLayers.Building);

            int ws = geo.Sub(wall);
            geo.Builder.AddBox(new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, 0.3f), Quaternion.identity, 0.25f, ws);
            geo.Builder.AddBox(new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(0.3f, h, d), Quaternion.identity, 0.25f, ws);
            geo.Builder.AddBox(new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(0.3f, h, d), Quaternion.identity, 0.25f, ws);
            geo.AddBoxCollider(new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, 0.3f), GameLayers.Building);
            geo.AddBoxCollider(new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(0.3f, h, d), GameLayers.Building);
            geo.AddBoxCollider(new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(0.3f, h, d), GameLayers.Building);
            // Front wall with a doorway gap.
            geo.Builder.AddBox(new Vector3(-w * 0.28f, h * 0.5f, -d * 0.5f), new Vector3(w * 0.44f, h, 0.3f), Quaternion.identity, 0.25f, ws);
            geo.Builder.AddBox(new Vector3(w * 0.28f, h * 0.5f, -d * 0.5f), new Vector3(w * 0.44f, h, 0.3f), Quaternion.identity, 0.25f, ws);
            geo.Builder.AddBox(new Vector3(0f, h - 0.5f, -d * 0.5f), new Vector3(w * 0.14f, 1f, 0.3f), Quaternion.identity, 0.25f, ws);
            geo.AddBoxCollider(new Vector3(-w * 0.28f, h * 0.5f, -d * 0.5f), new Vector3(w * 0.44f, h, 0.3f), GameLayers.Building);
            geo.AddBoxCollider(new Vector3(w * 0.28f, h * 0.5f, -d * 0.5f), new Vector3(w * 0.44f, h, 0.3f), GameLayers.Building);

            // Ceiling strip lights.
            var lamp = MaterialLibrary.Emissive(new Color(1f, 0.97f, 0.90f), 2.2f);
            int ls = geo.Sub(lamp);
            int rows = Mathf.Max(1, Mathf.RoundToInt(d / 5f));
            for (int i = 0; i < rows; i++)
            {
                float z = (-0.5f + (i + 0.5f) / rows) * d;
                geo.Builder.AddBox(new Vector3(0f, h - 0.12f, z), new Vector3(w * 0.6f, 0.1f, 0.35f), Quaternion.identity, 0.4f, ls);
                geo.AddLight(new Vector3(0f, h - 0.4f, z), new Color(1f, 0.97f, 0.9f), 14f, 2.4f, false);
            }
        }

        private void BuildGunStore(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var metal = MaterialLibrary.Solid(new Color(0.30f, 0.31f, 0.33f), 0.4f, 0.5f, "rack");
            var glass = MaterialLibrary.Transparent(new Color(0.7f, 0.8f, 0.85f, 0.25f));
            int ms = geo.Sub(metal), gs = geo.Sub(glass);
            for (int side = -1; side <= 1; side += 2)
            {
                geo.Builder.AddBox(new Vector3(side * (w * 0.5f - 0.6f), 1.4f, 0f), new Vector3(0.5f, 2.4f, d * 0.7f), Quaternion.identity, 0.4f, ms);
                geo.AddBoxCollider(new Vector3(side * (w * 0.5f - 0.6f), 1.2f, 0f), new Vector3(0.6f, 2.4f, d * 0.7f), GameLayers.Prop);
            }
            geo.Builder.AddBox(new Vector3(0f, 0.9f, -1.5f), new Vector3(w * 0.4f, 0.1f, 1.0f), Quaternion.identity, 0.5f, gs);
            geo.Builder.AddBox(new Vector3(0f, 0.45f, -1.5f), new Vector3(w * 0.4f, 0.9f, 1.0f), Quaternion.identity, 0.5f, ms);
            geo.AddBoxCollider(new Vector3(0f, 0.45f, -1.5f), new Vector3(w * 0.4f, 0.9f, 1.0f), GameLayers.Prop);
        }

        private void BuildClothingStore(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var metal = MaterialLibrary.Solid(new Color(0.70f, 0.71f, 0.73f), 0.45f, 0.6f, "railing");
            int ms = geo.Sub(metal);
            for (int i = 0; i < 4; i++)
            {
                float x = (-0.5f + (i + 0.5f) / 4f) * w * 0.8f;
                geo.Builder.AddBox(new Vector3(x, 1.7f, 0f), new Vector3(0.06f, 0.06f, d * 0.5f), Quaternion.identity, 0.6f, ms);
                geo.Builder.AddBox(new Vector3(x, 0.9f, 0f), new Vector3(0.1f, 1.6f, 0.1f), Quaternion.identity, 0.6f, ms);
                var cloth = MaterialLibrary.Solid(new Color(rng.Range(0.25f, 0.95f), rng.Range(0.25f, 0.95f), rng.Range(0.25f, 0.95f)), 0.1f, 0f, "cloth" + i);
                geo.Builder.AddBox(new Vector3(x, 1.2f, 0f), new Vector3(0.42f, 0.95f, d * 0.45f), Quaternion.identity, 0.5f, geo.Sub(cloth));
                geo.AddBoxCollider(new Vector3(x, 1.0f, 0f), new Vector3(0.5f, 2f, d * 0.5f), GameLayers.Prop);
            }
        }

        private void BuildBarber(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var chair = MaterialLibrary.Solid(new Color(0.20f, 0.20f, 0.24f), 0.2f, 0.1f, "chair");
            var mirror = MaterialLibrary.Solid(new Color(0.82f, 0.86f, 0.90f), 0.95f, 0.9f, "mirror");
            int cs = geo.Sub(chair), rs = geo.Sub(mirror);
            for (int i = 0; i < 3; i++)
            {
                float x = (-0.5f + (i + 0.5f) / 3f) * w * 0.7f;
                geo.Builder.AddBox(new Vector3(x, 0.5f, 1.5f), new Vector3(0.7f, 1f, 0.7f), Quaternion.identity, 0.6f, cs);
                geo.Builder.AddBox(new Vector3(x, 1.6f, d * 0.5f - 0.5f), new Vector3(1.1f, 1.5f, 0.06f), Quaternion.identity, 0.5f, rs);
                geo.AddBoxCollider(new Vector3(x, 0.5f, 1.5f), new Vector3(0.8f, 1.2f, 0.8f), GameLayers.Prop);
            }
        }

        private void BuildMechanic(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var metal = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 0, new Color(0.55f, 0.56f, 0.58f), 0.3f, 0.4f);
            int ms = geo.Sub(metal);
            geo.Builder.AddBox(new Vector3(0f, 0.25f, 1f), new Vector3(5.5f, 0.5f, 2.6f), Quaternion.identity, 0.3f, ms);
            geo.AddBoxCollider(new Vector3(0f, 0.25f, 1f), new Vector3(5.5f, 0.5f, 2.6f), GameLayers.Prop);
            for (int side = -1; side <= 1; side += 2)
            {
                geo.Builder.AddBox(new Vector3(side * (w * 0.5f - 1.0f), 1.0f, -d * 0.25f), new Vector3(0.8f, 2f, 3.5f), Quaternion.identity, 0.4f, ms);
                geo.AddBoxCollider(new Vector3(side * (w * 0.5f - 1.0f), 1.0f, -d * 0.25f), new Vector3(0.8f, 2f, 3.5f), GameLayers.Prop);
            }
        }

        private void BuildShowroom(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var podium = MaterialLibrary.Surface(SurfaceKind.Marble, 0, Color.white, 0.5f);
            int ps = geo.Sub(podium);
            for (int i = 0; i < 3; i++)
            {
                float x = (-0.5f + (i + 0.5f) / 3f) * w * 0.72f;
                geo.Builder.AddCylinder(new Vector3(x, 0.12f, 1.5f), 3.2f, 0.24f, 16, ps, true, 0.3f);
            }
        }

        private void BuildStoreAisles(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var shelf = MaterialLibrary.Solid(new Color(0.62f, 0.63f, 0.65f), 0.3f, 0.3f, "shelf");
            int ss = geo.Sub(shelf);
            int aisles = Mathf.Max(2, Mathf.RoundToInt(w / 5f));
            for (int i = 0; i < aisles; i++)
            {
                float x = (-0.5f + (i + 0.5f) / aisles) * w * 0.8f;
                geo.Builder.AddBox(new Vector3(x, 1.0f, -0.5f), new Vector3(1.0f, 2.0f, d * 0.55f), Quaternion.identity, 0.4f, ss);
                geo.AddBoxCollider(new Vector3(x, 1.0f, -0.5f), new Vector3(1.0f, 2.0f, d * 0.55f), GameLayers.Prop);
                for (int k = 0; k < 4; k++)
                {
                    var goods = MaterialLibrary.Solid(new Color(rng.Range(0.3f, 1f), rng.Range(0.3f, 1f), rng.Range(0.3f, 1f)), 0.2f, 0f, "goods" + i + k);
                    geo.Builder.AddBox(new Vector3(x, 0.45f + k * 0.48f, -0.5f), new Vector3(1.04f, 0.30f, d * 0.5f), Quaternion.identity, 0.5f, geo.Sub(goods));
                }
            }
        }

        private void BuildRestaurant(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var wood = MaterialLibrary.Surface(SurfaceKind.Wood, 0, new Color(0.48f, 0.34f, 0.22f), 0.3f);
            int ws = geo.Sub(wood);
            for (int i = 0; i < 4; i++)
            {
                float x = (-0.5f + (i % 2 + 0.5f) / 2f) * w * 0.7f;
                float z = (-0.5f + (i / 2 + 0.5f) / 2f) * d * 0.6f;
                geo.Builder.AddCylinder(new Vector3(x, 0.75f, z), 0.75f, 0.08f, 12, ws, true, 0.5f);
                geo.Builder.AddCylinder(new Vector3(x, 0.36f, z), 0.10f, 0.72f, 8, ws, false, 0.5f);
                geo.AddBoxCollider(new Vector3(x, 0.4f, z), new Vector3(1.5f, 0.8f, 1.5f), GameLayers.Prop);
            }
        }

        private void BuildNightclub(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var floorGlow = MaterialLibrary.Emissive(new Color(0.55f, 0.15f, 0.85f), 1.6f);
            var bar = MaterialLibrary.Solid(new Color(0.12f, 0.12f, 0.16f), 0.5f, 0.3f, "bar");
            geo.Builder.AddBox(new Vector3(0f, 0.03f, 0f), new Vector3(w * 0.5f, 0.06f, d * 0.4f), Quaternion.identity, 0.3f, geo.Sub(floorGlow));
            geo.Builder.AddBox(new Vector3(-w * 0.34f, 0.6f, 0f), new Vector3(1.2f, 1.2f, d * 0.5f), Quaternion.identity, 0.4f, geo.Sub(bar));
            geo.AddBoxCollider(new Vector3(-w * 0.34f, 0.6f, 0f), new Vector3(1.2f, 1.2f, d * 0.5f), GameLayers.Prop);
            geo.AddLight(new Vector3(0f, 3f, 0f), new Color(0.7f, 0.2f, 0.95f), 20f, 3f, false);
        }

        private void BuildClinic(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var bed = MaterialLibrary.Solid(new Color(0.90f, 0.92f, 0.94f), 0.2f, 0.1f, "hbed");
            int bs = geo.Sub(bed);
            for (int i = 0; i < 4; i++)
            {
                float x = (-0.5f + (i + 0.5f) / 4f) * w * 0.8f;
                geo.Builder.AddBox(new Vector3(x, 0.55f, d * 0.25f), new Vector3(0.9f, 0.2f, 2.1f), Quaternion.identity, 0.5f, bs);
                geo.Builder.AddBox(new Vector3(x, 0.28f, d * 0.25f), new Vector3(0.7f, 0.55f, 1.9f), Quaternion.identity, 0.5f, bs);
                geo.AddBoxCollider(new Vector3(x, 0.4f, d * 0.25f), new Vector3(0.9f, 0.8f, 2.1f), GameLayers.Prop);
            }
        }

        private void BuildPrecinct(ChunkGeometry geo, float w, float d, ref Rng rng)
        {
            var desk = MaterialLibrary.Surface(SurfaceKind.Wood, 1, new Color(0.35f, 0.28f, 0.22f), 0.3f);
            var bars = MaterialLibrary.Solid(new Color(0.35f, 0.36f, 0.38f), 0.4f, 0.7f, "bars");
            int ds = geo.Sub(desk), bsx = geo.Sub(bars);
            for (int i = 0; i < 3; i++)
            {
                float x = (-0.5f + (i + 0.5f) / 3f) * w * 0.7f;
                geo.Builder.AddBox(new Vector3(x, 0.4f, -d * 0.2f), new Vector3(1.8f, 0.8f, 0.9f), Quaternion.identity, 0.5f, ds);
                geo.AddBoxCollider(new Vector3(x, 0.4f, -d * 0.2f), new Vector3(1.8f, 0.8f, 0.9f), GameLayers.Prop);
            }
            for (int i = 0; i < 10; i++)
            {
                float x = -w * 0.45f + i * 0.42f;
                geo.Builder.AddBox(new Vector3(x, 1.4f, d * 0.35f), new Vector3(0.08f, 2.8f, 0.08f), Quaternion.identity, 0.8f, bsx);
            }
        }

        // ------------------------------------------------------------------
        private void Materialise(GameObject go, ChunkGeometry geo, string meshName)
        {
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = geo.BuildMesh(meshName);
            mr.sharedMaterials = geo.UsedMaterials();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (var b in geo.Boxes)
            {
                var host = new GameObject("Col");
                host.transform.SetParent(go.transform, false);
                host.transform.localPosition = b.Center;
                host.transform.localRotation = b.Rotation;
                host.layer = b.Layer;
                var bc = host.AddComponent<BoxCollider>();
                bc.size = b.Size;
                bc.isTrigger = b.IsTrigger;
            }

            foreach (var l in geo.Lights)
            {
                var lgo = new GameObject("Lamp");
                lgo.transform.SetParent(go.transform, false);
                lgo.transform.localPosition = l.Position;
                var light = lgo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = l.Color;
                light.range = l.Range;
                light.intensity = l.Intensity;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
            }
        }

        private void AddExitTrigger(GameObject go, Vector3 localPos, Vector3 size)
        {
            var t = new GameObject("ExitTrigger");
            t.transform.SetParent(go.transform, false);
            t.transform.localPosition = localPos;
            t.layer = GameLayers.Trigger;
            var bc = t.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size;
            t.AddComponent<InteriorExitTrigger>();
        }
    }

    /// <summary>Walk into the doorway to leave an interior.</summary>
    public class InteriorExitTrigger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != GameLayers.Player) return;
            Services.Game?.LeaveInterior();
        }
    }
}
