using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Data
{
    /// <summary>
    /// The twelve authored districts of San Monica. These profiles drive building
    /// silhouettes, lighting, NPC mix, traffic and police density - each area of
    /// the city therefore feels measurably different to play in.
    /// </summary>
    public static class DistrictCatalog
    {
        private static Dictionary<DistrictType, DistrictProfile> _profiles;

        public static DistrictProfile Get(DistrictType t)
        {
            if (_profiles == null) Build();
            return _profiles.TryGetValue(t, out var p) ? p : _profiles[DistrictType.Residential];
        }

        public static IEnumerable<DistrictProfile> All
        {
            get
            {
                if (_profiles == null) Build();
                return _profiles.Values;
            }
        }

        private static void Add(DistrictProfile p) => _profiles[p.type] = p;

        private static void Build()
        {
            _profiles = new Dictionary<DistrictType, DistrictProfile>();

            Add(new DistrictProfile
            {
                type = DistrictType.Downtown, displayName = "Vireo Heights",
                mapColor = new Color(0.30f, 0.32f, 0.38f),
                minHeight = 45f, maxHeight = 210f, minFloors = 12, maxFloors = 58,
                buildingDensity = 0.97f, blockFill = 0.92f,
                pedDensity = 2.1f, trafficDensity = 1.9f, policePresence = 1.5f, crimeRate = 0.10f, wealth = 0.85f,
                ambientTint = new Color(0.92f, 0.94f, 1f), streetLightSpacing = 26f, treeDensity = 0.06f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Commercial, displayName = "Kestrel Row",
                mapColor = new Color(0.36f, 0.36f, 0.40f),
                minHeight = 14f, maxHeight = 62f, minFloors = 4, maxFloors = 16,
                buildingDensity = 0.92f, blockFill = 0.88f,
                pedDensity = 1.8f, trafficDensity = 1.6f, policePresence = 1.1f, crimeRate = 0.14f, wealth = 0.65f,
                ambientTint = new Color(1f, 0.98f, 0.94f), streetLightSpacing = 30f, treeDensity = 0.12f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Marigold, displayName = "Marigold Quarter",
                mapColor = new Color(0.55f, 0.42f, 0.28f),
                minHeight = 9f, maxHeight = 28f, minFloors = 3, maxFloors = 8,
                buildingDensity = 0.95f, blockFill = 0.94f,
                pedDensity = 2.4f, trafficDensity = 1.3f, policePresence = 0.8f, crimeRate = 0.26f, wealth = 0.35f,
                ambientTint = new Color(1f, 0.95f, 0.86f), streetLightSpacing = 24f, treeDensity = 0.10f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Residential, displayName = "Sable Row",
                mapColor = new Color(0.42f, 0.44f, 0.36f),
                minHeight = 7f, maxHeight = 22f, minFloors = 2, maxFloors = 6,
                buildingDensity = 0.80f, blockFill = 0.72f,
                pedDensity = 1.2f, trafficDensity = 1.0f, policePresence = 0.7f, crimeRate = 0.30f, wealth = 0.30f,
                ambientTint = Color.white, streetLightSpacing = 34f, treeDensity = 0.22f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Wealthy, displayName = "Crestwood Hills",
                mapColor = new Color(0.48f, 0.55f, 0.40f),
                minHeight = 6f, maxHeight = 16f, minFloors = 1, maxFloors = 3,
                buildingDensity = 0.35f, blockFill = 0.38f,
                pedDensity = 0.45f, trafficDensity = 0.6f, policePresence = 1.6f, crimeRate = 0.04f, wealth = 1f,
                ambientTint = new Color(1f, 0.99f, 0.95f), streetLightSpacing = 40f, treeDensity = 0.45f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Industrial, displayName = "Foundry Flats",
                mapColor = new Color(0.38f, 0.34f, 0.30f),
                minHeight = 9f, maxHeight = 34f, minFloors = 1, maxFloors = 4,
                buildingDensity = 0.70f, blockFill = 0.80f,
                pedDensity = 0.55f, trafficDensity = 1.1f, policePresence = 0.5f, crimeRate = 0.34f, wealth = 0.25f,
                ambientTint = new Color(1f, 0.96f, 0.88f), streetLightSpacing = 44f, treeDensity = 0.03f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Port, displayName = "Iron Bay Docks",
                mapColor = new Color(0.32f, 0.38f, 0.42f),
                minHeight = 8f, maxHeight = 30f, minFloors = 1, maxFloors = 3,
                buildingDensity = 0.55f, blockFill = 0.70f,
                pedDensity = 0.5f, trafficDensity = 0.9f, policePresence = 0.6f, crimeRate = 0.40f, wealth = 0.22f,
                ambientTint = new Color(0.94f, 0.97f, 1f), streetLightSpacing = 48f, treeDensity = 0.01f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Airport, displayName = "Redwater International",
                mapColor = new Color(0.44f, 0.44f, 0.48f),
                minHeight = 8f, maxHeight = 26f, minFloors = 1, maxFloors = 3,
                buildingDensity = 0.25f, blockFill = 0.40f,
                pedDensity = 0.7f, trafficDensity = 0.8f, policePresence = 1.8f, crimeRate = 0.03f, wealth = 0.6f,
                ambientTint = Color.white, streetLightSpacing = 55f, treeDensity = 0.02f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.University, displayName = "Kestrel University",
                mapColor = new Color(0.46f, 0.42f, 0.50f),
                minHeight = 10f, maxHeight = 36f, minFloors = 2, maxFloors = 8,
                buildingDensity = 0.5f, blockFill = 0.55f,
                pedDensity = 1.9f, trafficDensity = 0.8f, policePresence = 0.9f, crimeRate = 0.08f, wealth = 0.55f,
                ambientTint = Color.white, streetLightSpacing = 32f, treeDensity = 0.4f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Beach, displayName = "Palmetto Shore",
                mapColor = new Color(0.80f, 0.74f, 0.52f),
                minHeight = 6f, maxHeight = 24f, minFloors = 1, maxFloors = 6,
                buildingDensity = 0.45f, blockFill = 0.55f,
                pedDensity = 1.7f, trafficDensity = 0.9f, policePresence = 0.8f, crimeRate = 0.12f, wealth = 0.6f,
                ambientTint = new Color(1f, 0.97f, 0.90f), streetLightSpacing = 30f, treeDensity = 0.30f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Marina, displayName = "Halcyon Marina",
                mapColor = new Color(0.40f, 0.55f, 0.62f),
                minHeight = 6f, maxHeight = 20f, minFloors = 1, maxFloors = 4,
                buildingDensity = 0.4f, blockFill = 0.5f,
                pedDensity = 0.9f, trafficDensity = 0.7f, policePresence = 1.2f, crimeRate = 0.09f, wealth = 0.9f,
                ambientTint = new Color(0.96f, 0.99f, 1f), streetLightSpacing = 34f, treeDensity = 0.25f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Suburb, displayName = "Junction Falls",
                mapColor = new Color(0.52f, 0.56f, 0.40f),
                minHeight = 5f, maxHeight = 12f, minFloors = 1, maxFloors = 2,
                buildingDensity = 0.35f, blockFill = 0.40f,
                pedDensity = 0.5f, trafficDensity = 0.6f, policePresence = 0.5f, crimeRate = 0.14f, wealth = 0.4f,
                ambientTint = Color.white, streetLightSpacing = 46f, treeDensity = 0.4f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Farmland, displayName = "Cedarbrook Farms",
                mapColor = new Color(0.58f, 0.55f, 0.30f),
                minHeight = 4f, maxHeight = 14f, minFloors = 1, maxFloors = 2,
                buildingDensity = 0.10f, blockFill = 0.18f,
                pedDensity = 0.18f, trafficDensity = 0.28f, policePresence = 0.25f, crimeRate = 0.10f, wealth = 0.3f,
                ambientTint = new Color(1f, 0.99f, 0.92f), streetLightSpacing = 70f, treeDensity = 0.22f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Forest, displayName = "Pinecrest Reserve",
                mapColor = new Color(0.22f, 0.36f, 0.20f),
                minHeight = 4f, maxHeight = 10f, minFloors = 1, maxFloors = 1,
                buildingDensity = 0.03f, blockFill = 0.06f,
                pedDensity = 0.10f, trafficDensity = 0.20f, policePresence = 0.15f, crimeRate = 0.06f, wealth = 0.2f,
                ambientTint = new Color(0.94f, 1f, 0.94f), streetLightSpacing = 90f, treeDensity = 1f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Mountains, displayName = "Mount Cinder",
                mapColor = new Color(0.44f, 0.42f, 0.40f),
                minHeight = 4f, maxHeight = 10f, minFloors = 1, maxFloors = 1,
                buildingDensity = 0.015f, blockFill = 0.03f,
                pedDensity = 0.05f, trafficDensity = 0.14f, policePresence = 0.1f, crimeRate = 0.05f, wealth = 0.25f,
                ambientTint = new Color(0.96f, 0.98f, 1f), streetLightSpacing = 120f, treeDensity = 0.55f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Badlands, displayName = "Dry Wash Badlands",
                mapColor = new Color(0.62f, 0.50f, 0.34f),
                minHeight = 4f, maxHeight = 9f, minFloors = 1, maxFloors = 1,
                buildingDensity = 0.02f, blockFill = 0.04f,
                pedDensity = 0.05f, trafficDensity = 0.16f, policePresence = 0.1f, crimeRate = 0.20f, wealth = 0.15f,
                ambientTint = new Color(1f, 0.96f, 0.86f), streetLightSpacing = 110f, treeDensity = 0.03f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Park, displayName = "Corbin Park",
                mapColor = new Color(0.30f, 0.48f, 0.26f),
                minHeight = 4f, maxHeight = 8f, minFloors = 1, maxFloors = 1,
                buildingDensity = 0.06f, blockFill = 0.10f,
                pedDensity = 1.1f, trafficDensity = 0.3f, policePresence = 0.7f, crimeRate = 0.12f, wealth = 0.5f,
                ambientTint = new Color(0.97f, 1f, 0.96f), streetLightSpacing = 36f, treeDensity = 0.85f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Highway, displayName = "Interstate 9",
                mapColor = new Color(0.34f, 0.34f, 0.34f),
                minHeight = 4f, maxHeight = 8f, minFloors = 1, maxFloors = 1,
                buildingDensity = 0.04f, blockFill = 0.08f,
                pedDensity = 0.05f, trafficDensity = 2.2f, policePresence = 1.2f, crimeRate = 0.06f, wealth = 0.3f,
                ambientTint = Color.white, streetLightSpacing = 50f, treeDensity = 0.06f
            });

            Add(new DistrictProfile
            {
                type = DistrictType.Ocean, displayName = "Halcyon Bay",
                mapColor = new Color(0.13f, 0.30f, 0.45f),
                minHeight = 0f, maxHeight = 0f, minFloors = 0, maxFloors = 0,
                buildingDensity = 0f, blockFill = 0f,
                pedDensity = 0f, trafficDensity = 0f, policePresence = 0.3f, crimeRate = 0f, wealth = 0.3f,
                ambientTint = new Color(0.9f, 0.96f, 1f), streetLightSpacing = 999f, treeDensity = 0f
            });
        }
    }
}
