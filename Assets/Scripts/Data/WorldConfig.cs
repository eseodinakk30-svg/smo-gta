using UnityEngine;

namespace SanMonica.Data
{
    public enum DistrictType
    {
        Ocean, Beach, Marina, Port, Downtown, Commercial, Marigold, Residential,
        Wealthy, Industrial, Airport, University, Suburb, Farmland, Forest,
        Mountains, Badlands, Park, Highway
    }

    /// <summary>
    /// Master configuration for the procedurally generated city of San Monica.
    /// A single seed reproduces the entire 16 x 16 km world byte for byte, which
    /// is what allows save games to reference world content without storing it.
    /// </summary>
    [CreateAssetMenu(menuName = "San Monica/World Config", fileName = "WorldConfig")]
    public class WorldConfig : ScriptableObject
    {
        [Header("Identity")]
        public int seed = 20260823;

        [Header("Dimensions (metres)")]
        [Tooltip("Total square size of the world. 16384 m => 268 km2 of playable space.")]
        public float worldSize = 16384f;
        [Tooltip("Streaming granularity. Each chunk holds one city block cluster.")]
        public float chunkSize = 256f;
        public float seaLevel = 0f;
        public float maxTerrainHeight = 620f;

        [Header("Streaming")]
        [Tooltip("Full detail radius in chunks.")]
        public int highDetailRings = 2;
        public int mediumDetailRings = 4;
        public int lowDetailRings = 8;
        public int impostorRings = 16;
        public float chunkBuildBudgetMs = 4f;

        [Header("City layout")]
        public float blockSize = 96f;          // building block footprint
        public float streetWidth = 18f;
        public float avenueWidth = 30f;
        public float highwayWidth = 42f;
        public float sidewalkWidth = 3.2f;
        public float laneWidth = 3.6f;

        [Header("Population")]
        public int maxActivePeds = 90;
        public int maxActiveVehicles = 60;
        public float pedSpawnRadius = 110f;
        public float pedDespawnRadius = 165f;
        public float vehicleSpawnRadius = 190f;
        public float vehicleDespawnRadius = 300f;

        [Header("Gameplay")]
        public Vector3 defaultSpawn = new Vector3(-120f, 2f, 340f);
        public float gravity = -19.6f;

        public float HalfSize => worldSize * 0.5f;
        public int ChunkCount => Mathf.RoundToInt(worldSize / chunkSize);

        public Vector2Int WorldToChunk(Vector3 world)
        {
            return new Vector2Int(
                Mathf.FloorToInt((world.x + HalfSize) / chunkSize),
                Mathf.FloorToInt((world.z + HalfSize) / chunkSize));
        }

        public Vector3 ChunkOrigin(Vector2Int c)
        {
            return new Vector3(c.x * chunkSize - HalfSize, 0f, c.y * chunkSize - HalfSize);
        }

        public Vector3 ChunkCenter(Vector2Int c) => ChunkOrigin(c) + new Vector3(chunkSize * 0.5f, 0f, chunkSize * 0.5f);

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < ChunkCount && c.y < ChunkCount;

        public bool InWorld(Vector3 p) => Mathf.Abs(p.x) <= HalfSize && Mathf.Abs(p.z) <= HalfSize;

        public static WorldConfig CreateDefault(int seed = 0)
        {
            var cfg = CreateInstance<WorldConfig>();
            cfg.name = "WorldConfig_Runtime";
            if (seed != 0) cfg.seed = seed;
            return cfg;
        }
    }

    /// <summary>Per district look-and-feel and simulation density.</summary>
    [System.Serializable]
    public class DistrictProfile
    {
        public DistrictType type;
        public string displayName = "District";
        public Color mapColor = Color.gray;

        [Header("Buildings")]
        public float minHeight = 8f;
        public float maxHeight = 24f;
        public float buildingDensity = 0.8f;
        public float blockFill = 0.85f;
        public int minFloors = 2;
        public int maxFloors = 6;

        [Header("Simulation density (multipliers)")]
        public float pedDensity = 1f;
        public float trafficDensity = 1f;
        public float policePresence = 1f;
        public float crimeRate = 0.1f;
        public float wealth = 0.5f;

        [Header("Ambience")]
        public Color ambientTint = Color.white;
        public float streetLightSpacing = 34f;
        public float treeDensity = 0.15f;

        public DistrictProfile Clone() => (DistrictProfile)MemberwiseClone();
    }
}
