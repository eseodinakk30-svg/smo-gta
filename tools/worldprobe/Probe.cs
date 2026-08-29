// Runs the game's own world generation outside Unity and reports what is
// actually produced under the spawn point. Reading the code twice produced two
// wrong diagnoses for "no floor at spawn"; this executes it instead.
using System;
using System.Collections.Generic;
using UnityEngine;
using SanMonica.Data;
using SanMonica.World;

internal static class Probe
{
    private static int _failures;

    private static void Fail(string message)
    {
        _failures++;
        Console.WriteLine("FAIL: " + message);
    }

    private static int Main()
    {
        var cfg = WorldConfig.CreateDefault();
        Console.WriteLine($"config: worldSize={cfg.worldSize} chunkSize={cfg.chunkSize} " +
                          $"halfSize={cfg.HalfSize} chunkCount={cfg.ChunkCount} seed={cfg.seed}");

        var map = new WorldMap(cfg);
        var roads = new RoadNetwork(cfg, map);
        roads.Build();
        Console.WriteLine($"roads: {roads.Segments.Count} segments");

        // The spawn point, exactly as GameManager computes its road-based branch.
        Vector2 fallback = map.DowntownCenter;
        int segment = roads.NearestSegment(fallback, 900f);
        Vector3 spawn;
        if (segment >= 0)
        {
            spawn = roads.SidewalkPoint(segment, true, 0.5f) + Vector3.up * 0.6f;
            Console.WriteLine($"spawn: sidewalk of segment {segment} -> {spawn}");
        }
        else
        {
            spawn = new Vector3(fallback.x, map.SampleHeight(fallback.x, fallback.y) + 0.6f, fallback.y);
            Console.WriteLine($"spawn: downtown fallback -> {spawn}");
        }

        float terrain = map.SampleHeight(spawn.x, spawn.z);
        Console.WriteLine($"terrain height under spawn: {terrain:0.###}   (spawn is {spawn.y - terrain:0.###} above it)");

        var coord = cfg.WorldToChunk(spawn);
        Console.WriteLine($"spawn chunk: {coord}  inBounds={cfg.InBounds(coord)}  origin={cfg.ChunkOrigin(coord)}");

        var db = ScriptableObject.CreateInstance<GameDatabase>();
        var layout = new CityLayout(cfg, map, roads, db);
        layout.Generate();

        var builder = new ChunkBuilder(cfg, map, roads, layout);
        var geo = new ChunkGeometry();

        // What the physics engine would actually find: a ray straight down through
        // the triangles of the chunk mesh. Vertices "near" the spawn prove nothing
        // on their own - a hole in the surface has vertices all around its edge.
        builder.Build(coord, 0, geo);
        float hit = RaycastDown(geo, spawn.x, spawn.z, spawn.y + 100f, out float faceY);
        if (faceY > 0f) Console.WriteLine("  surface under the spawn faces UP - a downward raycast would report it");
        else Fail("the surface under the spawn faces DOWN - Physics.Raycast will not report it");
        Console.WriteLine(hit > float.NegativeInfinity
            ? $"RAY DOWN at spawn hits mesh at y={hit:0.###}  (spawn y={spawn.y:0.###})"
            : "RAY DOWN at spawn hits NOTHING - there is a hole in the mesh here");

        // Is the spawn buried inside a solid collider? A player standing inside a
        // building box is pushed somewhere unpredictable, which looks exactly like
        // falling through an intact floor.
        // Winding. Unity treats Cross(b-a, c-a) as the front face, culls back
        // faces when rendering and - crucially - Physics.Raycast does not report
        // a hit on a back face, while a CharacterController capsule still
        // collides with it. Ground wound the wrong way is therefore solid to
        // stand on and invisible to every raycast the game does.
        int up = 0, down = 0;
        {
            var verts = geo.Builder.Vertices;
            for (int sub = 0; sub < geo.Builder.SubmeshCount; sub++)
            {
                var tris = geo.Builder.Submesh(sub);
                for (int i = 0; i + 2 < tris.Count; i += 3)
                {
                    Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                    Vector3 fn = Vector3.Cross(b - a, c - a);
                    if (fn.y > 0.001f) up++;
                    else if (fn.y < -0.001f) down++;
                }
            }
        }
        Console.WriteLine($"triangle winding: {up} face up, {down} face down (front = Cross(b-a, c-a))");

        int inside = 0;
        for (int i = 0; i < geo.Boxes.Count; i++)
        {
            var b = geo.Boxes[i];
            if (b.IsTrigger) continue;
            Vector3 d = spawn - b.Center;
            if (Mathf.Abs(d.x) <= b.Size.x * 0.5f &&
                Mathf.Abs(d.y) <= b.Size.y * 0.5f &&
                Mathf.Abs(d.z) <= b.Size.z * 0.5f)
            {
                inside++;
                Console.WriteLine($"  spawn is INSIDE box center={b.Center} size={b.Size} layer={b.Layer}");
            }
        }
        Console.WriteLine($"solid boxes containing the spawn point: {inside} (rotation ignored, axis-aligned test)");

        int misses = 0, flipped = 0;
        for (int gx = -4; gx <= 4; gx++)
        for (int gz = -4; gz <= 4; gz++)
        {
            float px = spawn.x + gx * 3f, pz = spawn.z + gz * 3f;
            if (RaycastDown(geo, px, pz, spawn.y + 100f, out float fy) == float.NegativeInfinity) misses++;
            else if (fy <= 0f) flipped++;
        }
        Console.WriteLine($"9x9 grid of 3 m samples around the spawn: {misses} of 81 hit nothing, " +
                          $"{flipped} of 81 land on a DOWN-facing surface");
        if (misses > 0) Fail($"{misses} of 81 samples around the spawn hit no geometry at all");
        if (flipped > 0) Fail($"{flipped} of 81 samples around the spawn land on an inside-out surface");

        // The spawn tile is one of four thousand. Sweep the map so a surface
        // wound the wrong way in some other district cannot hide.
        {
            var rng = new System.Random(1);
            int sampled = 0, hits = 0, flippedAll = 0, empty = 0;
            var worst = new List<string>();
            for (int n = 0; n < 60; n++)
            {
                var c = new Vector2Int(rng.Next(6, cfg.ChunkCount - 6), rng.Next(6, cfg.ChunkCount - 6));
                builder.Build(c, 0, geo);
                Vector3 o = cfg.ChunkOrigin(c);
                int chunkFlipped = 0;
                for (int gx = 1; gx < 8; gx++)
                for (int gz = 1; gz < 8; gz++)
                {
                    float px = o.x + gx * (cfg.chunkSize / 8f);
                    float pz = o.z + gz * (cfg.chunkSize / 8f);
                    sampled++;
                    float y = RaycastDown(geo, px, pz, 900f, out float fy);
                    if (y == float.NegativeInfinity) { empty++; continue; }
                    hits++;
                    if (fy <= 0f)
                    {
                        flippedAll++; chunkFlipped++;
                        if (worst.Count < 14)
                            Console.WriteLine($"  flipped hit at ({px:0.#}, {y:0.##}, {pz:0.#}) " +
                                              $"= terrain {map.SampleHeight(px, pz):0.##} + {y - map.SampleHeight(px, pz):0.##}");
                    }
                }
                if (chunkFlipped > 0) worst.Add($"{c}:{chunkFlipped}");
            }
            Console.WriteLine($"map sweep: {sampled} samples over 60 chunks - {hits} hit a surface, " +
                              $"{empty} hit nothing (water//sea is expected), {flippedAll} landed on a DOWN-facing surface");
            if (worst.Count > 0) Console.WriteLine("  chunks still flipped: " + string.Join(", ", worst));
            if (flippedAll > 0)
                Fail($"{flippedAll} of {sampled} samples across the map land on an inside-out surface");
        }

        for (int lod = 0; lod <= 2; lod++)
        {
            builder.Build(coord, lod, geo);
            int tris = 0;
            for (int i = 0; i < geo.Builder.SubmeshCount; i++) tris += geo.Builder.Submesh(i).Count / 3;

            var verts = geo.Builder.Vertices;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            int near = 0;
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float dx = v.x - spawn.x, dz = v.z - spawn.z;
                if (dx * dx + dz * dz > 400f) continue;   // within 20 m of the spawn
                near++;
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;
            }
            Console.WriteLine($"lod {lod}: {verts.Count} verts, {tris} tris, {geo.Boxes.Count} boxes; " +
                              (near > 0
                                ? $"{near} verts within 20 m of spawn, y {minY:0.##}..{maxY:0.##}"
                                : "NO geometry within 20 m of the spawn"));
        }
        Console.WriteLine(_failures == 0
            ? "world generation OK"
            : $"world generation FAILED {_failures} check(s)");
        return _failures == 0 ? 0 : 1;
    }

    /// <summary>Downward ray against every triangle in the geometry, Moller-Trumbore.</summary>
    private static float RaycastDown(ChunkGeometry geo, float x, float z, float fromY)
        => RaycastDown(geo, x, z, fromY, out _);

    private static float RaycastDown(ChunkGeometry geo, float x, float z, float fromY, out float faceUpY)
    {
        faceUpY = 0f;
        var verts = geo.Builder.Vertices;
        float best = float.NegativeInfinity;
        var origin = new Vector3(x, fromY, z);
        var dir = new Vector3(0f, -1f, 0f);

        for (int sub = 0; sub < geo.Builder.SubmeshCount; sub++)
        {
            var tris = geo.Builder.Submesh(sub);
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 h = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, h);
                if (det > -1e-7f && det < 1e-7f) continue;
                float inv = 1f / det;
                Vector3 s = origin - a;
                float u = Vector3.Dot(s, h) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                float t = Vector3.Dot(e2, q) * inv;
                if (t <= 0f) continue;
                float y = fromY - t;
                if (y > best) { best = y; faceUpY = Vector3.Cross(e1, e2).y; }
            }
        }
        return best;
    }
}
