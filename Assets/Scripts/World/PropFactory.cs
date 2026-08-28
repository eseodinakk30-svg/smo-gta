using UnityEngine;
using SanMonica.Core;
using SanMonica.Utils;

namespace SanMonica.World
{
    public enum TreeKind { Broadleaf, Pine, Palm, Bush, DeadTree }

    /// <summary>
    /// Street furniture, vegetation and industrial set dressing. Props are baked
    /// into the chunk mesh so a street full of lamps, trees and bins costs
    /// nothing extra in draw calls.
    /// </summary>
    public static class PropFactory
    {
        public static void StreetLamp(ChunkGeometry geo, Vector3 basePos, float rotY, bool tall = false)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            float h = tall ? 11f : 7.4f;
            var metal = MaterialLibrary.Solid(new Color(0.26f, 0.27f, 0.29f), 0.4f, 0.5f, "lamp");
            var glow = MaterialLibrary.Emissive(new Color(1f, 0.88f, 0.62f), 2.2f);

            geo.Builder.AddCylinder(basePos + Vector3.up * (h * 0.5f), 0.11f, h, 6, geo.Sub(metal), false, 0.5f);
            geo.Builder.AddBox(basePos + Vector3.up * h + rot * new Vector3(0f, 0f, 0.9f), new Vector3(0.14f, 0.14f, 1.9f), rot, 0.5f, geo.Sub(metal));
            geo.Builder.AddBox(basePos + Vector3.up * (h - 0.15f) + rot * new Vector3(0f, 0f, 1.75f), new Vector3(0.42f, 0.18f, 0.9f), rot, 0.6f, geo.Sub(glow));
            geo.AddBoxCollider(basePos + Vector3.up * (h * 0.5f), new Vector3(0.3f, h, 0.3f), GameLayers.Prop);
            geo.AddLight(basePos + Vector3.up * (h - 0.35f) + rot * new Vector3(0f, 0f, 1.75f),
                new Color(1f, 0.87f, 0.68f), tall ? 22f : 16f, 2.4f);
        }

        public static void TrafficLight(ChunkGeometry geo, Vector3 basePos, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var metal = MaterialLibrary.Solid(new Color(0.20f, 0.22f, 0.22f), 0.35f, 0.5f, "tl");
            geo.Builder.AddCylinder(basePos + Vector3.up * 2.6f, 0.10f, 5.2f, 6, geo.Sub(metal), false, 0.5f);
            geo.Builder.AddBox(basePos + Vector3.up * 5.1f + rot * new Vector3(0f, 0f, 1.6f), new Vector3(0.12f, 0.12f, 3.2f), rot, 0.5f, geo.Sub(metal));
            Vector3 head = basePos + Vector3.up * 4.75f + rot * new Vector3(0f, 0f, 3.0f);
            geo.Builder.AddBox(head, new Vector3(0.34f, 1.0f, 0.30f), rot, 0.6f, geo.Sub(metal));
            geo.AddBoxCollider(basePos + Vector3.up * 2.6f, new Vector3(0.3f, 5.2f, 0.3f), GameLayers.Prop);
        }

