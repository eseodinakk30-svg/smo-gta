using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.World
{
    public enum LotKind { Building, Park, ParkingLot, Plaza, Yard, Farmfield, Pool, Empty, Apron }

    public struct BuildingLot
    {
        public Vector2 Center;
        public Vector2 Size;
        public float Yaw;
        public DistrictType District;
        public LotKind Kind;
        public int ShopIndex;      // -1 when none
        public int PropertyIndex;  // -1 when none
        public int Seed;
    }

    public class ShopInstance
    {
        public int Index;
        public ShopDefinition Definition;
        public Vector3 Position;      // world position of the door
        public Vector3 Forward;       // direction the door faces
        public DistrictType District;
        public string UniqueName;
        public bool Discovered;
    }

    public class PropertyInstance
    {
        public int Index;
        public PropertyDefinition Definition;
        public bool Owned;
        public List<string> StoredVehicles = new List<string>();
    }

    public struct ParkingSpot
    {
        public Vector3 Position;
        public float Yaw;
        public bool Indoor;
    }

    /// <summary>
    /// A single deterministic pass over the whole 16 x 16 km map that decides
    /// where every building lot, shop, property and parking bay sits. Running it
    /// up-front (about 15 000 lots, a few hundred milliseconds) means the map
    /// screen, missions and the economy know the entire city even though only a
    /// handful of chunks are ever loaded as geometry.
    /// </summary>
    public class CityLayout
    {
        public readonly List<BuildingLot> Lots = new List<BuildingLot>(20000);
        public readonly List<ShopInstance> Shops = new List<ShopInstance>(400);
        public readonly List<PropertyInstance> Properties = new List<PropertyInstance>(200);
        public readonly List<ParkingSpot> ParkingSpots = new List<ParkingSpot>(4000);

        private readonly Dictionary<long, List<int>> _lotsByChunk = new Dictionary<long, List<int>>();
        private readonly WorldConfig _cfg;
        private readonly WorldMap _map;
        private readonly RoadNetwork _roads;
        private readonly GameDatabase _db;

        public CityLayout(WorldConfig cfg, WorldMap map, RoadNetwork roads, GameDatabase db)
        {
            _cfg = cfg; _map = map; _roads = roads; _db = db;
        }

        private static long ChunkKey(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        public List<int> LotsInChunk(Vector2Int c)
        {
            return _lotsByChunk.TryGetValue(ChunkKey(c.x, c.y), out var list) ? list : null;
        }

        public void Generate()
        {
            Lots.Clear(); Shops.Clear(); Properties.Clear(); ParkingSpots.Clear(); _lotsByChunk.Clear();

            GenerateUrbanBlocks();
            GenerateRuralLots();
            GenerateAirportLots();
            GeneratePortLots();
            IndexLots();
        }

        // ------------------------------------------------------------------
        // Urban blocks along the street grid
        // ------------------------------------------------------------------
        private void GenerateUrbanBlocks()
        {
            float pitch = _cfg.blockSize + _cfg.streetWidth;
            const float minX = -3900f, maxX = 3900f, minZ = -4400f, maxZ = 4400f;
            int nx = Mathf.CeilToInt((maxX - minX) / pitch);
            int nz = Mathf.CeilToInt((maxZ - minZ) / pitch);

            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                Vector2 blockCenter = new Vector2(minX + (ix + 0.5f) * pitch, minZ + (iz + 0.5f) * pitch);
                if (_map.Landness(blockCenter.x, blockCenter.y) < 30f) continue;
                float urban = _map.UrbanMask(blockCenter.x, blockCenter.y);
                if (urban < 0.20f) continue;

                var district = _map.DistrictAt(blockCenter.x, blockCenter.y);
                if (district == DistrictType.Airport || district == DistrictType.Ocean) continue;
                var profile = DistrictCatalog.Get(district);
                var rng = Rng.FromCoords(_cfg.seed, ix, iz, 7);

                // Reject blocks that a road runs straight through.
                float clearance = _roads.RoadClearance(blockCenter, out _);
                if (clearance < 6f) continue;

                float usable = Mathf.Min(_cfg.blockSize, clearance * 2f - 4f);
                if (usable < 16f) continue;

                LotKind blockKind = ChooseBlockKind(district, profile, ref rng, urban);
                if (blockKind == LotKind.Park || blockKind == LotKind.Plaza || blockKind == LotKind.ParkingLot)
                {
                    AddLot(new BuildingLot
                    {
                        Center = blockCenter, Size = new Vector2(usable, usable), Yaw = 0f,
                        District = district, Kind = blockKind, ShopIndex = -1, PropertyIndex = -1,
                        Seed = (int)rng.NextUInt()
                    });
                    if (blockKind == LotKind.ParkingLot) AddParkingRows(blockCenter, usable, 0f, ref rng);
                    continue;
                }

                SubdivideBlock(blockCenter, usable, district, profile, ref rng);
            }
        }

        private LotKind ChooseBlockKind(DistrictType d, DistrictProfile p, ref Rng rng, float urban)
        {
            if (d == DistrictType.Park) return rng.Chance(0.85f) ? LotKind.Park : LotKind.Building;
            float roll = rng.Value;
            if (roll > p.buildingDensity)
            {
                if (d == DistrictType.Downtown || d == DistrictType.Commercial)
                    return rng.Chance(0.45f) ? LotKind.Plaza : LotKind.ParkingLot;
                if (d == DistrictType.Industrial || d == DistrictType.Port) return LotKind.Yard;
                return rng.Chance(0.5f) ? LotKind.Park : LotKind.ParkingLot;
            }
            return LotKind.Building;
        }

        private void SubdivideBlock(Vector2 center, float usable, DistrictType district, DistrictProfile profile, ref Rng rng)
        {
            // Larger buildings downtown, many small lots in low rise districts.
            int divX, divZ;
            switch (district)
            {
                case DistrictType.Downtown: divX = rng.Range(1, 3); divZ = rng.Range(1, 3); break;
                case DistrictType.Commercial:
                case DistrictType.University: divX = rng.Range(1, 3); divZ = rng.Range(2, 4); break;
                case DistrictType.Industrial:
                case DistrictType.Port: divX = rng.Range(1, 3); divZ = rng.Range(1, 3); break;
                case DistrictType.Wealthy: divX = 2; divZ = 2; break;
                case DistrictType.Suburb: divX = rng.Range(2, 4); divZ = rng.Range(2, 4); break;
                default: divX = rng.Range(2, 4); divZ = rng.Range(2, 4); break;
            }

            float cellX = usable / divX, cellZ = usable / divZ;
            for (int bx = 0; bx < divX; bx++)
            for (int bz = 0; bz < divZ; bz++)
            {
                if (!rng.Chance(profile.blockFill)) continue;
                Vector2 lotCenter = center + new Vector2(
                    (-0.5f + (bx + 0.5f) / divX) * usable,
                    (-0.5f + (bz + 0.5f) / divZ) * usable);

                if (_map.Landness(lotCenter.x, lotCenter.y) < 22f) continue;
                if (_roads.RoadClearance(lotCenter, out _) < 5f) continue;
                if (_map.SampleSlope(lotCenter.x, lotCenter.y) > 26f) continue;

                float inset = district == DistrictType.Wealthy || district == DistrictType.Suburb ? 5.5f : 1.8f;
                Vector2 size = new Vector2(Mathf.Max(7f, cellX - inset), Mathf.Max(7f, cellZ - inset));

                // Face the nearest street.
                float yaw = FacingYaw(lotCenter);

                var lot = new BuildingLot
                {
                    Center = lotCenter, Size = size, Yaw = yaw, District = district,
                    Kind = LotKind.Building, ShopIndex = -1, PropertyIndex = -1,
                    Seed = (int)rng.NextUInt()
                };

                var lotRng = new Rng(lot.Seed);
                AssignCommerce(ref lot, district, ref lotRng);
                AddLot(lot);

                // Driveways / kerbside parking for low density districts.
                if ((district == DistrictType.Suburb || district == DistrictType.Wealthy || district == DistrictType.Residential)
                    && lotRng.Chance(0.55f))
                {
                    Vector2 fwd = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                    Vector2 spot = lotCenter + fwd * (size.y * 0.5f + 4.2f);
                    if (_roads.RoadClearance(spot, out _) > 1.2f)
                        ParkingSpots.Add(new ParkingSpot { Position = new Vector3(spot.x, _map.SampleHeight(spot.x, spot.y), spot.y), Yaw = yaw });
                }
            }
        }

        private float FacingYaw(Vector2 p)
        {
            int seg = _roads.NearestSegment(p, 160f);
            if (seg < 0) return 0f;
            var s = _roads.Segments[seg];
            RoadNetwork.DistanceToSegment(p, in s, out float t);
            Vector2 closest = s.A + s.Dir * (t * s.Length);
            Vector2 toRoad = closest - p;
            if (toRoad.sqrMagnitude < 0.01f) return 0f;
            toRoad.Normalize();
            return Mathf.Atan2(toRoad.x, toRoad.y) * Mathf.Rad2Deg;
        }

        private void AssignCommerce(ref BuildingLot lot, DistrictType district, ref Rng rng)
        {
            var shops = _db.shops;
            // Shops sit on ground floors along commercial streets.
            float shopChance = district == DistrictType.Commercial ? 0.42f
                : district == DistrictType.Downtown ? 0.30f
                : district == DistrictType.Marigold ? 0.38f
                : district == DistrictType.Beach ? 0.32f
                : district == DistrictType.Residential ? 0.12f
                : district == DistrictType.Suburb ? 0.10f
                : district == DistrictType.Industrial ? 0.10f
                : district == DistrictType.Port ? 0.08f
                : district == DistrictType.Airport ? 0.25f
                : district == DistrictType.Marina ? 0.22f
                : 0.05f;

            if (rng.Chance(shopChance))
            {
                var candidates = new List<ShopDefinition>();
                foreach (var s in shops)
                {
                    if (s.districts == null || s.districts.Length == 0) { candidates.Add(s); continue; }
                    for (int i = 0; i < s.districts.Length; i++)
                        if (s.districts[i] == district) { candidates.Add(s); break; }
                }
                if (candidates.Count > 0)
                {
                    var def = candidates[rng.Range(0, candidates.Count)];
                    Vector2 fwd = new Vector2(Mathf.Sin(lot.Yaw * Mathf.Deg2Rad), Mathf.Cos(lot.Yaw * Mathf.Deg2Rad));
                    Vector2 door = lot.Center + fwd * (lot.Size.y * 0.5f + 1.2f);
                    var inst = new ShopInstance
                    {
                        Index = Shops.Count,
                        Definition = def,
                        Position = new Vector3(door.x, _map.SampleHeight(door.x, door.y), door.y),
                        Forward = new Vector3(fwd.x, 0f, fwd.y),
                        District = district,
                        UniqueName = def.displayName
                    };
                    Shops.Add(inst);
                    lot.ShopIndex = inst.Index;
                    return;
                }
            }

            // Purchasable property.
            float propChance = district == DistrictType.Wealthy ? 0.16f
                : district == DistrictType.Suburb ? 0.09f
                : district == DistrictType.Residential ? 0.07f
                : district == DistrictType.Downtown ? 0.05f
                : district == DistrictType.Marina ? 0.12f
                : district == DistrictType.Industrial ? 0.05f
                : 0.02f;

            if (rng.Chance(propChance))
            {
                PropertyKind kind;
                int price, income, slots;
                switch (district)
                {
                    case DistrictType.Wealthy:
                        kind = PropertyKind.Villa; price = rng.Range(850000, 3200000); income = rng.Range(0, 400); slots = 6; break;
                    case DistrictType.Downtown:
                        kind = PropertyKind.Penthouse; price = rng.Range(600000, 2100000); income = rng.Range(0, 300); slots = 4; break;
                    case DistrictType.Marina:
                        kind = PropertyKind.Apartment; price = rng.Range(320000, 900000); income = rng.Range(0, 220); slots = 3; break;
                    case DistrictType.Suburb:
                        kind = PropertyKind.House; price = rng.Range(120000, 420000); income = rng.Range(0, 120); slots = 2; break;
                    case DistrictType.Industrial:
                    case DistrictType.Port:
                        kind = PropertyKind.Warehouse; price = rng.Range(280000, 1100000); income = rng.Range(400, 2600); slots = 8; break;
                    default:
                        kind = PropertyKind.Apartment; price = rng.Range(65000, 260000); income = rng.Range(0, 90); slots = 2; break;
                }

                Vector2 fwd2 = new Vector2(Mathf.Sin(lot.Yaw * Mathf.Deg2Rad), Mathf.Cos(lot.Yaw * Mathf.Deg2Rad));
                Vector2 door2 = lot.Center + fwd2 * (lot.Size.y * 0.5f + 1.4f);
                Vector2 park = lot.Center + fwd2 * (lot.Size.y * 0.5f + 6.5f);

                var def2 = new PropertyDefinition
                {
                    id = "prop_" + Properties.Count,
                    displayName = PropertyName(kind, district, ref rng),
                    kind = kind, price = price, dailyIncome = income, garageSlots = slots,
                    district = district,
                    position = new Vector3(door2.x, _map.SampleHeight(door2.x, door2.y), door2.y),
                    spawnPoint = new Vector3(park.x, _map.SampleHeight(park.x, park.y), park.y),
                    heading = lot.Yaw,
                    allowsSave = true,
                    allowsWardrobe = kind != PropertyKind.Garage && kind != PropertyKind.Warehouse
                };
                Properties.Add(new PropertyInstance { Index = Properties.Count, Definition = def2 });
                lot.PropertyIndex = Properties.Count - 1;
            }
        }

        private static readonly string[] StreetNames =
        {
            "Alder", "Bayline", "Corbin", "Dorsey", "Enfield", "Fallow", "Gantry", "Harlow",
            "Ivory", "Junction", "Kestrel", "Lantern", "Marigold", "Nolan", "Orchard", "Pallas",
            "Quarry", "Redwater", "Sable", "Tanner", "Umber", "Vireo", "Wexford", "Yarrow"
        };

        private static string PropertyName(PropertyKind kind, DistrictType d, ref Rng rng)
        {
            string street = StreetNames[rng.Range(0, StreetNames.Length)];
            int num = rng.Range(1, 240);
            switch (kind)
            {
                case PropertyKind.Villa: return street + " Ridge Villa";
                case PropertyKind.Penthouse: return num + " " + street + " Penthouse";
                case PropertyKind.House: return num + " " + street + " Street";
                case PropertyKind.Warehouse: return street + " Depot";
                case PropertyKind.Garage: return street + " Lock-Up";
                default: return num + " " + street + " Apartments";
            }
        }

        private void AddParkingRows(Vector2 center, float usable, float yaw, ref Rng rng)
        {
            int rows = Mathf.Max(1, Mathf.FloorToInt(usable / 12f));
            int cols = Mathf.Max(1, Mathf.FloorToInt(usable / 3f));
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!rng.Chance(0.82f)) continue;
                float ox = (-0.5f + (c + 0.5f) / cols) * usable;
                float oz = (-0.5f + (r + 0.5f) / rows) * usable;
                Vector2 p = center + new Vector2(ox, oz);
                ParkingSpots.Add(new ParkingSpot
                {
                    Position = new Vector3(p.x, _map.SampleHeight(p.x, p.y), p.y),
                    Yaw = yaw + (r % 2 == 0 ? 0f : 180f)
                });
            }
        }

        // ------------------------------------------------------------------
        // Countryside
        // ------------------------------------------------------------------
        private void GenerateRuralLots()
        {
            // Scatter farms, cabins and roadside structures along rural roads.
            for (int i = 0; i < _roads.Segments.Count; i++)
            {
                var s = _roads.Segments[i];
                if (s.Kind != RoadKind.Rural && s.Kind != RoadKind.Dirt) continue;
                var rng = Rng.FromCoords(_cfg.seed, i, 0, 91);
                int count = rng.Range(0, 3);
                for (int k = 0; k < count; k++)
                {
                    float t = rng.Value;
                    float side = rng.Chance(0.5f) ? 1f : -1f;
                    float dist = rng.Range(16f, 55f);
                    Vector2 p = s.Point(t) + s.Right * side * dist;
                    if (_map.Landness(p.x, p.y) < 25f) continue;
                    var d = _map.DistrictAt(p.x, p.y);
                    if (WorldMap.IsUrban(d)) continue;
                    if (_map.SampleSlope(p.x, p.y) > 20f) continue;

                    float yaw = Mathf.Atan2(-s.Right.x * side, -s.Right.y * side) * Mathf.Rad2Deg;
                    var lot = new BuildingLot
                    {
                        Center = p,
                        Size = new Vector2(rng.Range(9f, 20f), rng.Range(9f, 22f)),
                        Yaw = yaw, District = d, Kind = LotKind.Building,
                        ShopIndex = -1, PropertyIndex = -1, Seed = (int)rng.NextUInt()
                    };
                    var lr = new Rng(lot.Seed);
                    if (d == DistrictType.Farmland || d == DistrictType.Suburb || d == DistrictType.Badlands)
                        AssignCommerce(ref lot, d, ref lr);
                    AddLot(lot);

                    // Fields around farms.
                    if (d == DistrictType.Farmland && lr.Chance(0.7f))
                    {
                        Vector2 fc = p + s.Right * side * rng.Range(60f, 140f);
                        if (_map.Landness(fc.x, fc.y) > 40f)
                            AddLot(new BuildingLot
                            {
                                Center = fc, Size = new Vector2(rng.Range(90f, 180f), rng.Range(90f, 180f)),
                                Yaw = 0f, District = d, Kind = LotKind.Farmfield,
                                ShopIndex = -1, PropertyIndex = -1, Seed = (int)lr.NextUInt()
                            });
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Airport & port set pieces
        // ------------------------------------------------------------------
        private void GenerateAirportLots()
        {
            Vector2 c = _map.AirportCenter;
            var rng = new Rng(_cfg.seed ^ 0x5A17);

            // Terminal row.
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = c + new Vector2(-560f + i * 560f, -560f);
                AddLot(new BuildingLot
                {
                    Center = p, Size = new Vector2(320f, 90f), Yaw = 0f,
                    District = DistrictType.Airport, Kind = LotKind.Building,
                    ShopIndex = -1, PropertyIndex = -1, Seed = (int)rng.NextUInt()
                });
            }
            // Hangars.
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = c + new Vector2(-900f + i * 340f, -180f);
                AddLot(new BuildingLot
                {
                    Center = p, Size = new Vector2(150f, 110f), Yaw = 0f,
                    District = DistrictType.Airport, Kind = LotKind.Building,
                    ShopIndex = -1, PropertyIndex = -1, Seed = (int)rng.NextUInt()
                });
            }
            // Apron parking for aircraft and ground vehicles.
            AddLot(new BuildingLot
            {
                Center = c + new Vector2(0f, -330f), Size = new Vector2(2000f, 180f), Yaw = 0f,
                District = DistrictType.Airport, Kind = LotKind.Apron,
                ShopIndex = -1, PropertyIndex = -1, Seed = (int)rng.NextUInt()
            });
            for (int i = 0; i < 40; i++)
            {
                Vector2 p = c + new Vector2(-950f + i * 48f, -700f);
                ParkingSpots.Add(new ParkingSpot { Position = new Vector3(p.x, _map.SampleHeight(p.x, p.y), p.y), Yaw = 0f });
            }
        }

        private void GeneratePortLots()
        {
            Vector2 c = _map.PortCenter;
            var rng = new Rng(_cfg.seed ^ 0x9033);
            for (int i = 0; i < 26; i++)
            {
                Vector2 p = c + new Vector2(rng.Range(-620f, 620f), rng.Range(-520f, 520f));
                if (_map.Landness(p.x, p.y) < 20f) continue;
                AddLot(new BuildingLot
                {
                    Center = p, Size = new Vector2(rng.Range(30f, 90f), rng.Range(24f, 70f)),
                    Yaw = rng.Chance(0.5f) ? 0f : 90f, District = DistrictType.Port,
                    Kind = rng.Chance(0.55f) ? LotKind.Yard : LotKind.Building,
                    ShopIndex = -1, PropertyIndex = -1, Seed = (int)rng.NextUInt()
                });
            }
        }

        // ------------------------------------------------------------------
        private void AddLot(BuildingLot lot)
        {
            Lots.Add(lot);
        }

        private void IndexLots()
        {
            float half = _cfg.HalfSize;
            for (int i = 0; i < Lots.Count; i++)
            {
                var l = Lots[i];
                float pad = Mathf.Max(l.Size.x, l.Size.y) * 0.6f + 8f;
                int minX = Mathf.FloorToInt((l.Center.x - pad + half) / _cfg.chunkSize);
                int maxX = Mathf.FloorToInt((l.Center.x + pad + half) / _cfg.chunkSize);
                int minZ = Mathf.FloorToInt((l.Center.y - pad + half) / _cfg.chunkSize);
                int maxZ = Mathf.FloorToInt((l.Center.y + pad + half) / _cfg.chunkSize);
                for (int cx = minX; cx <= maxX; cx++)
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    long k = ChunkKey(cx, cz);
                    if (!_lotsByChunk.TryGetValue(k, out var list)) { list = new List<int>(8); _lotsByChunk[k] = list; }
                    if (!list.Contains(i)) list.Add(i);
                }
            }
        }

        // ------------------------------------------------------------------
        // Queries used by gameplay systems
        // ------------------------------------------------------------------
        public ShopInstance NearestShop(Vector3 pos, ShopType type, float maxDistance = 4000f)
        {
            ShopInstance best = null; float bestD = maxDistance * maxDistance;
            foreach (var s in Shops)
            {
                if (s.Definition.type != type) continue;
                float d = (s.Position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        public List<ShopInstance> ShopsOfType(ShopType type)
        {
            var list = new List<ShopInstance>();
            foreach (var s in Shops) if (s.Definition.type == type) list.Add(s);
            return list;
        }

        public bool TryFindParking(Vector3 near, float radius, out ParkingSpot spot)
        {
            spot = default;
            float best = radius * radius;
            bool found = false;
            for (int i = 0; i < ParkingSpots.Count; i++)
            {
                float d = (ParkingSpots[i].Position - near).sqrMagnitude;
                if (d < best) { best = d; spot = ParkingSpots[i]; found = true; }
            }
            return found;
        }
    }
}
