using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Data
{
    public enum Faction
    {
        Civilian, SMPD, Paramedic, FireDept, SerranoCartel, IronBaySyndicate,
        HalcyonDynamics, VanguardSecurity, CalleNueve, Player, Wildlife
    }

    public enum PedRole
    {
        Pedestrian, Commuter, Worker, Vendor, Guard, Driver, Police, SwatOfficer,
        Medic, Mechanic, Criminal, Gangster, Tourist, Jogger, Executive, Student,
        Dockworker, Farmer, Beachgoer, Homeless, Nightlife
    }

    [CreateAssetMenu(menuName = "San Monica/Ped Archetype", fileName = "Ped")]
    public class PedArchetype : ScriptableObject
    {
        [Header("Identity")]
        public string id = "ped";
        public string displayName = "Citizen";
        public PedRole role = PedRole.Pedestrian;
        public Faction faction = Faction.Civilian;
        public float spawnWeight = 1f;
        public DistrictType[] preferredDistricts;

        [Header("Active hours (24h). Wraps around midnight when start > end.")]
        public int activeFrom = 6;
        public int activeTo = 23;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float armour = 0f;
        public float walkSpeed = 1.35f;
        public float runSpeed = 3.6f;
        public float sprintSpeed = 5.4f;
        public float bravery = 0.2f;        // 0 = flees instantly, 1 = never flees
        public float aggression = 0.05f;    // chance to fight back
        public float alertness = 0.5f;      // how quickly they notice crimes
        public float reportChance = 0.55f;  // chance to call the police as a witness

        [Header("Loadout")]
        public string[] possibleWeapons;
        public float armedChance = 0f;
        public int minCash = 5;
        public int maxCash = 90;

        [Header("Appearance")]
        public float minHeight = 1.68f;
        public float maxHeight = 1.90f;
        public float minBuild = 0.9f;
        public float maxBuild = 1.15f;
        public Color[] skinTones;
        public Color[] hairColors;
        public Color[] shirtColors;
        public Color[] trouserColors;
        public bool wearsUniform = false;
        public bool wearsHat = false;
        public bool wearsVest = false;
        public bool wearsBackpack = false;

        public bool IsActiveAt(int hour)
        {
            if (activeFrom == activeTo) return true;
            if (activeFrom < activeTo) return hour >= activeFrom && hour < activeTo;
            return hour >= activeFrom || hour < activeTo;
        }
    }

    public static class PedCatalogData
    {
        private static List<PedArchetype> _all;
        public static List<PedArchetype> All { get { if (_all == null) Build(); return _all; } }

        public static readonly Color[] SkinTones =
        {
            new Color(0.96f,0.83f,0.72f), new Color(0.90f,0.75f,0.62f), new Color(0.80f,0.63f,0.48f),
            new Color(0.66f,0.48f,0.35f), new Color(0.50f,0.35f,0.25f), new Color(0.36f,0.25f,0.18f),
            new Color(0.27f,0.19f,0.14f)
        };

        public static readonly Color[] HairColors =
        {
            new Color(0.07f,0.06f,0.06f), new Color(0.20f,0.13f,0.08f), new Color(0.36f,0.24f,0.13f),
            new Color(0.62f,0.48f,0.26f), new Color(0.55f,0.24f,0.10f), new Color(0.70f,0.70f,0.72f),
            new Color(0.86f,0.84f,0.80f), new Color(0.35f,0.10f,0.45f)
        };

        private static readonly Color[] CasualShirts =
        {
            new Color(0.85f,0.85f,0.88f), new Color(0.16f,0.20f,0.32f), new Color(0.55f,0.16f,0.18f),
            new Color(0.18f,0.42f,0.32f), new Color(0.92f,0.72f,0.24f), new Color(0.36f,0.36f,0.40f),
            new Color(0.72f,0.44f,0.66f), new Color(0.25f,0.55f,0.70f), new Color(0.10f,0.10f,0.12f)
        };

        private static readonly Color[] Trousers =
        {
            new Color(0.18f,0.22f,0.32f), new Color(0.12f,0.12f,0.14f), new Color(0.35f,0.32f,0.28f),
            new Color(0.42f,0.40f,0.36f), new Color(0.22f,0.26f,0.22f), new Color(0.55f,0.52f,0.48f)
        };

        private static PedArchetype P(string id, string name, PedRole role, Faction f)
        {
            var p = ScriptableObject.CreateInstance<PedArchetype>();
            p.name = "Ped_" + id;
            p.id = id; p.displayName = name; p.role = role; p.faction = f;
            p.skinTones = SkinTones; p.hairColors = HairColors;
            p.shirtColors = CasualShirts; p.trouserColors = Trousers;
            return p;
        }

        private static void Build()
        {
            _all = new List<PedArchetype>();
            PedArchetype p;

            p = P("citizen", "Citizen", PedRole.Pedestrian, Faction.Civilian);
            p.spawnWeight = 8f; p.minCash = 10; p.maxCash = 140;
            _all.Add(p);

            p = P("commuter", "Commuter", PedRole.Commuter, Faction.Civilian);
            p.spawnWeight = 5f; p.activeFrom = 6; p.activeTo = 20; p.walkSpeed = 1.55f;
            p.wearsBackpack = true; p.minCash = 20; p.maxCash = 220;
            _all.Add(p);

            p = P("executive", "Executive", PedRole.Executive, Faction.Civilian);
            p.spawnWeight = 2.4f; p.activeFrom = 7; p.activeTo = 20; p.walkSpeed = 1.45f;
            p.shirtColors = new[] { new Color(0.92f,0.92f,0.94f), new Color(0.82f,0.84f,0.88f) };
            p.trouserColors = new[] { new Color(0.12f,0.13f,0.18f), new Color(0.20f,0.20f,0.24f) };
            p.minCash = 200; p.maxCash = 1400;
            p.preferredDistricts = new[] { DistrictType.Downtown, DistrictType.Commercial, DistrictType.Wealthy };
            _all.Add(p);

            p = P("student", "Student", PedRole.Student, Faction.Civilian);
            p.spawnWeight = 3f; p.activeFrom = 7; p.activeTo = 23; p.wearsBackpack = true;
            p.minCash = 5; p.maxCash = 80; p.minHeight = 1.62f; p.maxHeight = 1.84f;
            p.preferredDistricts = new[] { DistrictType.University, DistrictType.Marigold, DistrictType.Commercial };
            _all.Add(p);

            p = P("worker", "Construction Worker", PedRole.Worker, Faction.Civilian);
            p.spawnWeight = 2.6f; p.activeFrom = 6; p.activeTo = 18; p.wearsHat = true; p.wearsVest = true;
            p.maxHealth = 120f; p.bravery = 0.35f; p.aggression = 0.15f; p.minBuild = 1.0f; p.maxBuild = 1.25f;
            p.shirtColors = new[] { new Color(0.94f,0.55f,0.08f), new Color(0.90f,0.86f,0.20f) };
            p.minCash = 40; p.maxCash = 260;
            p.preferredDistricts = new[] { DistrictType.Industrial, DistrictType.Port, DistrictType.Downtown, DistrictType.Commercial };
            _all.Add(p);

            p = P("dockworker", "Dockworker", PedRole.Dockworker, Faction.Civilian);
            p.spawnWeight = 1.8f; p.activeFrom = 5; p.activeTo = 22; p.wearsVest = true; p.maxHealth = 125f;
            p.bravery = 0.42f; p.aggression = 0.22f; p.minBuild = 1.05f; p.maxBuild = 1.3f;
            p.preferredDistricts = new[] { DistrictType.Port, DistrictType.Industrial };
            _all.Add(p);

            p = P("vendor", "Street Vendor", PedRole.Vendor, Faction.Civilian);
            p.spawnWeight = 1.4f; p.activeFrom = 7; p.activeTo = 22; p.walkSpeed = 0.9f;
            p.minCash = 80; p.maxCash = 500;
            p.preferredDistricts = new[] { DistrictType.Marigold, DistrictType.Beach, DistrictType.Commercial, DistrictType.Park };
            _all.Add(p);

            p = P("mechanic", "Mechanic", PedRole.Mechanic, Faction.Civilian);
            p.spawnWeight = 0.9f; p.activeFrom = 8; p.activeTo = 21; p.wearsUniform = true;
            p.shirtColors = new[] { new Color(0.20f,0.26f,0.36f), new Color(0.28f,0.28f,0.30f) };
            p.maxHealth = 115f; p.bravery = 0.35f;
            p.preferredDistricts = new[] { DistrictType.Industrial, DistrictType.Residential, DistrictType.Suburb };
            _all.Add(p);

            p = P("jogger", "Jogger", PedRole.Jogger, Faction.Civilian);
            p.spawnWeight = 1.6f; p.activeFrom = 5; p.activeTo = 21; p.walkSpeed = 2.9f; p.runSpeed = 4.4f;
            p.minCash = 0; p.maxCash = 30; p.bravery = 0.1f;
            p.preferredDistricts = new[] { DistrictType.Park, DistrictType.Beach, DistrictType.Wealthy, DistrictType.Marina };
            _all.Add(p);

            p = P("beachgoer", "Beachgoer", PedRole.Beachgoer, Faction.Civilian);
            p.spawnWeight = 1.5f; p.activeFrom = 8; p.activeTo = 20;
            p.shirtColors = new[] { new Color(0.95f,0.60f,0.30f), new Color(0.30f,0.75f,0.85f), new Color(0.95f,0.90f,0.35f) };
            p.preferredDistricts = new[] { DistrictType.Beach, DistrictType.Marina };
            _all.Add(p);

            p = P("tourist", "Tourist", PedRole.Tourist, Faction.Civilian);
            p.spawnWeight = 1.3f; p.activeFrom = 8; p.activeTo = 23; p.wearsHat = true; p.wearsBackpack = true;
            p.minCash = 60; p.maxCash = 600; p.walkSpeed = 1.05f;
            p.preferredDistricts = new[] { DistrictType.Downtown, DistrictType.Beach, DistrictType.Marina, DistrictType.Airport, DistrictType.Park };
            _all.Add(p);

            p = P("nightlife", "Club Goer", PedRole.Nightlife, Faction.Civilian);
            p.spawnWeight = 2.2f; p.activeFrom = 20; p.activeTo = 4;
            p.shirtColors = new[] { new Color(0.10f,0.10f,0.14f), new Color(0.62f,0.10f,0.42f), new Color(0.86f,0.86f,0.90f) };
            p.minCash = 40; p.maxCash = 420;
            p.preferredDistricts = new[] { DistrictType.Downtown, DistrictType.Marigold, DistrictType.Commercial, DistrictType.Beach };
            _all.Add(p);

            p = P("homeless", "Drifter", PedRole.Homeless, Faction.Civilian);
            p.spawnWeight = 1.1f; p.activeFrom = 0; p.activeTo = 0; p.walkSpeed = 0.85f;
            p.minCash = 0; p.maxCash = 25; p.bravery = 0.4f; p.alertness = 0.3f; p.reportChance = 0.2f;
            p.shirtColors = new[] { new Color(0.36f,0.32f,0.26f), new Color(0.28f,0.30f,0.26f) };
            p.preferredDistricts = new[] { DistrictType.Marigold, DistrictType.Industrial, DistrictType.Residential, DistrictType.Port };
            _all.Add(p);

            p = P("farmer", "Farmhand", PedRole.Farmer, Faction.Civilian);
            p.spawnWeight = 0.9f; p.activeFrom = 5; p.activeTo = 19; p.wearsHat = true; p.maxHealth = 115f;
            p.bravery = 0.45f; p.aggression = 0.25f; p.armedChance = 0.18f;
            p.possibleWeapons = new[] { "pump" };
            p.preferredDistricts = new[] { DistrictType.Farmland, DistrictType.Suburb, DistrictType.Badlands };
            _all.Add(p);

            // ---- Authority ----
            p = P("cop", "SMPD Officer", PedRole.Police, Faction.SMPD);
            p.spawnWeight = 0f; p.maxHealth = 150f; p.armour = 45f; p.bravery = 0.95f; p.aggression = 1f;
            p.alertness = 1f; p.walkSpeed = 1.5f; p.runSpeed = 4.6f; p.sprintSpeed = 6.2f;
            p.wearsUniform = true; p.wearsHat = true; p.wearsVest = true; p.armedChance = 1f;
            p.possibleWeapons = new[] { "p9", "p9", "pump", "smg-9", "baton" };
            p.shirtColors = new[] { new Color(0.13f,0.17f,0.28f) };
            p.trouserColors = new[] { new Color(0.11f,0.14f,0.22f) };
            p.minCash = 0; p.maxCash = 0; p.activeFrom = 0; p.activeTo = 0;
            _all.Add(p);

            p = P("swat", "SMPD Tactical", PedRole.SwatOfficer, Faction.SMPD);
            p.spawnWeight = 0f; p.maxHealth = 190f; p.armour = 120f; p.bravery = 1f; p.aggression = 1f;
            p.alertness = 1f; p.runSpeed = 4.4f; p.sprintSpeed = 5.8f; p.wearsUniform = true;
            p.wearsHat = true; p.wearsVest = true; p.armedChance = 1f;
            p.possibleWeapons = new[] { "carbine", "burst-carbine", "auto-shotgun" };
            p.shirtColors = new[] { new Color(0.10f,0.11f,0.13f) };
            p.trouserColors = new[] { new Color(0.09f,0.10f,0.12f) };
            p.minBuild = 1.1f; p.maxBuild = 1.3f; p.activeFrom = 0; p.activeTo = 0;
            _all.Add(p);

            p = P("medic", "Paramedic", PedRole.Medic, Faction.Paramedic);
            p.spawnWeight = 0.25f; p.maxHealth = 120f; p.bravery = 0.6f; p.wearsUniform = true;
            p.shirtColors = new[] { new Color(0.90f,0.90f,0.92f) };
            p.trouserColors = new[] { new Color(0.16f,0.34f,0.48f) };
            p.activeFrom = 0; p.activeTo = 0;
            _all.Add(p);

            p = P("security", "Vanguard Guard", PedRole.Guard, Faction.VanguardSecurity);
            p.spawnWeight = 0.6f; p.maxHealth = 145f; p.armour = 60f; p.bravery = 0.85f; p.aggression = 0.7f;
            p.alertness = 0.9f; p.wearsUniform = true; p.wearsVest = true; p.armedChance = 0.85f;
            p.possibleWeapons = new[] { "p9", "smg-9", "baton" };
            p.shirtColors = new[] { new Color(0.15f,0.16f,0.18f) };
            p.trouserColors = new[] { new Color(0.13f,0.14f,0.16f) };
            p.activeFrom = 0; p.activeTo = 0;
            p.preferredDistricts = new[] { DistrictType.Downtown, DistrictType.Wealthy, DistrictType.Airport, DistrictType.Marina, DistrictType.Industrial };
            _all.Add(p);

            // ---- Criminal factions ----
            p = P("serrano", "Serrano Soldier", PedRole.Gangster, Faction.SerranoCartel);
            p.spawnWeight = 0.8f; p.maxHealth = 130f; p.armour = 20f; p.bravery = 0.8f; p.aggression = 0.85f;
            p.alertness = 0.8f; p.armedChance = 0.75f; p.reportChance = 0f;
            p.possibleWeapons = new[] { "p9", "machine-pistol", "smg-9", "machete", "sawn-off", "knife" };
            p.shirtColors = new[] { new Color(0.62f,0.12f,0.14f), new Color(0.10f,0.10f,0.12f) };
            p.minCash = 100; p.maxCash = 900; p.activeFrom = 0; p.activeTo = 0;
            p.preferredDistricts = new[] { DistrictType.Marigold, DistrictType.Industrial, DistrictType.Badlands };
            _all.Add(p);

            p = P("ironbay", "Iron Bay Enforcer", PedRole.Gangster, Faction.IronBaySyndicate);
            p.spawnWeight = 0.7f; p.maxHealth = 140f; p.armour = 35f; p.bravery = 0.85f; p.aggression = 0.8f;
            p.alertness = 0.85f; p.armedChance = 0.8f; p.reportChance = 0f;
            p.possibleWeapons = new[] { "p9-heavy", "pump", "smg-heavy", "wrench", "sawn-off" };
            p.shirtColors = new[] { new Color(0.14f,0.20f,0.26f), new Color(0.20f,0.22f,0.24f) };
            p.minBuild = 1.05f; p.maxBuild = 1.3f; p.minCash = 120; p.maxCash = 1100;
            p.activeFrom = 0; p.activeTo = 0;
            p.preferredDistricts = new[] { DistrictType.Port, DistrictType.Industrial };
            _all.Add(p);

            p = P("callenueve", "Calle Nueve Crew", PedRole.Gangster, Faction.CalleNueve);
            p.spawnWeight = 0.75f; p.maxHealth = 115f; p.bravery = 0.65f; p.aggression = 0.7f;
            p.alertness = 0.7f; p.armedChance = 0.55f; p.reportChance = 0f;
            p.possibleWeapons = new[] { "p9", "bat", "machine-pistol", "knife" };
            p.shirtColors = new[] { new Color(0.16f,0.42f,0.62f), new Color(0.90f,0.90f,0.92f) };
            p.minCash = 40; p.maxCash = 500; p.activeFrom = 0; p.activeTo = 0;
            p.preferredDistricts = new[] { DistrictType.Residential, DistrictType.Marigold };
            _all.Add(p);

            p = P("mugger", "Mugger", PedRole.Criminal, Faction.Civilian);
            p.spawnWeight = 0.45f; p.maxHealth = 105f; p.bravery = 0.5f; p.aggression = 0.6f;
            p.armedChance = 0.4f; p.reportChance = 0f; p.activeFrom = 20; p.activeTo = 5;
            p.possibleWeapons = new[] { "machete", "p9", "knife" };
            p.shirtColors = new[] { new Color(0.14f,0.14f,0.16f), new Color(0.24f,0.22f,0.20f) };
            p.minCash = 30; p.maxCash = 320;
            p.preferredDistricts = new[] { DistrictType.Marigold, DistrictType.Residential, DistrictType.Industrial, DistrictType.Park };
            _all.Add(p);

            p = P("halcyon", "Halcyon Operative", PedRole.Guard, Faction.HalcyonDynamics);
            p.spawnWeight = 0.2f; p.maxHealth = 160f; p.armour = 90f; p.bravery = 1f; p.aggression = 0.9f;
            p.alertness = 1f; p.armedChance = 1f; p.reportChance = 0f;
            p.possibleWeapons = new[] { "carbine", "battle-rifle", "dmr", "p9-suppressed" };
            p.shirtColors = new[] { new Color(0.16f,0.20f,0.24f) };
            p.trouserColors = new[] { new Color(0.14f,0.16f,0.20f) };
            p.wearsVest = true; p.minBuild = 1.05f; p.maxBuild = 1.25f;
            p.activeFrom = 0; p.activeTo = 0;
            p.preferredDistricts = new[] { DistrictType.Downtown, DistrictType.Airport };
            _all.Add(p);
        }
    }
}