        public static void Tree(ChunkGeometry geo, Vector3 basePos, ref Rng rng, TreeKind kind, int lod)
        {
            int seg = lod == 0 ? 7 : 5;
            switch (kind)
            {
                case TreeKind.Palm:
                {
                    float h = rng.Range(6f, 12f);
                    var trunk = MaterialLibrary.Surface(SurfaceKind.Wood, 2, new Color(0.52f, 0.44f, 0.32f), 0.1f);
                    var leaf = MaterialLibrary.Foliage(new Color(0.30f, 0.52f, 0.22f), 1);
                    float lean = rng.Range(-7f, 7f);
                    var lr = Quaternion.Euler(lean, rng.Value * 360f, lean * 0.5f);
                    geo.Builder.AddCylinder(basePos + lr * Vector3.up * (h * 0.5f), 0.26f, h, seg, geo.Sub(trunk), false, 0.4f);
                    int fronds = lod == 0 ? 8 : 5;
                    for (int i = 0; i < fronds; i++)
                    {
                        float a = i / (float)fronds * 360f;
                        var fr = Quaternion.Euler(rng.Range(18f, 42f), a, 0f);
                        Vector3 top = basePos + lr * Vector3.up * h;
                        geo.Builder.AddBox(top + fr * new Vector3(0f, 0f, 1.9f), new Vector3(0.8f, 0.06f, 3.8f), fr, 0.5f, geo.Sub(leaf));
                    }
                    geo.AddBoxCollider(basePos + Vector3.up * (h * 0.5f), new Vector3(0.6f, h, 0.6f), GameLayers.Foliage);
                    break;
                }
                case TreeKind.Pine:
                {
                    float h = rng.Range(9f, 20f);
                    var trunk = MaterialLibrary.Surface(SurfaceKind.Wood, 1, new Color(0.34f, 0.26f, 0.18f), 0.08f);
                    var leaf = MaterialLibrary.Foliage(new Color(0.16f, 0.34f, 0.18f) * rng.Range(0.85f, 1.15f), 0);
                    geo.Builder.AddCylinder(basePos + Vector3.up * (h * 0.32f), 0.28f, h * 0.64f, seg, geo.Sub(trunk), false, 0.4f);
                    int tiers = lod == 0 ? 4 : 2;
                    for (int i = 0; i < tiers; i++)
                    {
                        float t = i / (float)tiers;
                        float y = h * (0.32f + t * 0.55f);
                        float r = Mathf.Lerp(2.9f, 0.7f, t) * rng.Range(0.9f, 1.1f);
                        geo.Builder.AddTaperedBox(basePos + Vector3.up * y, new Vector3(r * 2f, h * 0.22f, r * 2f), 0.2f, 0.2f, Quaternion.identity, geo.Sub(leaf), 0.3f);
                    }
                    geo.AddBoxCollider(basePos + Vector3.up * (h * 0.4f), new Vector3(0.7f, h * 0.8f, 0.7f), GameLayers.Foliage);
                    break;
                }
                case TreeKind.Bush:
                {
                    float r = rng.Range(0.7f, 1.7f);
                    var leaf = MaterialLibrary.Foliage(new Color(0.24f, 0.40f, 0.20f) * rng.Range(0.85f, 1.2f), 2);
                    geo.Builder.AddSphere(basePos + Vector3.up * r * 0.8f, r, seg, Mathf.Max(3, seg / 2), geo.Sub(leaf));
                    break;
                }
                case TreeKind.DeadTree:
                {
                    float h = rng.Range(4f, 8f);
                    var trunk = MaterialLibrary.Surface(SurfaceKind.Wood, 0, new Color(0.42f, 0.36f, 0.30f), 0.08f);
                    int s = geo.Sub(trunk);
                    geo.Builder.AddCylinder(basePos + Vector3.up * (h * 0.5f), 0.22f, h, 5, s, false, 0.4f);
                    for (int i = 0; i < 3; i++)
                    {
                        var br = Quaternion.Euler(rng.Range(30f, 60f), rng.Value * 360f, 0f);
                        geo.Builder.AddBox(basePos + Vector3.up * (h * rng.Range(0.6f, 0.9f)) + br * new Vector3(0f, 0f, 1.1f),
                            new Vector3(0.14f, 0.14f, 2.2f), br, 0.5f, s);
                    }
                    geo.AddBoxCollider(basePos + Vector3.up * (h * 0.5f), new Vector3(0.5f, h, 0.5f), GameLayers.Foliage);
                    break;
                }
                default:
                {
                    float h = rng.Range(5f, 13f);
                    var trunk = MaterialLibrary.Surface(SurfaceKind.Wood, 1, new Color(0.38f, 0.29f, 0.20f), 0.08f);
                    var leaf = MaterialLibrary.Foliage(new Color(0.26f, 0.45f, 0.20f) * rng.Range(0.82f, 1.18f), 0);
                    geo.Builder.AddCylinder(basePos + Vector3.up * (h * 0.35f), 0.30f, h * 0.7f, seg, geo.Sub(trunk), false, 0.4f);
                    float cr = h * rng.Range(0.28f, 0.40f);
                    int blobs = lod == 0 ? 3 : 1;
                    int ls = geo.Sub(leaf);
                    for (int i = 0; i < blobs; i++)
                    {
                        Vector2 o = rng.InsideUnitCircle() * cr * 0.45f;
                        geo.Builder.AddSphere(basePos + Vector3.up * (h * 0.78f) + new Vector3(o.x, rng.Range(-0.4f, 0.6f), o.y),
                            cr * rng.Range(0.65f, 1f), seg, Mathf.Max(3, seg / 2), ls);
                    }
                    geo.AddBoxCollider(basePos + Vector3.up * (h * 0.4f), new Vector3(0.8f, h * 0.8f, 0.8f), GameLayers.Foliage);
                    break;
                }
            }
        }

