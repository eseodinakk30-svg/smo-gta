using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    public struct BuildingResult
    {
        public Vector3 Entrance;
        public Vector3 EntranceForward;
        public float Height;
        public bool CanHostShop;
        public bool CanHostProperty;
        public Vector3 Center;
    }

    /// <summary>
    /// Procedural architecture. Every building in San Monica is assembled here
    /// from a footprint plus the district profile, giving each area its own
    /// silhouette, palette and level of detail.
    /// </summary>
    public static class BuildingFactory
    {
        private static readonly Color[] ConcreteTints =
        {
            new Color(0.78f,0.76f,0.72f), new Color(0.68f,0.68f,0.70f), new Color(0.84f,0.82f,0.78f),
            new Color(0.60f,0.62f,0.64f), new Color(0.72f,0.66f,0.58f), new Color(0.88f,0.86f,0.84f)
        };

        private static readonly Color[] HouseTints =
        {
            new Color(0.90f,0.86f,0.76f), new Color(0.78f,0.82f,0.76f), new Color(0.86f,0.74f,0.62f),
            new Color(0.70f,0.76f,0.84f), new Color(0.92f,0.90f,0.86f), new Color(0.80f,0.68f,0.60f),
            new Color(0.62f,0.70f,0.66f)
        };

        private static readonly Color[] GlassTints =
        {
            new Color(0.55f,0.68f,0.75f), new Color(0.40f,0.52f,0.62f), new Color(0.62f,0.72f,0.70f),
            new Color(0.34f,0.44f,0.56f), new Color(0.70f,0.74f,0.78f)
        };

        public static BuildingResult Build(ChunkGeometry geo, WorldMap map, DistrictType district,
            DistrictProfile profile, Vector3 groundCenter, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            switch (district)
            {
                case DistrictType.Downtown:
                    return BuildTower(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Commercial:
                case DistrictType.University:
                    return BuildMidRise(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Marigold:
                case DistrictType.Residential:
                    return rng.Chance(0.62f)
                        ? BuildApartment(geo, profile, groundCenter, size, yaw, ref rng, lod)
                        : BuildHouse(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Wealthy:
                    return BuildVilla(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Suburb:
                    return BuildHouse(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Industrial:
                case DistrictType.Port:
                    return BuildWarehouse(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Airport:
                    return BuildTerminal(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Farmland:
                    return BuildFarm(geo, profile, groundCenter, size, yaw, ref rng, lod);
                case DistrictType.Marina:
                case DistrictType.Beach:
                    return BuildBeachBlock(geo, profile, groundCenter, size, yaw, ref rng, lod);
                default:
                    return BuildShack(geo, profile, groundCenter, size, yaw, ref rng, lod);
            }
        }

        // ------------------------------------------------------------------
        // Downtown towers
        // ------------------------------------------------------------------
        private static BuildingResult BuildTower(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int floors = rng.Range(p.minFloors, p.maxFloors + 1);
            float floorH = rng.Range(3.4f, 4.2f);
            float total = floors * floorH;

            Color glass = rng.Pick(GlassTints);
            var glassMat = MaterialLibrary.Windows(SurfaceKind.GlassCurtain, rng.Range(0, 3), glass, 0f);
            var frameMat = MaterialLibrary.Surface(SurfaceKind.Concrete, rng.Range(0, 3), rng.Pick(ConcreteTints), 0.25f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.Roof, 0, new Color(0.32f, 0.32f, 0.34f), 0.15f);

            // Podium with retail frontage.
            float podiumH = rng.Range(6f, 11f);
            Vector2 podium = size;
            geo.Builder.Tint = Color.white;
            AddBox(geo, glassMat, g + Vector3.up * (podiumH * 0.5f), new Vector3(podium.x, podiumH, podium.y), rot, 0.09f);
            AddBox(geo, frameMat, g + Vector3.up * (podiumH + 0.5f), new Vector3(podium.x + 0.7f, 1f, podium.y + 0.7f), rot, 0.2f);
            geo.AddBoxCollider(g + Vector3.up * (podiumH * 0.5f), new Vector3(podium.x, podiumH, podium.y), GameLayers.Building, rot);

            // Setback shaft, optionally stepped.
            int setbacks = rng.Range(1, 4);
            Vector2 cur = size * rng.Range(0.78f, 0.92f);
            float y = podiumH;
            float remaining = total;
            for (int s = 0; s < setbacks && remaining > 6f; s++)
            {
                float h = s == setbacks - 1 ? remaining : remaining * rng.Range(0.4f, 0.7f);
                h = Mathf.Max(6f, h);
                Vector3 c = g + Vector3.up * (y + h * 0.5f);
                AddBox(geo, glassMat, c, new Vector3(cur.x, h, cur.y), rot, 0.11f);
                // Slim vertical mullions read as structure from a distance.
                if (lod == 0)
                {
                    float step = Mathf.Max(3.5f, cur.x / 6f);
                    for (float o = -cur.x * 0.5f + step; o < cur.x * 0.5f - 0.1f; o += step)
                    {
                        AddBox(geo, frameMat, c + rot * new Vector3(o, 0f, cur.y * 0.5f + 0.06f), new Vector3(0.35f, h, 0.14f), rot, 0.4f);
                        AddBox(geo, frameMat, c + rot * new Vector3(o, 0f, -cur.y * 0.5f - 0.06f), new Vector3(0.35f, h, 0.14f), rot, 0.4f);
                    }
                }
                AddBox(geo, frameMat, g + Vector3.up * (y + h), new Vector3(cur.x + 0.5f, 0.6f, cur.y + 0.5f), rot, 0.25f);
                geo.AddBoxCollider(c, new Vector3(cur.x, h, cur.y), GameLayers.Building, rot);
                y += h;
                remaining -= h;
                cur *= rng.Range(0.72f, 0.88f);
            }

            // Roof plant, mast and aviation light.
            if (lod <= 1)
            {
                AddBox(geo, roofMat, g + Vector3.up * (y + 1.2f), new Vector3(cur.x * 0.45f, 2.4f, cur.y * 0.45f), rot, 0.3f);
                float mastH = rng.Range(6f, 22f);
                AddBox(geo, frameMat, g + Vector3.up * (y + 2.4f + mastH * 0.5f), new Vector3(0.5f, mastH, 0.5f), rot, 0.5f);
                var beacon = MaterialLibrary.Emissive(new Color(1f, 0.15f, 0.1f), 3.5f);
                AddBox(geo, beacon, g + Vector3.up * (y + 2.6f + mastH), new Vector3(0.7f, 0.7f, 0.7f), rot, 1f);
            }

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 1.6f),
                EntranceForward = -fwd,
                Height = y,
                CanHostShop = true,
                CanHostProperty = rng.Chance(0.25f),
                Center = g + Vector3.up * (y * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Commercial mid rise
        // ------------------------------------------------------------------
        private static BuildingResult BuildMidRise(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int floors = rng.Range(p.minFloors, p.maxFloors + 1);
            float floorH = rng.Range(3.2f, 3.8f);
            float total = floors * floorH;
            Color tint = rng.Pick(ConcreteTints);

            var wallMat = rng.Chance(0.35f)
                ? MaterialLibrary.Surface(SurfaceKind.Brick, rng.Range(0, 3), Color.white, 0.14f, 0f, 0.35f)
                : MaterialLibrary.Surface(SurfaceKind.Concrete, rng.Range(0, 4), tint, 0.2f);
            var winMat = MaterialLibrary.Windows(SurfaceKind.OfficeWindows, rng.Range(0, 4), rng.Pick(GlassTints), 0f);
            var shopMat = MaterialLibrary.Windows(SurfaceKind.ShopFront, rng.Range(0, 3), Color.white, 0f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.Roof, 0, new Color(0.30f, 0.30f, 0.32f), 0.12f);

            float shopH = 4.2f;
            AddBox(geo, shopMat, g + Vector3.up * (shopH * 0.5f), new Vector3(size.x, shopH, size.y), rot, 0.16f);

            Vector3 shaftC = g + Vector3.up * (shopH + (total - shopH) * 0.5f);
            Vector3 shaftS = new Vector3(size.x, Mathf.Max(3f, total - shopH), size.y);
            // Window bands on the long faces, solid wall on the short faces.
            AddBox(geo, wallMat, shaftC, shaftS, rot, 0.13f);
            if (lod == 0)
            {
                float bandInset = 0.12f;
                AddPanel(geo, winMat, shaftC + rot * new Vector3(0f, 0f, size.y * 0.5f + bandInset), new Vector2(size.x * 0.88f, shaftS.y * 0.82f), rot, 0.16f);
                AddPanel(geo, winMat, shaftC + rot * new Vector3(0f, 0f, -size.y * 0.5f - bandInset), new Vector2(size.x * 0.88f, shaftS.y * 0.82f), rot * Quaternion.Euler(0f, 180f, 0f), 0.16f);
                AddPanel(geo, winMat, shaftC + rot * new Vector3(size.x * 0.5f + bandInset, 0f, 0f), new Vector2(size.y * 0.86f, shaftS.y * 0.82f), rot * Quaternion.Euler(0f, 90f, 0f), 0.16f);
                AddPanel(geo, winMat, shaftC + rot * new Vector3(-size.x * 0.5f - bandInset, 0f, 0f), new Vector2(size.y * 0.86f, shaftS.y * 0.82f), rot * Quaternion.Euler(0f, -90f, 0f), 0.16f);
            }

            AddBox(geo, roofMat, g + Vector3.up * (total + 0.35f), new Vector3(size.x + 0.6f, 0.7f, size.y + 0.6f), rot, 0.25f);
            if (lod <= 1)
            {
                for (int i = 0; i < rng.Range(1, 4); i++)
                {
                    Vector2 o = rng.InsideUnitCircle() * Mathf.Min(size.x, size.y) * 0.3f;
                    AddBox(geo, roofMat, g + Vector3.up * (total + 1.4f) + rot * new Vector3(o.x, 0f, o.y),
                        new Vector3(rng.Range(1.4f, 2.8f), 1.4f, rng.Range(1.4f, 2.8f)), rot, 0.4f);
                }
            }

            geo.AddBoxCollider(g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), GameLayers.Building, rot);

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 1.4f),
                EntranceForward = -fwd,
                Height = total,
                CanHostShop = true,
                CanHostProperty = rng.Chance(0.35f),
                Center = g + Vector3.up * (total * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Apartment blocks
        // ------------------------------------------------------------------
        private static BuildingResult BuildApartment(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int floors = rng.Range(Mathf.Max(2, p.minFloors), p.maxFloors + 1);
            float floorH = 3.05f;
            float total = floors * floorH;

            bool brick = rng.Chance(0.55f);
            var wallMat = brick
                ? MaterialLibrary.Surface(SurfaceKind.Brick, rng.Range(0, 3), Color.white, 0.12f, 0f, 0.32f)
                : MaterialLibrary.Surface(SurfaceKind.Plaster, rng.Range(0, 4), rng.Pick(HouseTints), 0.16f);
            var winMat = MaterialLibrary.Windows(SurfaceKind.ResidentialWindows, rng.Range(0, 4), new Color(0.55f, 0.62f, 0.68f), 0f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.Roof, 1, new Color(0.28f, 0.27f, 0.28f), 0.1f);
            var trimMat = MaterialLibrary.Solid(new Color(0.85f, 0.84f, 0.80f), 0.15f, 0f, "trim");

            AddBox(geo, wallMat, g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), rot, 0.20f);
            geo.AddBoxCollider(g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), GameLayers.Building, rot);

            if (lod == 0)
            {
                // Window rows and balconies on the two long faces.
                for (int f = 0; f < floors; f++)
                {
                    float y = 1.4f + f * floorH;
                    AddPanel(geo, winMat, g + Vector3.up * y + rot * new Vector3(0f, 0f, size.y * 0.5f + 0.1f),
                        new Vector2(size.x * 0.86f, floorH * 0.55f), rot, 0.5f);
                    AddPanel(geo, winMat, g + Vector3.up * y + rot * new Vector3(0f, 0f, -size.y * 0.5f - 0.1f),
                        new Vector2(size.x * 0.86f, floorH * 0.55f), rot * Quaternion.Euler(0f, 180f, 0f), 0.5f);

                    if (f > 0 && rng.Chance(0.5f))
                    {
                        float bw = Mathf.Min(3.2f, size.x * 0.32f);
                        Vector3 bc = g + Vector3.up * (y - 0.5f) + rot * new Vector3(rng.Range(-size.x * 0.3f, size.x * 0.3f), 0f, size.y * 0.5f + 0.7f);
                        AddBox(geo, trimMat, bc, new Vector3(bw, 0.16f, 1.4f), rot, 0.6f);
                        AddBox(geo, trimMat, bc + Vector3.up * 0.5f + rot * new Vector3(0f, 0f, 0.65f), new Vector3(bw, 1f, 0.1f), rot, 0.8f);
                    }
                }
                // Ground floor entrance canopy.
                AddBox(geo, trimMat, g + Vector3.up * 2.6f + rot * new Vector3(0f, 0f, size.y * 0.5f + 0.8f),
                    new Vector3(3.4f, 0.22f, 1.8f), rot, 0.5f);
            }

            AddBox(geo, roofMat, g + Vector3.up * (total + 0.3f), new Vector3(size.x + 0.5f, 0.6f, size.y + 0.5f), rot, 0.25f);
            if (lod <= 1)
                AddBox(geo, roofMat, g + Vector3.up * (total + 1.6f) + rot * new Vector3(size.x * 0.25f, 0f, 0f),
                    new Vector3(2.2f, 2f, 2.2f), rot, 0.4f);

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 1.3f),
                EntranceForward = -fwd,
                Height = total,
                CanHostShop = rng.Chance(0.35f),
                CanHostProperty = true,
                Center = g + Vector3.up * (total * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Detached houses
        // ------------------------------------------------------------------
        private static BuildingResult BuildHouse(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            size = new Vector2(Mathf.Min(size.x, 16f), Mathf.Min(size.y, 14f));
            int floors = rng.Chance(0.42f) ? 2 : 1;
            float floorH = 2.9f;
            float total = floors * floorH;

            Color body = rng.Pick(HouseTints);
            var wallMat = MaterialLibrary.Surface(SurfaceKind.Plaster, rng.Range(0, 4), body, 0.14f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.Roof, rng.Range(0, 3), new Color(0.34f, 0.24f, 0.20f), 0.1f);
            var winMat = MaterialLibrary.Windows(SurfaceKind.ResidentialWindows, rng.Range(0, 4), new Color(0.5f, 0.58f, 0.64f), 0f);
            var woodMat = MaterialLibrary.Surface(SurfaceKind.Wood, rng.Range(0, 3), Color.white, 0.12f);

            AddBox(geo, wallMat, g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), rot, 0.22f);
            geo.AddBoxCollider(g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), GameLayers.Building, rot);

            // Gable roof from two tilted slabs.
            float roofH = rng.Range(1.6f, 2.8f);
            float slope = Mathf.Atan2(roofH, size.y * 0.5f) * Mathf.Rad2Deg;
            float slabLen = Mathf.Sqrt(roofH * roofH + size.y * size.y * 0.25f) + 0.4f;
            var lRot = rot * Quaternion.Euler(slope, 0f, 0f);
            var rRot = rot * Quaternion.Euler(-slope, 0f, 0f);
            AddBox(geo, roofMat, g + Vector3.up * (total + roofH * 0.5f) + rot * new Vector3(0f, 0f, -size.y * 0.25f), new Vector3(size.x + 0.8f, 0.22f, slabLen), lRot, 0.3f);
            AddBox(geo, roofMat, g + Vector3.up * (total + roofH * 0.5f) + rot * new Vector3(0f, 0f, size.y * 0.25f), new Vector3(size.x + 0.8f, 0.22f, slabLen), rRot, 0.3f);
            AddBox(geo, wallMat, g + Vector3.up * (total + roofH * 0.45f), new Vector3(size.x * 0.99f, roofH, 0.3f), rot, 0.35f);

            if (lod == 0)
            {
                for (int f = 0; f < floors; f++)
                {
                    float y = 1.35f + f * floorH;
                    AddPanel(geo, winMat, g + Vector3.up * y + rot * new Vector3(0f, 0f, size.y * 0.5f + 0.08f), new Vector2(size.x * 0.62f, 1.25f), rot, 0.8f);
                    AddPanel(geo, winMat, g + Vector3.up * y + rot * new Vector3(size.x * 0.5f + 0.08f, 0f, 0f), new Vector2(size.y * 0.45f, 1.15f), rot * Quaternion.Euler(0f, 90f, 0f), 0.8f);
                }
                // Porch.
                AddBox(geo, woodMat, g + Vector3.up * 0.1f + rot * new Vector3(0f, 0f, size.y * 0.5f + 1.1f), new Vector3(size.x * 0.55f, 0.2f, 2.2f), rot, 0.6f);
                AddBox(geo, woodMat, g + Vector3.up * 2.55f + rot * new Vector3(0f, 0f, size.y * 0.5f + 1.1f), new Vector3(size.x * 0.6f, 0.16f, 2.4f), rot, 0.6f);
                AddBox(geo, woodMat, g + Vector3.up * 1.3f + rot * new Vector3(-size.x * 0.24f, 0f, size.y * 0.5f + 2.0f), new Vector3(0.16f, 2.5f, 0.16f), rot, 1f);
                AddBox(geo, woodMat, g + Vector3.up * 1.3f + rot * new Vector3(size.x * 0.24f, 0f, size.y * 0.5f + 2.0f), new Vector3(0.16f, 2.5f, 0.16f), rot, 1f);
                // Chimney.
                if (rng.Chance(0.45f))
                    AddBox(geo, MaterialLibrary.Surface(SurfaceKind.Brick, 1, Color.white, 0.1f, 0f, 0.5f),
                        g + Vector3.up * (total + roofH + 0.6f) + rot * new Vector3(size.x * 0.3f, 0f, -size.y * 0.2f),
                        new Vector3(0.8f, 2.2f, 0.8f), rot, 0.6f);
            }

            // Garage.
            if (rng.Chance(0.55f) && lod <= 1)
            {
                Vector3 gp = g + rot * new Vector3(size.x * 0.5f + 3.0f, 0f, -size.y * 0.15f);
                AddBox(geo, wallMat, gp + Vector3.up * 1.4f, new Vector3(5.4f, 2.8f, 5.6f), rot, 0.25f);
                AddBox(geo, roofMat, gp + Vector3.up * 2.95f, new Vector3(5.8f, 0.3f, 6f), rot, 0.3f);
                AddBox(geo, MaterialLibrary.Solid(new Color(0.72f, 0.72f, 0.74f), 0.3f, 0.3f, "door"),
                    gp + Vector3.up * 1.2f + rot * new Vector3(0f, 0f, 2.85f), new Vector3(4.2f, 2.4f, 0.14f), rot, 0.8f);
                geo.AddBoxCollider(gp + Vector3.up * 1.4f, new Vector3(5.4f, 2.8f, 5.6f), GameLayers.Building, rot);
            }

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 2.4f),
                EntranceForward = -fwd,
                Height = total + roofH,
                CanHostShop = false,
                CanHostProperty = true,
                Center = g + Vector3.up * (total * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Crestwood villas
        // ------------------------------------------------------------------
        private static BuildingResult BuildVilla(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            size = new Vector2(Mathf.Clamp(size.x, 14f, 30f), Mathf.Clamp(size.y, 12f, 26f));
            float floorH = 3.3f;
            int floors = rng.Chance(0.55f) ? 2 : 1;
            float total = floors * floorH;

            var wallMat = MaterialLibrary.Surface(SurfaceKind.Plaster, 0, new Color(0.94f, 0.93f, 0.90f), 0.18f);
            var stoneMat = MaterialLibrary.Surface(SurfaceKind.Marble, 0, new Color(0.88f, 0.86f, 0.82f), 0.42f);
            var glassMat = MaterialLibrary.Windows(SurfaceKind.GlassCurtain, 1, new Color(0.42f, 0.55f, 0.62f), 0f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.Concrete, 2, new Color(0.80f, 0.78f, 0.74f), 0.2f);

            // Two overlapping volumes give the modern-villa massing.
            AddBox(geo, wallMat, g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), rot, 0.2f);
            geo.AddBoxCollider(g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), GameLayers.Building, rot);

            Vector3 wingOffset = rot * new Vector3(size.x * 0.42f, 0f, -size.y * 0.34f);
            AddBox(geo, stoneMat, g + wingOffset + Vector3.up * (floorH * 0.5f), new Vector3(size.x * 0.55f, floorH, size.y * 0.6f), rot, 0.22f);
            geo.AddBoxCollider(g + wingOffset + Vector3.up * (floorH * 0.5f), new Vector3(size.x * 0.55f, floorH, size.y * 0.6f), GameLayers.Building, rot);

            if (lod == 0)
            {
                AddPanel(geo, glassMat, g + Vector3.up * (total * 0.5f) + rot * new Vector3(0f, 0f, size.y * 0.5f + 0.09f),
                    new Vector2(size.x * 0.7f, total * 0.7f), rot, 0.22f);
                AddPanel(geo, glassMat, g + Vector3.up * (total * 0.5f) + rot * new Vector3(-size.x * 0.5f - 0.09f, 0f, 0f),
                    new Vector2(size.y * 0.6f, total * 0.6f), rot * Quaternion.Euler(0f, -90f, 0f), 0.22f);
            }

            AddBox(geo, roofMat, g + Vector3.up * (total + 0.25f), new Vector3(size.x + 1.6f, 0.5f, size.y + 1.6f), rot, 0.25f);

            // Pool and terrace.
            if (lod <= 1)
            {
                Vector3 pool = g + rot * new Vector3(0f, 0f, size.y * 0.5f + 7f);
                AddBox(geo, stoneMat, pool + Vector3.up * 0.06f, new Vector3(12f, 0.12f, 9f), rot, 0.3f);
                AddBox(geo, MaterialLibrary.Transparent(new Color(0.2f, 0.55f, 0.65f, 0.75f), 0.95f),
                    pool + Vector3.up * 0.14f, new Vector3(8f, 0.1f, 5.2f), rot, 0.5f);
            }

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 2.6f),
                EntranceForward = -fwd,
                Height = total,
                CanHostShop = false,
                CanHostProperty = true,
                Center = g + Vector3.up * (total * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Industrial sheds and port warehouses
        // ------------------------------------------------------------------
        private static BuildingResult BuildWarehouse(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            float h = rng.Range(7f, 14f);
            Color panel = new Color(0.62f, 0.64f, 0.66f) * rng.Range(0.8f, 1.1f);
            var wallMat = rng.Chance(0.5f)
                ? MaterialLibrary.Surface(SurfaceKind.MetalPanel, rng.Range(0, 3), panel, 0.30f, 0.35f)
                : MaterialLibrary.Surface(SurfaceKind.Concrete, 3, new Color(0.66f, 0.65f, 0.62f), 0.18f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 1, new Color(0.44f, 0.46f, 0.48f), 0.28f, 0.4f);
            var doorMat = MaterialLibrary.Solid(new Color(0.30f, 0.34f, 0.40f), 0.25f, 0.4f, "rolldoor");

            AddBox(geo, wallMat, g + Vector3.up * (h * 0.5f), new Vector3(size.x, h, size.y), rot, 0.10f);
            geo.AddBoxCollider(g + Vector3.up * (h * 0.5f), new Vector3(size.x, h, size.y), GameLayers.Building, rot);
            AddBox(geo, roofMat, g + Vector3.up * (h + 0.3f), new Vector3(size.x + 0.8f, 0.6f, size.y + 0.8f), rot, 0.2f);

            if (lod <= 1)
            {
                int doors = Mathf.Clamp(Mathf.FloorToInt(size.x / 8f), 1, 5);
                for (int i = 0; i < doors; i++)
                {
                    float ox = (-0.5f + (i + 0.5f) / doors) * size.x * 0.9f;
                    AddBox(geo, doorMat, g + Vector3.up * 2.2f + rot * new Vector3(ox, 0f, size.y * 0.5f + 0.09f),
                        new Vector3(5f, 4.4f, 0.16f), rot, 0.4f);
                }
                // Roof vents and pipes.
                for (int i = 0; i < rng.Range(2, 6); i++)
                {
                    Vector2 o = new Vector2(rng.Range(-size.x * 0.4f, size.x * 0.4f), rng.Range(-size.y * 0.4f, size.y * 0.4f));
                    AddBox(geo, roofMat, g + Vector3.up * (h + 1.1f) + rot * new Vector3(o.x, 0f, o.y),
                        new Vector3(1.2f, 1.2f, 1.2f), rot, 0.5f);
                }
            }

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 2f),
                EntranceForward = -fwd,
                Height = h,
                CanHostShop = rng.Chance(0.2f),
                CanHostProperty = rng.Chance(0.3f),
                Center = g + Vector3.up * (h * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Airport terminals and hangars
        // ------------------------------------------------------------------
        private static BuildingResult BuildTerminal(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            bool hangar = rng.Chance(0.45f);
            float h = hangar ? rng.Range(14f, 22f) : rng.Range(10f, 18f);

            var glassMat = MaterialLibrary.Windows(SurfaceKind.GlassCurtain, 2, new Color(0.55f, 0.68f, 0.74f), 0f);
            var panelMat = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 2, new Color(0.74f, 0.76f, 0.78f), 0.34f, 0.4f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 0, new Color(0.56f, 0.58f, 0.60f), 0.3f, 0.35f);

            AddBox(geo, hangar ? panelMat : glassMat, g + Vector3.up * (h * 0.5f), new Vector3(size.x, h, size.y), rot, 0.09f);
            geo.AddBoxCollider(g + Vector3.up * (h * 0.5f), new Vector3(size.x, h, size.y), GameLayers.Building, rot);
            AddBox(geo, roofMat, g + Vector3.up * (h + 0.4f), new Vector3(size.x + 2.2f, 0.8f, size.y + 2.2f), rot, 0.18f);

            if (hangar && lod <= 1)
            {
                AddBox(geo, MaterialLibrary.Solid(new Color(0.38f, 0.40f, 0.44f), 0.3f, 0.4f, "hangar"),
                    g + Vector3.up * (h * 0.42f) + rot * new Vector3(0f, 0f, size.y * 0.5f + 0.12f),
                    new Vector3(size.x * 0.8f, h * 0.78f, 0.2f), rot, 0.2f);
            }

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 3f),
                EntranceForward = -fwd,
                Height = h,
                CanHostShop = !hangar,
                CanHostProperty = false,
                Center = g + Vector3.up * (h * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Farms
        // ------------------------------------------------------------------
        private static BuildingResult BuildFarm(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            bool barn = rng.Chance(0.5f);
            var woodMat = MaterialLibrary.Surface(SurfaceKind.Wood, rng.Range(0, 3),
                barn ? new Color(0.62f, 0.20f, 0.16f) : new Color(0.90f, 0.88f, 0.82f), 0.12f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 1, new Color(0.42f, 0.44f, 0.46f), 0.25f, 0.3f);

            float h = barn ? rng.Range(7f, 10f) : rng.Range(3.2f, 6f);
            Vector2 s = barn ? new Vector2(Mathf.Min(size.x, 18f), Mathf.Min(size.y, 26f)) : new Vector2(Mathf.Min(size.x, 13f), Mathf.Min(size.y, 11f));

            AddBox(geo, woodMat, g + Vector3.up * (h * 0.5f), new Vector3(s.x, h, s.y), rot, 0.25f);
            geo.AddBoxCollider(g + Vector3.up * (h * 0.5f), new Vector3(s.x, h, s.y), GameLayers.Building, rot);

            float roofH = barn ? 3.4f : 2.2f;
            float slope = Mathf.Atan2(roofH, s.x * 0.5f) * Mathf.Rad2Deg;
            float slabLen = Mathf.Sqrt(roofH * roofH + s.x * s.x * 0.25f) + 0.3f;
            AddBox(geo, roofMat, g + Vector3.up * (h + roofH * 0.5f) + rot * new Vector3(-s.x * 0.25f, 0f, 0f),
                new Vector3(slabLen, 0.2f, s.y + 0.6f), rot * Quaternion.Euler(0f, 0f, -slope), 0.3f);
            AddBox(geo, roofMat, g + Vector3.up * (h + roofH * 0.5f) + rot * new Vector3(s.x * 0.25f, 0f, 0f),
                new Vector3(slabLen, 0.2f, s.y + 0.6f), rot * Quaternion.Euler(0f, 0f, slope), 0.3f);

            // Grain silo next to barns.
            if (barn && lod <= 1)
            {
                Vector3 silo = g + rot * new Vector3(s.x * 0.5f + 5f, 0f, 0f);
                var siloMat = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 0, new Color(0.80f, 0.80f, 0.78f), 0.3f, 0.4f);
                geo.Builder.AddCylinder(silo + Vector3.up * 6f, 3.2f, 12f, 12, geo.Sub(siloMat), true, 0.25f);
                geo.AddBoxCollider(silo + Vector3.up * 6f, new Vector3(6.2f, 12f, 6.2f), GameLayers.Building);
            }

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (s.y * 0.5f + 2f),
                EntranceForward = -fwd,
                Height = h + roofH,
                CanHostShop = false,
                CanHostProperty = !barn,
                Center = g + Vector3.up * (h * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Beach / marina low rise
        // ------------------------------------------------------------------
        private static BuildingResult BuildBeachBlock(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int floors = rng.Range(1, 5);
            float floorH = 3.1f;
            float total = floors * floorH;

            Color pastel = new Color(rng.Range(0.82f, 1f), rng.Range(0.80f, 0.98f), rng.Range(0.76f, 0.94f));
            var wallMat = MaterialLibrary.Surface(SurfaceKind.Plaster, rng.Range(0, 4), pastel, 0.16f);
            var trimMat = MaterialLibrary.Solid(new Color(0.30f, 0.62f, 0.72f), 0.2f, 0f, "beachtrim");
            var winMat = MaterialLibrary.Windows(SurfaceKind.ResidentialWindows, rng.Range(0, 4), new Color(0.55f, 0.68f, 0.72f), 0f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.Concrete, 1, new Color(0.86f, 0.84f, 0.80f), 0.2f);

            AddBox(geo, wallMat, g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), rot, 0.2f);
            geo.AddBoxCollider(g + Vector3.up * (total * 0.5f), new Vector3(size.x, total, size.y), GameLayers.Building, rot);
            AddBox(geo, roofMat, g + Vector3.up * (total + 0.25f), new Vector3(size.x + 0.8f, 0.5f, size.y + 0.8f), rot, 0.25f);

            if (lod == 0)
            {
                for (int f = 0; f < floors; f++)
                {
                    float y = 1.4f + f * floorH;
                    AddPanel(geo, winMat, g + Vector3.up * y + rot * new Vector3(0f, 0f, size.y * 0.5f + 0.09f),
                        new Vector2(size.x * 0.8f, 1.4f), rot, 0.6f);
                    if (f > 0)
                        AddBox(geo, trimMat, g + Vector3.up * (y - 0.9f) + rot * new Vector3(0f, 0f, size.y * 0.5f + 0.6f),
                            new Vector3(size.x * 0.9f, 0.9f, 0.1f), rot, 0.8f);
                }
            }

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (size.y * 0.5f + 1.6f),
                EntranceForward = -fwd,
                Height = total,
                CanHostShop = true,
                CanHostProperty = rng.Chance(0.5f),
                Center = g + Vector3.up * (total * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Rural shacks / cabins
        // ------------------------------------------------------------------
        private static BuildingResult BuildShack(ChunkGeometry geo, DistrictProfile p, Vector3 g, Vector2 size, float yaw, ref Rng rng, int lod)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector2 s = new Vector2(Mathf.Min(size.x, 9f), Mathf.Min(size.y, 8f));
            float h = rng.Range(2.6f, 3.6f);
            var wallMat = MaterialLibrary.Surface(SurfaceKind.Wood, rng.Range(0, 3), new Color(0.55f, 0.45f, 0.34f), 0.1f);
            var roofMat = MaterialLibrary.Surface(SurfaceKind.RustedMetal, 0, new Color(0.60f, 0.56f, 0.52f), 0.2f, 0.3f);

            AddBox(geo, wallMat, g + Vector3.up * (h * 0.5f), new Vector3(s.x, h, s.y), rot, 0.3f);
            AddBox(geo, roofMat, g + Vector3.up * (h + 0.2f), new Vector3(s.x + 0.7f, 0.2f, s.y + 0.7f), rot * Quaternion.Euler(6f, 0f, 0f), 0.35f);
            geo.AddBoxCollider(g + Vector3.up * (h * 0.5f), new Vector3(s.x, h, s.y), GameLayers.Building, rot);

            Vector3 fwd = rot * Vector3.forward;
            return new BuildingResult
            {
                Entrance = g + fwd * (s.y * 0.5f + 1.5f),
                EntranceForward = -fwd,
                Height = h,
                CanHostShop = false,
                CanHostProperty = false,
                Center = g + Vector3.up * (h * 0.5f)
            };
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static void AddBox(ChunkGeometry geo, Material m, Vector3 center, Vector3 size, Quaternion rot, float uvScale)
        {
            geo.Builder.AddBox(center, size, rot, uvScale, geo.Sub(m));
        }

        /// <summary>Single quad facing +Z of the given rotation - used for window bands.</summary>
        private static void AddPanel(ChunkGeometry geo, Material m, Vector3 center, Vector2 size, Quaternion rot, float uvScale)
        {
            Vector3 r = rot * Vector3.right * (size.x * 0.5f);
            Vector3 u = rot * Vector3.up * (size.y * 0.5f);
            geo.Builder.AddQuad(center - r - u, center + r - u, center + r + u, center - r + u,
                new Vector2(size.x, size.y) * uvScale, geo.Sub(m));
        }
    }
}
