using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.Vehicles
{
    public struct VehicleVisual
    {
        public Mesh Mesh;
        public Material[] Materials;
        public Vector3[] WheelPositions;
        public float WheelRadius;
        public float WheelWidth;
        public Vector3[] SeatPositions;
        public Vector3[] HeadlightPositions;
        public Vector3[] TaillightPositions;
        public Vector3 ExhaustPosition;
        public Vector3 RotorPosition;
        public float RotorRadius;
        public Bounds LocalBounds;
    }

    /// <summary>
    /// Builds every vehicle body in the game from its data definition. Cars,
    /// bikes, boats, helicopters and planes all resolve to one mesh with the
    /// shared palette atlas materials, so a busy street stays cheap to draw.
    /// </summary>
    public static class VehicleMeshBuilder
    {
        private const int SubMatte = 0, SubGlossy = 1, SubMetal = 2, SubGlass = 3, SubEmissive = 4;

        public static VehicleVisual Build(VehicleDefinition def, Color paint, int seed)
        {
            var rng = new Rng(seed);
            var mb = new MeshBuilder(5);
            var v = new VehicleVisual { WheelRadius = def.wheelRadius, WheelWidth = def.wheelWidth };

            if (def.IsAircraft) BuildAircraft(mb, def, paint, ref rng, ref v);
            else if (def.IsWatercraft) BuildBoat(mb, def, paint, ref rng, ref v);
            else if (def.IsBike) BuildBike(mb, def, paint, ref rng, ref v);
            else BuildCar(mb, def, paint, ref rng, ref v);

            v.Mesh = mb.ToMesh("Vehicle_" + def.id);
            v.Materials = mb.FilterMaterials(PaletteAtlas.StandardSet);
            v.LocalBounds = v.Mesh.bounds;
            return v;
        }

        private static void Paint(MeshBuilder mb, int start, Color c)
            => mb.SetUVRange(start, mb.VertexCount, PaletteAtlas.UV(c));

        private static void Box(MeshBuilder mb, int sub, Color color, Vector3 center, Vector3 size, Quaternion rot)
        {
            int s = mb.VertexCount;
            mb.AddBox(center, size, rot, 0f, sub);
            Paint(mb, s, color);
        }

        private static void Taper(MeshBuilder mb, int sub, Color color, Vector3 center, Vector3 size, float tx, float tz, Quaternion rot)
        {
            int s = mb.VertexCount;
            mb.AddTaperedBox(center, size, tx, tz, rot, sub, 0f);
            Paint(mb, s, color);
        }

        // ------------------------------------------------------------------
        // Cars, SUVs, vans, trucks and buses
        // ------------------------------------------------------------------
        private static void BuildCar(MeshBuilder mb, VehicleDefinition def, Color paint, ref Rng rng, ref VehicleVisual v)
        {
            float L = def.length, W = def.width, H = def.height;
            float ride = def.rideHeight;
            float bodyH = H - ride;
            float cabinL = L * def.cabinLengthRatio;
            float cabinH = bodyH * def.cabinHeightRatio;
            float lowerH = bodyH - cabinH;

            Color trim = new Color(0.10f, 0.10f, 0.12f);
            Color glass = new Color(0.16f, 0.22f, 0.28f);
            Color chrome = new Color(0.78f, 0.80f, 0.84f);

            float baseY = ride + lowerH * 0.5f;

            // Lower body with a slight taper for a cast, moulded look.
            Taper(mb, SubGlossy, paint, new Vector3(0f, baseY, 0f), new Vector3(W, lowerH, L), 0.97f, 0.985f, Quaternion.identity);

            // Nose slope.
            if (def.noseSlope > 0.01f)
                Taper(mb, SubGlossy, paint, new Vector3(0f, ride + lowerH * 0.92f, L * 0.32f),
                    new Vector3(W * 0.96f, lowerH * 0.42f, L * 0.34f), 0.88f, 0.6f, Quaternion.Euler(-def.noseSlope * 34f, 0f, 0f));

            // Cabin.
            float cabinZ = def.hasBed ? -L * 0.08f : (def.hasCargoBox ? L * 0.24f : -L * 0.02f);
            Taper(mb, SubGlossy, paint, new Vector3(0f, ride + lowerH + cabinH * 0.5f, cabinZ),
                new Vector3(W * 0.94f, cabinH, cabinL), def.roofTaper, 0.86f, Quaternion.identity);

            // Glass: windscreen, rear window and side glazing.
            float glassInset = 0.03f;
            Taper(mb, SubGlass, glass, new Vector3(0f, ride + lowerH + cabinH * 0.52f, cabinZ + cabinL * 0.5f - glassInset),
                new Vector3(W * 0.86f, cabinH * 0.78f, 0.06f), def.roofTaper, 1f, Quaternion.identity);
            Taper(mb, SubGlass, glass, new Vector3(0f, ride + lowerH + cabinH * 0.52f, cabinZ - cabinL * 0.5f + glassInset),
                new Vector3(W * 0.84f, cabinH * 0.72f, 0.06f), def.roofTaper, 1f, Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
                Taper(mb, SubGlass, glass, new Vector3(side * W * 0.455f, ride + lowerH + cabinH * 0.55f, cabinZ),
                    new Vector3(0.05f, cabinH * 0.66f, cabinL * 0.86f), 1f, def.roofTaper, Quaternion.identity);

            // Pickup bed / cargo box.
            if (def.hasBed)
            {
                float bedL = L * 0.42f;
                float bedZ = -L * 0.5f + bedL * 0.5f + 0.1f;
                Box(mb, SubGlossy, paint, new Vector3(0f, ride + lowerH + 0.28f, bedZ), new Vector3(W * 0.94f, 0.56f, bedL), Quaternion.identity);
                Box(mb, SubMatte, trim, new Vector3(0f, ride + lowerH + 0.02f, bedZ), new Vector3(W * 0.84f, 0.06f, bedL * 0.9f), Quaternion.identity);
            }
            else if (def.hasCargoBox)
            {
                float boxL = L * (def.vehicleClass == VehicleClass.Van ? 0.56f : 0.62f);
                float boxZ = -L * 0.5f + boxL * 0.5f + 0.08f;
                float boxH = H - ride - 0.1f;
                Box(mb, SubGlossy, paint, new Vector3(0f, ride + boxH * 0.5f, boxZ), new Vector3(W * 0.99f, boxH, boxL), Quaternion.identity);
            }

            // Bus glazing runs the whole flank.
            if (def.vehicleClass == VehicleClass.Bus)
            {
                for (int side = -1; side <= 1; side += 2)
                    Box(mb, SubGlass, glass, new Vector3(side * W * 0.5f, ride + bodyH * 0.62f, 0f),
                        new Vector3(0.05f, bodyH * 0.34f, L * 0.86f), Quaternion.identity);
            }

            // Bumpers and grille.
            Box(mb, SubMetal, chrome, new Vector3(0f, ride + lowerH * 0.32f, L * 0.5f - 0.06f), new Vector3(W * 0.98f, lowerH * 0.26f, 0.16f), Quaternion.identity);
            Box(mb, SubMetal, chrome, new Vector3(0f, ride + lowerH * 0.32f, -L * 0.5f + 0.06f), new Vector3(W * 0.98f, lowerH * 0.24f, 0.16f), Quaternion.identity);
            Box(mb, SubMatte, trim, new Vector3(0f, ride + lowerH * 0.62f, L * 0.5f - 0.02f), new Vector3(W * 0.62f, lowerH * 0.30f, 0.08f), Quaternion.identity);

            // Lights.
            var headlights = new List<Vector3>();
            var taillights = new List<Vector3>();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 hl = new Vector3(side * W * 0.36f, ride + lowerH * 0.72f, L * 0.5f + 0.02f);
                Vector3 tl = new Vector3(side * W * 0.36f, ride + lowerH * 0.74f, -L * 0.5f - 0.02f);
                Box(mb, SubEmissive, new Color(1f, 0.96f, 0.86f), hl, new Vector3(W * 0.22f, lowerH * 0.20f, 0.06f), Quaternion.identity);
                Box(mb, SubEmissive, new Color(0.85f, 0.10f, 0.08f), tl, new Vector3(W * 0.20f, lowerH * 0.18f, 0.06f), Quaternion.identity);
                headlights.Add(hl);
                taillights.Add(tl);
            }
            v.HeadlightPositions = headlights.ToArray();
            v.TaillightPositions = taillights.ToArray();

            // Emergency light bar.
            if (def.hasSiren)
            {
                float roofY = ride + lowerH + cabinH + 0.08f;
                Box(mb, SubMatte, trim, new Vector3(0f, roofY, cabinZ), new Vector3(W * 0.72f, 0.12f, 0.28f), Quaternion.identity);
                Box(mb, SubEmissive, new Color(0.1f, 0.25f, 1f), new Vector3(-W * 0.22f, roofY + 0.02f, cabinZ), new Vector3(W * 0.24f, 0.13f, 0.24f), Quaternion.identity);
                Box(mb, SubEmissive, new Color(1f, 0.15f, 0.12f), new Vector3(W * 0.22f, roofY + 0.02f, cabinZ), new Vector3(W * 0.24f, 0.13f, 0.24f), Quaternion.identity);
            }

            if (def.hasRoofRack)
                for (int side = -1; side <= 1; side += 2)
                    Box(mb, SubMatte, trim, new Vector3(side * W * 0.32f, ride + lowerH + cabinH + 0.05f, cabinZ), new Vector3(0.07f, 0.07f, cabinL * 0.8f), Quaternion.identity);

            // Mirrors and door lines.
            for (int side = -1; side <= 1; side += 2)
            {
                Box(mb, SubMatte, trim, new Vector3(side * (W * 0.52f), ride + lowerH + cabinH * 0.55f, cabinZ + cabinL * 0.34f),
                    new Vector3(0.16f, 0.10f, 0.07f), Quaternion.identity);
                Box(mb, SubMatte, trim, new Vector3(side * (W * 0.5f + 0.005f), ride + lowerH * 0.55f, 0f),
                    new Vector3(0.02f, 0.05f, L * 0.7f), Quaternion.identity);
            }

            // Wheels.
            v.WheelPositions = BuildWheelPositions(def);
            v.ExhaustPosition = new Vector3(W * 0.28f, ride * 0.6f, -L * 0.5f - 0.05f);

            // Seats.
            var seats = new List<Vector3>();
            int rows = Mathf.Clamp(Mathf.CeilToInt(def.seats / 2f), 1, 6);
            for (int r = 0; r < rows; r++)
            {
                float z = cabinZ + cabinL * 0.28f - r * (cabinL * 0.55f / Mathf.Max(1, rows - 1 + 0.0001f));
                if (rows == 1) z = cabinZ;
                seats.Add(new Vector3(-W * 0.22f, ride + lowerH * 0.55f, z));
                if (seats.Count < def.seats) seats.Add(new Vector3(W * 0.22f, ride + lowerH * 0.55f, z));
                if (seats.Count >= def.seats) break;
            }
            v.SeatPositions = seats.ToArray();
        }

        public static Vector3[] BuildWheelPositions(VehicleDefinition def)
        {
            float halfBase = def.wheelbase * 0.5f;
            float halfTrack = def.track * 0.5f;
            float y = def.wheelRadius;

            if (def.IsBike)
                return new[] { new Vector3(0f, y, halfBase), new Vector3(0f, y, -halfBase) };

            if (def.wheelCount >= 6)
            {
                float rearOffset = def.wheelRadius * 2.4f;
                return new[]
                {
                    new Vector3(-halfTrack, y, halfBase),
                    new Vector3(halfTrack, y, halfBase),
                    new Vector3(-halfTrack, y, -halfBase + rearOffset * 0.5f),
                    new Vector3(halfTrack, y, -halfBase + rearOffset * 0.5f),
                    new Vector3(-halfTrack, y, -halfBase - rearOffset * 0.5f),
                    new Vector3(halfTrack, y, -halfBase - rearOffset * 0.5f)
                };
            }

            if (def.wheelCount == 3)
                return new[]
                {
                    new Vector3(0f, y, halfBase),
                    new Vector3(-halfTrack, y, -halfBase),
                    new Vector3(halfTrack, y, -halfBase)
                };

            return new[]
            {
                new Vector3(-halfTrack, y, halfBase),
                new Vector3(halfTrack, y, halfBase),
                new Vector3(-halfTrack, y, -halfBase),
                new Vector3(halfTrack, y, -halfBase)
            };
        }

        /// <summary>Standalone wheel mesh, shared by every vehicle of the same size.</summary>
        public static Mesh BuildWheel(float radius, float width, int segments = 12)
        {
            var mb = new MeshBuilder(5);
            var rot = Quaternion.Euler(0f, 0f, 90f);

            // Tyre: a cylinder lying on its side (X axis).
            int start = mb.VertexCount;
            AddOrientedCylinder(mb, SubMatte, Vector3.zero, radius, width, segments, rot);
            mb.SetUVRange(start, mb.VertexCount, PaletteAtlas.UV(new Color(0.07f, 0.07f, 0.08f)));

            start = mb.VertexCount;
            AddOrientedCylinder(mb, SubMetal, Vector3.zero, radius * 0.62f, width * 1.02f, segments, rot);
            mb.SetUVRange(start, mb.VertexCount, PaletteAtlas.UV(new Color(0.72f, 0.74f, 0.78f)));

            var mesh = mb.ToMesh("Wheel");
            return mesh;
        }

        public static Material[] WheelMaterials => new[] { PaletteAtlas.Matte, PaletteAtlas.Metal };

        private static void AddOrientedCylinder(MeshBuilder mb, int sub, Vector3 center, float radius, float height, int segments, Quaternion rot)
        {
            // Builds the cylinder directly in the rotated frame.
            float half = height * 0.5f;
            var ringTop = new Vector3[segments];
            var ringBottom = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ringTop[i] = center + rot * (dir * radius + Vector3.up * half);
                ringBottom[i] = center + rot * (dir * radius - Vector3.up * half);
            }
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                mb.AddQuad(ringBottom[i], ringBottom[n], ringTop[n], ringTop[i], Vector2.one, sub);
            }
            mb.AddTriangleFan(center + rot * (Vector3.up * half), rot * Vector3.up, ringTop, 0f, sub);
            var reversed = new Vector3[segments];
            for (int i = 0; i < segments; i++) reversed[i] = ringBottom[segments - 1 - i];
            mb.AddTriangleFan(center - rot * (Vector3.up * half), rot * Vector3.down, reversed, 0f, sub);
        }

        // ------------------------------------------------------------------
        private static void BuildBike(MeshBuilder mb, VehicleDefinition def, Color paint, ref Rng rng, ref VehicleVisual v)
        {
            float L = def.length, W = def.width, H = def.height;
            Color trim = new Color(0.10f, 0.10f, 0.12f);
            Color chrome = new Color(0.80f, 0.82f, 0.86f);

            float y = def.wheelRadius;
            Taper(mb, SubGlossy, paint, new Vector3(0f, y + 0.26f, 0.05f), new Vector3(W * 0.62f, 0.34f, L * 0.44f), 0.7f, 0.6f, Quaternion.identity);
            Box(mb, SubMatte, trim, new Vector3(0f, y + 0.42f, -L * 0.16f), new Vector3(W * 0.52f, 0.14f, L * 0.30f), Quaternion.identity);
            Box(mb, SubMetal, chrome, new Vector3(0f, y + 0.12f, -L * 0.05f), new Vector3(0.16f, 0.24f, L * 0.34f), Quaternion.identity);
            // Forks and bars.
            Box(mb, SubMetal, chrome, new Vector3(0f, y + 0.30f, L * 0.34f), new Vector3(0.10f, 0.62f, 0.10f), Quaternion.Euler(-18f, 0f, 0f));
            Box(mb, SubMatte, trim, new Vector3(0f, y + 0.62f, L * 0.30f), new Vector3(W * 1.05f, 0.06f, 0.06f), Quaternion.identity);
            // Headlight and tail light.
            Vector3 hl = new Vector3(0f, y + 0.50f, L * 0.40f);
            Vector3 tl = new Vector3(0f, y + 0.40f, -L * 0.40f);
            Box(mb, SubEmissive, new Color(1f, 0.96f, 0.86f), hl, new Vector3(0.20f, 0.16f, 0.06f), Quaternion.identity);
            Box(mb, SubEmissive, new Color(0.85f, 0.10f, 0.08f), tl, new Vector3(0.16f, 0.10f, 0.05f), Quaternion.identity);
            v.HeadlightPositions = new[] { hl };
            v.TaillightPositions = new[] { tl };
            // Exhaust.
            Box(mb, SubMetal, chrome, new Vector3(W * 0.22f, y * 0.7f, -L * 0.26f), new Vector3(0.10f, 0.10f, L * 0.34f), Quaternion.identity);

            v.WheelPositions = BuildWheelPositions(def);
            v.ExhaustPosition = new Vector3(W * 0.22f, y * 0.6f, -L * 0.44f);
            v.SeatPositions = def.seats > 1
                ? new[] { new Vector3(0f, y + 0.52f, -L * 0.05f), new Vector3(0f, y + 0.56f, -L * 0.24f) }
                : new[] { new Vector3(0f, y + 0.52f, -L * 0.05f) };
        }

        // ------------------------------------------------------------------
        private static void BuildBoat(MeshBuilder mb, VehicleDefinition def, Color paint, ref Rng rng, ref VehicleVisual v)
        {
            float L = def.length, W = def.width, H = def.height;
            Color deck = new Color(0.86f, 0.84f, 0.78f);
            Color trim = new Color(0.14f, 0.16f, 0.20f);

            // Hull: tapered downward and forward.
            Taper(mb, SubGlossy, paint, new Vector3(0f, -H * 0.18f, 0f), new Vector3(W, H * 0.6f, L), 1.15f, 1.02f, Quaternion.identity);
            Taper(mb, SubGlossy, paint, new Vector3(0f, -H * 0.06f, L * 0.36f), new Vector3(W * 0.92f, H * 0.5f, L * 0.30f), 0.25f, 0.2f, Quaternion.identity);
            // Deck.
            Box(mb, SubMatte, deck, new Vector3(0f, H * 0.10f, -L * 0.05f), new Vector3(W * 0.94f, 0.10f, L * 0.82f), Quaternion.identity);

            // Cabin / console.
            float cabinH = def.vehicleClass == VehicleClass.Yacht ? H * 0.55f : H * 0.30f;
            Taper(mb, SubGlossy, paint, new Vector3(0f, H * 0.14f + cabinH * 0.5f, -L * 0.10f),
                new Vector3(W * 0.72f, cabinH, L * (def.vehicleClass == VehicleClass.Yacht ? 0.44f : 0.20f)), 0.86f, 0.86f, Quaternion.identity);
            Box(mb, SubGlass, new Color(0.18f, 0.28f, 0.34f), new Vector3(0f, H * 0.16f + cabinH * 0.6f, -L * 0.10f + L * 0.10f),
                new Vector3(W * 0.62f, cabinH * 0.5f, 0.05f), Quaternion.identity);

            if (def.vehicleClass == VehicleClass.Yacht)
            {
                Taper(mb, SubGlossy, paint, new Vector3(0f, H * 0.14f + cabinH * 1.35f, -L * 0.16f),
                    new Vector3(W * 0.5f, cabinH * 0.7f, L * 0.24f), 0.85f, 0.85f, Quaternion.identity);
                Box(mb, SubMetal, new Color(0.85f, 0.86f, 0.88f), new Vector3(0f, H * 0.9f, -L * 0.2f), new Vector3(0.08f, H * 0.5f, 0.08f), Quaternion.identity);
            }

            Vector3 nav = new Vector3(0f, H * 0.3f, L * 0.42f);
            Box(mb, SubEmissive, new Color(1f, 1f, 0.9f), nav, new Vector3(0.12f, 0.12f, 0.06f), Quaternion.identity);
            v.HeadlightPositions = new[] { nav };
            v.TaillightPositions = new[] { new Vector3(0f, H * 0.28f, -L * 0.46f) };
            v.WheelPositions = new Vector3[0];
            v.ExhaustPosition = new Vector3(0f, -H * 0.2f, -L * 0.5f);

            var seats = new List<Vector3>();
            for (int i = 0; i < def.seats; i++)
                seats.Add(new Vector3((i % 2 == 0 ? -1f : 1f) * W * 0.20f, H * 0.20f, -L * 0.05f - (i / 2) * 0.9f));
            v.SeatPositions = seats.ToArray();
        }

        // ------------------------------------------------------------------
        private static void BuildAircraft(MeshBuilder mb, VehicleDefinition def, Color paint, ref Rng rng, ref VehicleVisual v)
        {
            float L = def.length, W = def.width, H = def.height;
            Color trim = new Color(0.14f, 0.15f, 0.18f);
            Color glass = new Color(0.18f, 0.26f, 0.32f);
            bool heli = def.vehicleClass == VehicleClass.Helicopter;

            if (heli)
            {
                float bodyR = H * 0.30f;
                Taper(mb, SubGlossy, paint, new Vector3(0f, bodyR + 0.6f, L * 0.10f), new Vector3(W, H * 0.55f, L * 0.42f), 0.8f, 0.5f, Quaternion.identity);
                Box(mb, SubGlass, glass, new Vector3(0f, bodyR + 0.75f, L * 0.28f), new Vector3(W * 0.78f, H * 0.32f, 0.08f), Quaternion.Euler(-25f, 0f, 0f));
                // Tail boom and fin.
                Box(mb, SubGlossy, paint, new Vector3(0f, bodyR + 0.9f, -L * 0.28f), new Vector3(W * 0.22f, H * 0.16f, L * 0.55f), Quaternion.identity);
                Box(mb, SubGlossy, paint, new Vector3(0f, bodyR + 1.4f, -L * 0.5f), new Vector3(0.10f, H * 0.36f, L * 0.10f), Quaternion.identity);
                // Skids.
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(mb, SubMetal, new Color(0.6f, 0.62f, 0.66f), new Vector3(side * W * 0.42f, 0.10f, L * 0.06f), new Vector3(0.08f, 0.08f, L * 0.40f), Quaternion.identity);
                    Box(mb, SubMetal, new Color(0.6f, 0.62f, 0.66f), new Vector3(side * W * 0.30f, 0.42f, L * 0.06f), new Vector3(0.07f, 0.70f, 0.07f), Quaternion.Euler(0f, 0f, side * 14f));
                }
                v.RotorPosition = new Vector3(0f, bodyR + H * 0.62f, L * 0.08f);
                v.RotorRadius = L * 0.52f;
                Box(mb, SubMetal, new Color(0.5f, 0.52f, 0.56f), v.RotorPosition + Vector3.down * 0.18f, new Vector3(0.18f, 0.36f, 0.18f), Quaternion.identity);
                v.WheelPositions = new Vector3[0];
            }
            else
            {
                float fuseR = H * 0.22f;
                float y = def.wheelRadius + fuseR;
                Taper(mb, SubGlossy, paint, new Vector3(0f, y, 0f), new Vector3(W * 0.16f, H * 0.42f, L), 0.85f, 0.35f, Quaternion.identity);
                Box(mb, SubGlass, glass, new Vector3(0f, y + H * 0.16f, L * 0.24f), new Vector3(W * 0.13f, H * 0.16f, L * 0.16f), Quaternion.identity);
                // Wings.
                Box(mb, SubGlossy, paint, new Vector3(0f, y - H * 0.05f, L * 0.02f), new Vector3(W, H * 0.05f, L * 0.20f), Quaternion.identity);
                // Tail plane and fin.
                Box(mb, SubGlossy, paint, new Vector3(0f, y + H * 0.05f, -L * 0.42f), new Vector3(W * 0.36f, H * 0.04f, L * 0.10f), Quaternion.identity);
                Box(mb, SubGlossy, paint, new Vector3(0f, y + H * 0.22f, -L * 0.44f), new Vector3(H * 0.04f, H * 0.34f, L * 0.10f), Quaternion.identity);
                // Engines.
                for (int side = -1; side <= 1; side += 2)
                    Taper(mb, SubMetal, new Color(0.62f, 0.64f, 0.68f), new Vector3(side * W * 0.26f, y - H * 0.12f, L * 0.06f),
                        new Vector3(H * 0.20f, H * 0.20f, L * 0.16f), 0.9f, 0.9f, Quaternion.identity);
                v.WheelPositions = BuildWheelPositions(def);
                v.RotorPosition = new Vector3(0f, y, L * 0.5f);
                v.RotorRadius = 0f;
            }

            Vector3 nav1 = new Vector3(-W * 0.48f, H * 0.5f, 0f);
            Vector3 nav2 = new Vector3(W * 0.48f, H * 0.5f, 0f);
            Box(mb, SubEmissive, new Color(1f, 0.2f, 0.15f), nav1, new Vector3(0.14f, 0.10f, 0.14f), Quaternion.identity);
            Box(mb, SubEmissive, new Color(0.2f, 1f, 0.3f), nav2, new Vector3(0.14f, 0.10f, 0.14f), Quaternion.identity);
            v.HeadlightPositions = new[] { new Vector3(0f, H * 0.25f, L * 0.45f) };
            v.TaillightPositions = new[] { nav1, nav2 };
            v.ExhaustPosition = new Vector3(0f, H * 0.2f, -L * 0.5f);

            var seats = new List<Vector3>();
            for (int i = 0; i < def.seats; i++)
                seats.Add(new Vector3((i % 2 == 0 ? -1f : 1f) * W * 0.10f, H * 0.28f, L * 0.16f - (i / 2) * 0.9f));
            v.SeatPositions = seats.ToArray();
        }
    }
}