        public static void Bench(ChunkGeometry geo, Vector3 p, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var wood = MaterialLibrary.Surface(SurfaceKind.Wood, 0, new Color(0.48f, 0.34f, 0.22f), 0.15f);
            var metal = MaterialLibrary.Solid(new Color(0.22f, 0.24f, 0.26f), 0.4f, 0.5f, "bench");
            geo.Builder.AddBox(p + Vector3.up * 0.45f, new Vector3(1.9f, 0.09f, 0.55f), rot, 0.8f, geo.Sub(wood));
            geo.Builder.AddBox(p + Vector3.up * 0.78f + rot * new Vector3(0f, 0f, -0.24f), new Vector3(1.9f, 0.55f, 0.08f), rot, 0.8f, geo.Sub(wood));
            geo.Builder.AddBox(p + Vector3.up * 0.22f + rot * new Vector3(-0.8f, 0f, 0f), new Vector3(0.08f, 0.45f, 0.5f), rot, 1f, geo.Sub(metal));
            geo.Builder.AddBox(p + Vector3.up * 0.22f + rot * new Vector3(0.8f, 0f, 0f), new Vector3(0.08f, 0.45f, 0.5f), rot, 1f, geo.Sub(metal));
            geo.AddBoxCollider(p + Vector3.up * 0.45f, new Vector3(2f, 0.9f, 0.6f), GameLayers.Prop, rot);
        }

        public static void Bin(ChunkGeometry geo, Vector3 p, ref Rng rng)
        {
            var m = MaterialLibrary.Solid(new Color(0.22f, 0.30f, 0.24f), 0.3f, 0.2f, "bin");
            geo.Builder.AddCylinder(p + Vector3.up * 0.5f, 0.34f, 1f, 8, geo.Sub(m), true, 0.6f);
            geo.AddBoxCollider(p + Vector3.up * 0.5f, new Vector3(0.7f, 1f, 0.7f), GameLayers.Prop);
        }

        public static void Hydrant(ChunkGeometry geo, Vector3 p)
        {
            var m = MaterialLibrary.Solid(new Color(0.75f, 0.14f, 0.10f), 0.35f, 0.2f, "hyd");
            int s = geo.Sub(m);
            geo.Builder.AddCylinder(p + Vector3.up * 0.35f, 0.16f, 0.7f, 8, s, true, 1f);
            geo.Builder.AddSphere(p + Vector3.up * 0.72f, 0.17f, 7, 4, s);
            geo.AddBoxCollider(p + Vector3.up * 0.4f, new Vector3(0.4f, 0.8f, 0.4f), GameLayers.Prop);
        }

