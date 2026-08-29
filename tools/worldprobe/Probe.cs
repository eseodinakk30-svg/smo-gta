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

        // GameDatabase.Build is what the game calls; CreateInstance alone leaves it
        // empty, and an empty catalogue means no shops get placed at all.
        var db = GameDatabase.Build();
        Console.WriteLine($"database: {db.shops.Count} shop types, {db.vehicles.Count} vehicles, " +
                          $"{db.weapons.Count} weapons, {db.peds.Count} ped archetypes");
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
        CheckRoadNetwork(cfg, map, roads);
        CheckBridges(cfg, map, roads, builder, geo);
        CheckCityContent(cfg, map, roads, layout);

        Console.WriteLine(_failures == 0
            ? "world generation OK"
            : $"world generation FAILED {_failures} check(s)");
        return _failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// A bridge in the graph is worthless if no surface was built at deck
    /// height: the car would simply drive into the bay. This casts down onto the
    /// middle of each span the way physics does.
    /// </summary>
    private static void CheckBridges(WorldConfig cfg, WorldMap map, RoadNetwork roads,
                                     ChunkBuilder builder, ChunkGeometry geo)
    {
        var bridges = new List<int>();
        for (int i = 0; i < roads.Segments.Count; i++) if (roads.Segments[i].IsBridge) bridges.Add(i);
        Console.WriteLine($"bridges: {bridges.Count} spans carry roads over water");
        if (bridges.Count == 0) return;

        int drivable = 0, dry = 0;
        foreach (int i in bridges)
        {
            var seg = roads.Segments[i];
            Vector2 mid = seg.Point(0.5f);
            float deck = seg.DeckAt(0.5f);
            if (deck > cfg.seaLevel + 3f) dry++;

            var coord = cfg.WorldToChunk(new Vector3(mid.x, 0f, mid.y));
            builder.Build(coord, 0, geo);
            float hit = RaycastDown(geo, mid.x, mid.y, deck + 60f, out float faceY);
            bool ok = hit > cfg.seaLevel + 1f && faceY > 0f && Mathf.Abs(hit - deck) < 3f;
            if (ok) drivable++;
            else
                Console.WriteLine($"  span {i}: deck {deck:0.0} m, ray hit " +
                                  (hit == float.NegativeInfinity ? "nothing" : $"{hit:0.0} m") +
                                  (faceY > 0f ? "" : " (down-facing)"));
        }
        Console.WriteLine($"bridges: {dry}/{bridges.Count} clear the water, {drivable}/{bridges.Count} have a drivable deck");
        if (dry < bridges.Count) Fail($"{bridges.Count - dry} bridge decks sit at or under the waterline");
        if (drivable < bridges.Count) Fail($"{bridges.Count - drivable} bridges have no surface to drive on");
    }

    /// <summary>
    /// The road graph is what traffic drives on and what missions navigate, so
    /// a segment nobody connects to is a dead end no car can reach.
    /// </summary>
    private static void CheckRoadNetwork(WorldConfig cfg, WorldMap map, RoadNetwork roads)
    {
        int orphanNodes = 0, degenerate = 0, outOfBounds = 0, noSidewalk = 0;
        for (int i = 0; i < roads.Segments.Count; i++)
        {
            var seg = roads.Segments[i];
            if (seg.Length < 0.5f) degenerate++;
            if (Mathf.Abs(seg.A.x) > cfg.HalfSize || Mathf.Abs(seg.A.y) > cfg.HalfSize ||
                Mathf.Abs(seg.B.x) > cfg.HalfSize || Mathf.Abs(seg.B.y) > cfg.HalfSize) outOfBounds++;
            if (seg.HalfWidth <= 0.1f) noSidewalk++;
        }
        for (int i = 0; i < roads.Nodes.Count; i++)
            if (roads.Nodes[i].Segments.Count == 0) orphanNodes++;

        Console.WriteLine($"roads: {roads.Segments.Count} segments, {roads.Nodes.Count} nodes, " +
                          $"{degenerate} degenerate, {outOfBounds} outside the map, " +
                          $"{noSidewalk} with no width, {orphanNodes} unconnected nodes");
        if (degenerate > 0) Fail($"{degenerate} road segments are shorter than half a metre");
        if (outOfBounds > 0) Fail($"{outOfBounds} road segments leave the map bounds");
        if (noSidewalk > 0) Fail($"{noSidewalk} road segments have no width");

        // Reachability: walk the graph from the busiest node and see what it reaches.
        int start = 0;
        for (int i = 1; i < roads.Nodes.Count; i++)
            if (roads.Nodes[i].Segments.Count > roads.Nodes[start].Segments.Count) start = i;

        var seen = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(start); seen.Add(start);
        while (queue.Count > 0)
        {
            var node = roads.Nodes[queue.Dequeue()];
            for (int i = 0; i < node.Segments.Count; i++)
            {
                var seg = roads.Segments[node.Segments[i]];
                foreach (int next in new[] { seg.NodeA, seg.NodeB })
                    if (next >= 0 && next < roads.Nodes.Count && seen.Add(next)) queue.Enqueue(next);
            }
        }
        // Component sizes tell the difference between "one city with a few
        // stranded lanes" and "thousands of little islands".
        var visited = new HashSet<int>();
        var sizes = new List<int>();
        for (int n = 0; n < roads.Nodes.Count; n++)
        {
            if (visited.Contains(n)) continue;
            int size = 0;
            var q2 = new Queue<int>();
            q2.Enqueue(n); visited.Add(n);
            while (q2.Count > 0)
            {
                var nd = roads.Nodes[q2.Dequeue()]; size++;
                for (int i = 0; i < nd.Segments.Count; i++)
                {
                    var sg = roads.Segments[nd.Segments[i]];
                    foreach (int nx in new[] { sg.NodeA, sg.NodeB })
                        if (nx >= 0 && nx < roads.Nodes.Count && visited.Add(nx)) q2.Enqueue(nx);
                }
            }
            sizes.Add(size);
        }
        // For each surviving island, how far is the nearest other network and is
        // the ground between it water? That is the difference between "raise the
        // link limit" and "this needs a bridge".
        {
            var label = new int[roads.Nodes.Count];
            for (int i = 0; i < label.Length; i++) label[i] = -1;
            int comp = 0;
            for (int seed = 0; seed < roads.Nodes.Count; seed++)
            {
                if (label[seed] >= 0) continue;
                var st = new Stack<int>(); st.Push(seed); label[seed] = comp;
                while (st.Count > 0)
                {
                    var nd = roads.Nodes[st.Pop()];
                    for (int i = 0; i < nd.Segments.Count; i++)
                    {
                        var sg = roads.Segments[nd.Segments[i]];
                        if (sg.NodeA >= 0 && label[sg.NodeA] < 0) { label[sg.NodeA] = comp; st.Push(sg.NodeA); }
                        if (sg.NodeB >= 0 && label[sg.NodeB] < 0) { label[sg.NodeB] = comp; st.Push(sg.NodeB); }
                    }
                }
                comp++;
            }
            for (int c = 0; c < comp; c++)
            {
                float best = float.MaxValue; int bi = -1, bj = -1;
                for (int i = 0; i < roads.Nodes.Count; i++)
                {
                    if (label[i] != c) continue;
                    for (int j = 0; j < roads.Nodes.Count; j++)
                    {
                        if (label[j] == c) continue;
                        float d = (roads.Nodes[i].Pos - roads.Nodes[j].Pos).sqrMagnitude;
                        if (d < best) { best = d; bi = i; bj = j; }
                    }
                }
                if (bi < 0) continue;
                Vector2 a = roads.Nodes[bi].Pos, b = roads.Nodes[bj].Pos;
                int wet = 0, steps = 24;
                float lowest = float.MaxValue;
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 pnt = Vector2.Lerp(a, b, k / (float)steps);
                    float h = map.SampleHeight(pnt.x, pnt.y);
                    if (h <= cfg.seaLevel + 1.2f) wet++;
                    if (h < lowest) lowest = h;
                }
                Console.WriteLine($"  network {c}: nearest other network is {Mathf.Sqrt(best):0} m away, " +
                                  $"{wet}/{steps + 1} samples under water, lowest ground {lowest:0.0} m");
            }
        }

        sizes.Sort((a, b) => b.CompareTo(a));
        Console.WriteLine($"road components: {sizes.Count} separate networks; largest = " +
                          string.Join(", ", sizes.GetRange(0, Mathf.Min(6, sizes.Count))));
        int degSum = 0;
        for (int n = 0; n < roads.Nodes.Count; n++) degSum += roads.Nodes[n].Segments.Count;
        Console.WriteLine($"average node degree: {(roads.Nodes.Count == 0 ? 0f : degSum / (float)roads.Nodes.Count):0.00}");

        float reach = roads.Nodes.Count == 0 ? 0f : sizes[0] / (float)roads.Nodes.Count;
        Console.WriteLine($"road reachability: {seen.Count} of {roads.Nodes.Count} nodes ({reach:P1}) " +
                          "connected to the main network");
        if (reach < 0.85f)
            Fail($"only {reach:P1} of the road network is connected - traffic and missions cannot cross the city");
    }

    /// <summary>Shops and properties the player is sent to must exist and be placed sanely.</summary>
    private static void CheckCityContent(WorldConfig cfg, WorldMap map, RoadNetwork roads, CityLayout layout)
    {
        int shops = layout.Shops.Count, properties = layout.Properties.Count;
        Console.WriteLine($"city: {layout.Lots.Count} lots, {shops} shops, {properties} properties");
        if (shops == 0) Fail("no shops were generated - the economy has nowhere to happen");
        if (properties == 0) Fail("no properties were generated");

        int farFromRoad = 0, sunk = 0;
        for (int i = 0; i < shops; i++)
        {
            var shop = layout.Shops[i];
            var flat = new Vector2(shop.Position.x, shop.Position.z);
            if (roads.NearestSegment(flat, 220f) < 0) farFromRoad++;
            if (shop.Position.y < map.SampleHeight(shop.Position.x, shop.Position.z) - 2f) sunk++;
        }
        Console.WriteLine($"shops: {farFromRoad} more than 220 m from any road, {sunk} below the terrain");
        if (farFromRoad > shops / 10) Fail($"{farFromRoad} of {shops} shops are unreachable by road");
        if (sunk > 0) Fail($"{sunk} shops are buried under the terrain");
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