        public static void ParkingMeter(ChunkGeometry geo, Vector3 p, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var m = MaterialLibrary.Solid(new Color(0.30f, 0.32f, 0.34f), 0.4f, 0.5f, "meter");
            geo.Builder.AddCylinder(p + Vector3.up * 0.55f, 0.05f, 1.1f, 6, geo.Sub(m), false, 1f);
            geo.Builder.AddBox(p + Vector3.up * 1.22f, new Vector3(0.18f, 0.32f, 0.14f), rot, 1f, geo.Sub(m));
        }

        public static void BusStop(ChunkGeometry geo, Vector3 p, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var metal = MaterialLibrary.Solid(new Color(0.30f, 0.32f, 0.36f), 0.4f, 0.6f, "stop");
            var glass = MaterialLibrary.Transparent(new Color(0.7f, 0.8f, 0.85f, 0.30f));
            int ms = geo.Sub(metal);
            geo.Builder.AddBox(p + Vector3.up * 2.5f, new Vector3(3.6f, 0.12f, 1.6f), rot, 0.5f, ms);
            geo.Builder.AddBox(p + Vector3.up * 1.25f + rot * new Vector3(-1.75f, 0f, 0f), new Vector3(0.1f, 2.5f, 1.5f), rot, 0.6f, ms);
            geo.Builder.AddBox(p + Vector3.up * 1.25f + rot * new Vector3(1.75f, 0f, 0f), new Vector3(0.1f, 2.5f, 1.5f), rot, 0.6f, ms);
            geo.Builder.AddBox(p + Vector3.up * 1.25f + rot * new Vector3(0f, 0f, -0.75f), new Vector3(3.4f, 2.4f, 0.06f), rot, 0.4f, geo.Sub(glass));
            geo.Builder.AddBox(p + Vector3.up * 0.5f + rot * new Vector3(0f, 0f, 0.2f), new Vector3(2.6f, 0.1f, 0.45f), rot, 0.8f, ms);
            geo.AddBoxCollider(p + Vector3.up * 1.3f, new Vector3(3.6f, 2.6f, 1.6f), GameLayers.Prop, rot);
        }

        /// <summary>Illuminated shop sign above an entrance.</summary>
        public static void ShopSign(ChunkGeometry geo, Vector3 p, float rotY, Color color)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var frame = MaterialLibrary.Solid(new Color(0.18f, 0.18f, 0.20f), 0.3f, 0.4f, "sign");
            var glow = MaterialLibrary.Emissive(color, 2.6f);
            geo.Builder.AddBox(p, new Vector3(3.4f, 0.9f, 0.18f), rot, 0.5f, geo.Sub(frame));
            geo.Builder.AddBox(p + rot * new Vector3(0f, 0f, 0.12f), new Vector3(3.0f, 0.62f, 0.06f), rot, 0.6f, geo.Sub(glow));
            geo.AddLight(p + rot * new Vector3(0f, -1f, 1.2f), color, 9f, 1.4f);
        }

        public static void Billboard(ChunkGeometry geo, Vector3 p, float rotY, ref Rng rng)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var metal = MaterialLibrary.Solid(new Color(0.28f, 0.29f, 0.31f), 0.35f, 0.5f, "bb");
            Color c = new Color(rng.Range(0.3f, 1f), rng.Range(0.3f, 1f), rng.Range(0.3f, 1f));
            var face = MaterialLibrary.Emissive(c, 1.1f);
            int ms = geo.Sub(metal);
            geo.Builder.AddCylinder(p + Vector3.up * 3f, 0.18f, 6f, 6, ms, false, 0.5f);
            geo.Builder.AddBox(p + Vector3.up * 7.2f, new Vector3(9.5f, 4.2f, 0.3f), rot, 0.3f, ms);
            geo.Builder.AddBox(p + Vector3.up * 7.2f + rot * new Vector3(0f, 0f, 0.2f), new Vector3(9f, 3.8f, 0.08f), rot, 0.3f, geo.Sub(face));
            geo.AddBoxCollider(p + Vector3.up * 3f, new Vector3(0.5f, 6f, 0.5f), GameLayers.Prop);
        }

        public static void Fence(ChunkGeometry geo, Vector3 a, Vector3 b, float height, bool chainLink)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            var mat = chainLink
                ? MaterialLibrary.Solid(new Color(0.55f, 0.57f, 0.58f), 0.4f, 0.6f, "chain")
                : MaterialLibrary.Surface(SurfaceKind.Wood, 0, new Color(0.60f, 0.50f, 0.38f), 0.1f);
            int s = geo.Sub(mat);
            Vector3 mid = (a + b) * 0.5f + Vector3.up * (height * 0.5f);
            geo.Builder.AddBox(mid, new Vector3(0.06f, height, len), rot, 0.6f, s);
            int posts = Mathf.Max(2, Mathf.RoundToInt(len / 2.4f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)posts);
                geo.Builder.AddBox(p + Vector3.up * (height * 0.5f), new Vector3(0.10f, height + 0.1f, 0.10f), rot, 1f, s);
            }
            geo.AddBoxCollider(mid, new Vector3(0.2f, height, len), GameLayers.Prop, rot);
        }

        public static void Wall(ChunkGeometry geo, Vector3 a, Vector3 b, float height, Material mat)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            Vector3 mid = (a + b) * 0.5f + Vector3.up * (height * 0.5f);
            geo.Builder.AddBox(mid, new Vector3(0.3f, height, len), rot, 0.3f, geo.Sub(mat));
            geo.AddBoxCollider(mid, new Vector3(0.35f, height, len), GameLayers.Building, rot);
        }

        public static void Container(ChunkGeometry geo, Vector3 p, float rotY, ref Rng rng)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            Color[] cols = {
                new Color(0.72f,0.22f,0.16f), new Color(0.16f,0.38f,0.58f), new Color(0.22f,0.48f,0.30f),
                new Color(0.82f,0.66f,0.16f), new Color(0.58f,0.58f,0.60f), new Color(0.36f,0.24f,0.52f)
            };
            var m = MaterialLibrary.Surface(SurfaceKind.Container, rng.Range(0, 3), rng.Pick(cols), 0.28f, 0.3f);
            Vector3 size = new Vector3(2.44f, 2.59f, rng.Chance(0.35f) ? 12.19f : 6.06f);
            geo.Builder.AddBox(p + Vector3.up * (size.y * 0.5f), size, rot, 0.28f, geo.Sub(m));
            geo.AddBoxCollider(p + Vector3.up * (size.y * 0.5f), size, GameLayers.Prop, rot);
        }

        public static void PortCrane(ChunkGeometry geo, Vector3 p, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var m = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 0, new Color(0.85f, 0.55f, 0.10f), 0.3f, 0.5f);
            int s = geo.Sub(m);
            float legH = 32f;
            for (int i = 0; i < 4; i++)
            {
                float ox = (i % 2 == 0 ? -1f : 1f) * 9f;
                float oz = (i < 2 ? -1f : 1f) * 11f;
                geo.Builder.AddBox(p + Vector3.up * (legH * 0.5f) + rot * new Vector3(ox, 0f, oz), new Vector3(1.2f, legH, 1.2f), rot, 0.2f, s);
                geo.AddBoxCollider(p + Vector3.up * (legH * 0.5f) + rot * new Vector3(ox, 0f, oz), new Vector3(1.4f, legH, 1.4f), GameLayers.Building, rot);
            }
            geo.Builder.AddBox(p + Vector3.up * (legH + 2f), new Vector3(20f, 3.2f, 26f), rot, 0.15f, s);
            geo.Builder.AddBox(p + Vector3.up * (legH + 6f) + rot * new Vector3(0f, 0f, 16f), new Vector3(3.4f, 3.4f, 52f), rot, 0.12f, s);
            geo.AddBoxCollider(p + Vector3.up * (legH + 2f), new Vector3(20f, 3.2f, 26f), GameLayers.Building, rot);
        }

        public static void PowerPylon(ChunkGeometry geo, Vector3 p)
        {
            var m = MaterialLibrary.Solid(new Color(0.48f, 0.50f, 0.52f), 0.35f, 0.6f, "pylon");
            int s = geo.Sub(m);
            float h = 34f;
            for (int i = 0; i < 4; i++)
            {
                float ox = (i % 2 == 0 ? -1f : 1f) * 3.2f;
                float oz = (i < 2 ? -1f : 1f) * 3.2f;
                var lean = Quaternion.Euler(-Mathf.Atan2(2.6f, h) * Mathf.Rad2Deg * (oz > 0 ? 1f : -1f), 0f,
                                             Mathf.Atan2(2.6f, h) * Mathf.Rad2Deg * (ox > 0 ? 1f : -1f));
                geo.Builder.AddBox(p + Vector3.up * (h * 0.5f) + new Vector3(ox * 0.6f, 0f, oz * 0.6f), new Vector3(0.35f, h, 0.35f), lean, 0.3f, s);
            }
            for (int i = 0; i < 3; i++)
            {
                float y = h * (0.55f + i * 0.16f);
                geo.Builder.AddBox(p + Vector3.up * y, new Vector3(12f - i * 2.4f, 0.28f, 0.28f), Quaternion.identity, 0.3f, s);
            }
            geo.AddBoxCollider(p + Vector3.up * (h * 0.5f), new Vector3(4f, h, 4f), GameLayers.Building);
        }

        public static void Rock(ChunkGeometry geo, Vector3 p, ref Rng rng, float scale)
        {
            var m = MaterialLibrary.Surface(SurfaceKind.Rock, rng.Range(0, 3), new Color(0.55f, 0.52f, 0.48f), 0.08f);
            var rot = Quaternion.Euler(rng.Range(-20f, 20f), rng.Value * 360f, rng.Range(-20f, 20f));
            geo.Builder.AddTaperedBox(p + Vector3.up * (scale * 0.35f),
                new Vector3(scale, scale * 0.8f, scale * rng.Range(0.7f, 1.3f)), rng.Range(0.4f, 0.8f), rng.Range(0.4f, 0.8f), rot, geo.Sub(m), 0.4f);
            geo.AddBoxCollider(p + Vector3.up * (scale * 0.35f), new Vector3(scale, scale * 0.8f, scale), GameLayers.Prop, rot);
        }

        public static void Barrier(ChunkGeometry geo, Vector3 p, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var m = MaterialLibrary.Solid(new Color(0.86f, 0.84f, 0.80f), 0.2f, 0f, "barrier");
            geo.Builder.AddTaperedBox(p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 3.2f), 0.5f, 1f, rot, geo.Sub(m), 0.5f);
            geo.AddBoxCollider(p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 3.2f), GameLayers.Prop, rot);
        }

        public static void TrafficCone(ChunkGeometry geo, Vector3 p)
        {
            var m = MaterialLibrary.Solid(new Color(0.92f, 0.36f, 0.06f), 0.25f, 0f, "cone");
            int s = geo.Sub(m);
            geo.Builder.AddBox(p + Vector3.up * 0.03f, new Vector3(0.42f, 0.06f, 0.42f), Quaternion.identity, 1f, s);
            geo.Builder.AddTaperedBox(p + Vector3.up * 0.3f, new Vector3(0.3f, 0.55f, 0.3f), 0.12f, 0.12f, Quaternion.identity, s, 1f);
        }

        public static void Dumpster(ChunkGeometry geo, Vector3 p, float rotY, ref Rng rng)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var m = MaterialLibrary.Surface(SurfaceKind.MetalPanel, 1, new Color(0.20f, 0.36f, 0.28f), 0.25f, 0.4f);
            geo.Builder.AddTaperedBox(p + Vector3.up * 0.65f, new Vector3(2.2f, 1.3f, 1.3f), 1.12f, 1.05f, rot, geo.Sub(m), 0.4f);
            geo.AddBoxCollider(p + Vector3.up * 0.65f, new Vector3(2.3f, 1.3f, 1.4f), GameLayers.Prop, rot);
        }

        public static void Pier(ChunkGeometry geo, Vector3 start, Vector3 end, float width, float deckY)
        {
            Vector3 d = end - start;
            float len = d.magnitude;
            if (len < 1f) return;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            var wood = MaterialLibrary.Surface(SurfaceKind.Wood, 0, new Color(0.52f, 0.44f, 0.36f), 0.1f);
            int s = geo.Sub(wood);
            Vector3 mid = (start + end) * 0.5f;
            mid.y = deckY;
            geo.Builder.AddBox(mid, new Vector3(width, 0.4f, len), rot, 0.4f, s);
            geo.AddBoxCollider(mid, new Vector3(width, 0.4f, len), GameLayers.Ground, rot);
            int piles = Mathf.Max(2, Mathf.RoundToInt(len / 6f));
            for (int i = 0; i <= piles; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, i / (float)piles);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    Vector3 pile = p + rot * new Vector3(sgn * width * 0.42f, 0f, 0f);
                    pile.y = deckY - 3f;
                    geo.Builder.AddCylinder(pile, 0.22f, 6f, 6, s, false, 0.5f);
                }
            }
        }

        public static void FuelPump(ChunkGeometry geo, Vector3 p, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var body = MaterialLibrary.Solid(new Color(0.90f, 0.88f, 0.84f), 0.35f, 0.2f, "pump");
            var trim = MaterialLibrary.Solid(new Color(0.85f, 0.20f, 0.14f), 0.4f, 0.2f, "pumptrim");
            geo.Builder.AddBox(p + Vector3.up * 0.15f, new Vector3(1.6f, 0.3f, 0.9f), rot, 0.6f, geo.Sub(trim));
            geo.Builder.AddBox(p + Vector3.up * 0.95f, new Vector3(0.5f, 1.5f, 0.7f), rot, 0.7f, geo.Sub(body));
            geo.AddBoxCollider(p + Vector3.up * 0.85f, new Vector3(1.6f, 1.8f, 0.9f), GameLayers.Prop, rot);
        }

        public static void Canopy(ChunkGeometry geo, Vector3 p, float rotY, Vector2 size, float height, Color color)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            var m = MaterialLibrary.Solid(color, 0.25f, 0.1f, "canopy");
            var post = MaterialLibrary.Solid(new Color(0.75f, 0.75f, 0.76f), 0.35f, 0.5f, "post");
            int ms = geo.Sub(m); int ps = geo.Sub(post);
            geo.Builder.AddBox(p + Vector3.up * height, new Vector3(size.x, 0.45f, size.y), rot, 0.2f, ms);
            for (int i = 0; i < 4; i++)
            {
                float ox = (i % 2 == 0 ? -1f : 1f) * (size.x * 0.5f - 0.8f);
                float oz = (i < 2 ? -1f : 1f) * (size.y * 0.5f - 0.8f);
                Vector3 c = p + rot * new Vector3(ox, 0f, oz) + Vector3.up * (height * 0.5f);
                geo.Builder.AddBox(c, new Vector3(0.3f, height, 0.3f), rot, 0.6f, ps);
                geo.AddBoxCollider(c, new Vector3(0.35f, height, 0.35f), GameLayers.Prop, rot);
            }
            geo.AddLight(p + Vector3.up * (height - 0.5f), new Color(1f, 0.95f, 0.85f), 16f, 2f);
        }
    }
}
