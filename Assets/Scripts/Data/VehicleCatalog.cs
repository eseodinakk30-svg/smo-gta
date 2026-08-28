using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Data
{
    /// <summary>
    /// The San Monica vehicle line-up: 42 original vehicles across every class,
    /// all built procedurally from these parameters at runtime.
    /// </summary>
    public static class VehicleCatalogData
    {
        private static List<VehicleDefinition> _all;

        public static List<VehicleDefinition> All
        {
            get { if (_all == null) Build(); return _all; }
        }

        private static readonly Color[] CommonPaints =
        {
            new Color(0.82f,0.82f,0.84f), new Color(0.08f,0.08f,0.09f), new Color(0.55f,0.57f,0.60f),
            new Color(0.62f,0.12f,0.12f), new Color(0.10f,0.24f,0.48f), new Color(0.16f,0.36f,0.24f),
            new Color(0.90f,0.88f,0.80f), new Color(0.35f,0.20f,0.12f), new Color(0.20f,0.42f,0.55f)
        };

        private static readonly Color[] LoudPaints =
        {
            new Color(0.92f,0.16f,0.10f), new Color(0.98f,0.72f,0.05f), new Color(0.05f,0.55f,0.90f),
            new Color(0.10f,0.80f,0.42f), new Color(0.85f,0.10f,0.62f), new Color(0.96f,0.96f,0.96f),
            new Color(0.05f,0.05f,0.07f), new Color(0.55f,0.10f,0.85f)
        };

        private static VehicleDefinition V(string id, string name, string maker, VehicleClass cls)
        {
            var v = ScriptableObject.CreateInstance<VehicleDefinition>();
            v.name = "Veh_" + id;
            v.id = id; v.displayName = name; v.manufacturer = maker; v.vehicleClass = cls;
            v.paintOptions = CommonPaints;
            return v;
        }

        private static void Build()
        {
            _all = new List<VehicleDefinition>();
            VehicleDefinition v;

            // ---------- Compacts & sedans ----------
            v = V("wren", "Wren", "Corvale", VehicleClass.Compact);
            v.length = 3.9f; v.width = 1.72f; v.height = 1.48f; v.mass = 1080f; v.enginePower = 78f;
            v.topSpeedKph = 158f; v.wheelbase = 2.45f; v.track = 1.48f; v.wheelRadius = 0.31f;
            v.driveType = DriveType.FrontWheel; v.price = 9500; v.spawnWeight = 3.2f; v.engineBaseHz = 70f;
            _all.Add(v);

            v = V("meridian", "Meridian", "Corvale", VehicleClass.Sedan);
            v.length = 4.72f; v.width = 1.84f; v.height = 1.44f; v.mass = 1460f; v.enginePower = 135f;
            v.topSpeedKph = 192f; v.price = 21000; v.spawnWeight = 4f; v.driveType = DriveType.FrontWheel;
            _all.Add(v);

            v = V("harborline", "Harborline", "Ashford", VehicleClass.Sedan);
            v.length = 4.95f; v.width = 1.90f; v.height = 1.47f; v.mass = 1610f; v.enginePower = 168f;
            v.topSpeedKph = 205f; v.price = 34000; v.spawnWeight = 2.6f;
            _all.Add(v);

            v = V("regent", "Regent", "Ashford", VehicleClass.Sedan);
            v.length = 5.25f; v.width = 1.96f; v.height = 1.50f; v.mass = 1880f; v.enginePower = 240f;
            v.topSpeedKph = 228f; v.price = 78000; v.spawnWeight = 1.1f; v.glassTint = 0.6f;
            v.preferredDistricts = new[] { DistrictType.Wealthy, DistrictType.Downtown, DistrictType.Marina };
            _all.Add(v);

            v = V("pallas", "Pallas", "Nordvik", VehicleClass.Sedan);
            v.length = 4.86f; v.width = 1.88f; v.height = 1.45f; v.mass = 1540f; v.enginePower = 185f;
            v.topSpeedKph = 214f; v.price = 46000; v.spawnWeight = 1.8f; v.driveType = DriveType.AllWheel;
            _all.Add(v);

            // ---------- Coupes / sports ----------
            v = V("kestrel-gt", "Kestrel GT", "Vireo Motors", VehicleClass.Coupe);
            v.length = 4.48f; v.width = 1.88f; v.height = 1.32f; v.mass = 1380f; v.enginePower = 265f;
            v.topSpeedKph = 249f; v.price = 62000; v.spawnWeight = 1.4f; v.grip = 1.12f; v.seats = 2;
            v.cabinLengthRatio = 0.40f; v.roofTaper = 0.70f; v.paintOptions = LoudPaints; v.engineBaseHz = 58f;
            _all.Add(v);

            v = V("solano", "Solano", "Vireo Motors", VehicleClass.Sports);
            v.length = 4.36f; v.width = 1.92f; v.height = 1.24f; v.mass = 1290f; v.enginePower = 340f;
            v.topSpeedKph = 278f; v.price = 118000; v.spawnWeight = 0.7f; v.grip = 1.22f; v.seats = 2;
            v.cabinLengthRatio = 0.36f; v.noseSlope = 0.26f; v.roofTaper = 0.62f; v.rideHeight = 0.18f;
            v.paintOptions = LoudPaints; v.turboWhistle = 0.4f; v.engineBaseHz = 54f;
            v.preferredDistricts = new[] { DistrictType.Wealthy, DistrictType.Downtown, DistrictType.Marina, DistrictType.Beach };
            _all.Add(v);

            v = V("tessarae", "Tessarae", "Falcorne", VehicleClass.Super);
            v.length = 4.55f; v.width = 2.02f; v.height = 1.14f; v.mass = 1350f; v.enginePower = 545f;
            v.topSpeedKph = 336f; v.price = 890000; v.spawnWeight = 0.10f; v.grip = 1.42f; v.seats = 2;
            v.cabinLengthRatio = 0.32f; v.noseSlope = 0.34f; v.roofTaper = 0.55f; v.rideHeight = 0.12f;
            v.downforce = 190f; v.wheelRadius = 0.36f; v.paintOptions = LoudPaints; v.turboWhistle = 0.75f;
            v.engineBaseHz = 48f; v.gearCount = 8;
            v.preferredDistricts = new[] { DistrictType.Wealthy, DistrictType.Marina };
            _all.Add(v);

            v = V("obsidian-x", "Obsidian X", "Falcorne", VehicleClass.Super);
            v.length = 4.70f; v.width = 2.06f; v.height = 1.10f; v.mass = 1420f; v.enginePower = 620f;
            v.topSpeedKph = 352f; v.price = 1450000; v.spawnWeight = 0.05f; v.grip = 1.48f; v.seats = 2;
            v.cabinLengthRatio = 0.30f; v.noseSlope = 0.38f; v.roofTaper = 0.50f; v.rideHeight = 0.10f;
            v.downforce = 240f; v.paintOptions = LoudPaints; v.turboWhistle = 0.9f; v.engineBaseHz = 45f; v.gearCount = 8;
            v.preferredDistricts = new[] { DistrictType.Wealthy };
            _all.Add(v);

            v = V("brawler", "Brawler 440", "Steadman", VehicleClass.Muscle);
            v.length = 5.02f; v.width = 1.98f; v.height = 1.36f; v.mass = 1720f; v.enginePower = 330f;
            v.topSpeedKph = 246f; v.price = 54000; v.spawnWeight = 1.2f; v.grip = 0.94f; v.seats = 2;
            v.cabinLengthRatio = 0.42f; v.engineBaseHz = 42f; v.engineHarshness = 0.72f;
            v.preferredDistricts = new[] { DistrictType.Residential, DistrictType.Suburb, DistrictType.Badlands };
            _all.Add(v);

            v = V("dominator", "Dominator", "Steadman", VehicleClass.Muscle);
            v.length = 5.18f; v.width = 2.00f; v.height = 1.38f; v.mass = 1810f; v.enginePower = 385f;
            v.topSpeedKph = 258f; v.price = 71000; v.spawnWeight = 0.8f; v.grip = 0.92f; v.seats = 2;
            v.engineBaseHz = 40f; v.engineHarshness = 0.8f; v.paintOptions = LoudPaints;
            _all.Add(v);

            // ---------- SUVs / pickups / vans ----------
            v = V("ridgeback", "Ridgeback", "Nordvik", VehicleClass.SUV);
            v.length = 4.86f; v.width = 1.98f; v.height = 1.82f; v.mass = 2150f; v.enginePower = 190f;
            v.topSpeedKph = 186f; v.price = 42000; v.spawnWeight = 3f; v.driveType = DriveType.AllWheel;
            v.rideHeight = 0.42f; v.wheelRadius = 0.38f; v.cabinHeightRatio = 0.48f; v.hasRoofRack = true; v.seats = 5;
            _all.Add(v);

            v = V("summit", "Summit XL", "Nordvik", VehicleClass.SUV);
            v.length = 5.20f; v.width = 2.05f; v.height = 1.92f; v.mass = 2480f; v.enginePower = 260f;
            v.topSpeedKph = 198f; v.price = 96000; v.spawnWeight = 1.4f; v.driveType = DriveType.AllWheel;
            v.rideHeight = 0.44f; v.wheelRadius = 0.40f; v.seats = 7; v.glassTint = 0.62f;
            v.preferredDistricts = new[] { DistrictType.Wealthy, DistrictType.Suburb };
            _all.Add(v);

            v = V("packhorse", "Packhorse", "Steadman", VehicleClass.Pickup);
            v.length = 5.52f; v.width = 2.02f; v.height = 1.88f; v.mass = 2320f; v.enginePower = 215f;
            v.topSpeedKph = 176f; v.price = 38000; v.spawnWeight = 2.4f; v.driveType = DriveType.AllWheel;
            v.rideHeight = 0.46f; v.wheelRadius = 0.40f; v.hasBed = true; v.cabinLengthRatio = 0.34f; v.seats = 4;
            v.preferredDistricts = new[] { DistrictType.Farmland, DistrictType.Suburb, DistrictType.Industrial, DistrictType.Badlands };
            _all.Add(v);

            v = V("mule", "Mule", "Steadman", VehicleClass.Pickup);
            v.length = 5.10f; v.width = 1.92f; v.height = 1.80f; v.mass = 1980f; v.enginePower = 160f;
            v.topSpeedKph = 162f; v.price = 24000; v.spawnWeight = 2.2f; v.hasBed = true;
            v.cabinLengthRatio = 0.32f; v.rideHeight = 0.40f; v.seats = 2;
            _all.Add(v);

            v = V("courier", "Courier", "Corvale", VehicleClass.Van);
            v.length = 5.40f; v.width = 2.00f; v.height = 2.32f; v.mass = 2280f; v.enginePower = 145f;
            v.topSpeedKph = 152f; v.price = 27000; v.spawnWeight = 2f; v.hasCargoBox = true;
            v.cabinLengthRatio = 0.28f; v.cabinHeightRatio = 0.62f; v.seats = 3; v.driveType = DriveType.FrontWheel;
            _all.Add(v);

            v = V("hauler", "Hauler 12", "Brackett", VehicleClass.Truck);
            v.length = 8.60f; v.width = 2.45f; v.height = 3.30f; v.mass = 7800f; v.enginePower = 320f;
            v.topSpeedKph = 128f; v.price = 88000; v.spawnWeight = 0.9f; v.hasCargoBox = true;
            v.cabinLengthRatio = 0.24f; v.cabinHeightRatio = 0.70f; v.wheelRadius = 0.52f; v.wheelbase = 5.2f;
            v.track = 2.02f; v.wheelCount = 6; v.seats = 2; v.engineBaseHz = 32f; v.brakeTorque = 9000f;
            v.preferredDistricts = new[] { DistrictType.Industrial, DistrictType.Port, DistrictType.Highway };
            _all.Add(v);

            v = V("cityliner", "Cityliner", "Brackett", VehicleClass.Bus);
            v.length = 11.4f; v.width = 2.52f; v.height = 3.15f; v.mass = 11500f; v.enginePower = 280f;
            v.topSpeedKph = 108f; v.price = 145000; v.spawnWeight = 0.6f; v.cabinLengthRatio = 0.92f;
            v.cabinHeightRatio = 0.72f; v.wheelRadius = 0.52f; v.wheelbase = 6.4f; v.track = 2.1f;
            v.wheelCount = 6; v.seats = 24; v.engineBaseHz = 34f; v.brakeTorque = 11000f;
            v.paintOptions = new[] { new Color(0.20f,0.42f,0.62f), new Color(0.85f,0.82f,0.72f), new Color(0.24f,0.48f,0.34f) };
            _all.Add(v);

            // ---------- Service & emergency ----------
            v = V("taxi", "Meridian Cab", "Corvale", VehicleClass.Taxi);
            v.length = 4.72f; v.width = 1.84f; v.height = 1.46f; v.mass = 1500f; v.enginePower = 138f;
            v.topSpeedKph = 186f; v.price = 26000; v.spawnWeight = 1.8f;
            v.paintOptions = new[] { new Color(0.95f,0.76f,0.06f) };
            v.preferredDistricts = new[] { DistrictType.Downtown, DistrictType.Commercial, DistrictType.Marigold, DistrictType.Airport };
            _all.Add(v);

            v = V("patrol", "SMPD Patrol", "Ashford", VehicleClass.Police);
            v.length = 4.98f; v.width = 1.92f; v.height = 1.48f; v.mass = 1720f; v.enginePower = 245f;
            v.topSpeedKph = 232f; v.price = 0; v.spawnWeight = 0f; v.hasSiren = true; v.grip = 1.06f;
            v.paintOptions = new[] { new Color(0.92f,0.92f,0.94f) }; v.crashResistance = 1.3f; v.maxHealth = 1400f;
            _all.Add(v);

            v = V("interceptor", "SMPD Interceptor", "Vireo Motors", VehicleClass.Police);
            v.length = 4.60f; v.width = 1.92f; v.height = 1.30f; v.mass = 1420f; v.enginePower = 360f;
            v.topSpeedKph = 286f; v.hasSiren = true; v.grip = 1.28f; v.seats = 2; v.spawnWeight = 0f;
            v.cabinLengthRatio = 0.40f; v.paintOptions = new[] { new Color(0.10f,0.10f,0.13f) };
            v.crashResistance = 1.35f; v.maxHealth = 1500f;
            _all.Add(v);

            v = V("enforcer", "SMPD Enforcer", "Brackett", VehicleClass.Police);
            v.length = 6.20f; v.width = 2.35f; v.height = 2.85f; v.mass = 5400f; v.enginePower = 300f;
            v.topSpeedKph = 145f; v.hasSiren = true; v.hasCargoBox = true; v.spawnWeight = 0f;
            v.cabinLengthRatio = 0.30f; v.cabinHeightRatio = 0.66f; v.wheelRadius = 0.46f; v.wheelbase = 3.9f;
            v.track = 1.95f; v.seats = 8; v.crashResistance = 2.2f; v.maxHealth = 3200f;
            v.paintOptions = new[] { new Color(0.16f,0.18f,0.22f) };
            _all.Add(v);

            v = V("ambulance", "Medivan", "Brackett", VehicleClass.Ambulance);
            v.length = 6.05f; v.width = 2.25f; v.height = 2.75f; v.mass = 4200f; v.enginePower = 220f;
            v.topSpeedKph = 152f; v.hasSiren = true; v.hasCargoBox = true; v.spawnWeight = 0.25f;
            v.cabinLengthRatio = 0.28f; v.cabinHeightRatio = 0.66f; v.wheelRadius = 0.44f; v.seats = 4;
            v.paintOptions = new[] { new Color(0.95f,0.95f,0.96f) }; v.maxHealth = 1800f;
            _all.Add(v);

            v = V("firetruck", "Torrent Pumper", "Brackett", VehicleClass.FireTruck);
            v.length = 9.10f; v.width = 2.55f; v.height = 3.35f; v.mass = 13500f; v.enginePower = 400f;
            v.topSpeedKph = 118f; v.hasSiren = true; v.hasCargoBox = true; v.spawnWeight = 0.12f;
            v.cabinLengthRatio = 0.26f; v.cabinHeightRatio = 0.70f; v.wheelRadius = 0.55f; v.wheelbase = 5.4f;
            v.track = 2.15f; v.wheelCount = 6; v.seats = 6; v.maxHealth = 4200f; v.crashResistance = 2.6f;
            v.paintOptions = new[] { new Color(0.72f,0.09f,0.08f) };
            _all.Add(v);

            v = V("towtruck", "Grapple", "Brackett", VehicleClass.Utility);
            v.length = 6.30f; v.width = 2.20f; v.height = 2.55f; v.mass = 4800f; v.enginePower = 200f;
            v.topSpeedKph = 132f; v.spawnWeight = 0.5f; v.hasBed = true; v.cabinLengthRatio = 0.30f;
            v.wheelRadius = 0.46f; v.seats = 2;
            v.preferredDistricts = new[] { DistrictType.Industrial, DistrictType.Port };
            _all.Add(v);

            v = V("garbage", "Sanitation 9", "Brackett", VehicleClass.Truck);
            v.length = 8.20f; v.width = 2.45f; v.height = 3.20f; v.mass = 9200f; v.enginePower = 260f;
            v.topSpeedKph = 105f; v.spawnWeight = 0.45f; v.hasCargoBox = true; v.cabinLengthRatio = 0.24f;
            v.wheelRadius = 0.50f; v.wheelbase = 4.9f; v.wheelCount = 6; v.seats = 3;
            v.paintOptions = new[] { new Color(0.28f,0.45f,0.30f) };
            _all.Add(v);

            v = V("tractor", "Cedarbrook Tractor", "Steadman", VehicleClass.Utility);
            v.length = 4.20f; v.width = 2.10f; v.height = 2.70f; v.mass = 5200f; v.enginePower = 110f;
            v.topSpeedKph = 42f; v.spawnWeight = 0.3f; v.wheelRadius = 0.78f; v.track = 1.85f;
            v.wheelbase = 2.4f; v.seats = 1; v.cabinHeightRatio = 0.55f; v.driveType = DriveType.AllWheel;
            v.preferredDistricts = new[] { DistrictType.Farmland };
            v.paintOptions = new[] { new Color(0.20f,0.44f,0.22f), new Color(0.72f,0.16f,0.12f) };
            _all.Add(v);

            v = V("forklift", "Dockhand", "Brackett", VehicleClass.Utility);
            v.length = 2.80f; v.width = 1.35f; v.height = 2.20f; v.mass = 2600f; v.enginePower = 60f;
            v.topSpeedKph = 28f; v.spawnWeight = 0.25f; v.wheelRadius = 0.32f; v.track = 1.10f;
            v.wheelbase = 1.60f; v.seats = 1;
            v.preferredDistricts = new[] { DistrictType.Port, DistrictType.Industrial };
            v.paintOptions = new[] { new Color(0.95f,0.62f,0.05f) };
            _all.Add(v);

            // ---------- Two wheels ----------
            v = V("wasp", "Wasp 125", "Corvale", VehicleClass.Scooter);
            v.length = 1.90f; v.width = 0.72f; v.height = 1.18f; v.mass = 128f; v.enginePower = 9f;
            v.topSpeedKph = 92f; v.price = 3200; v.spawnWeight = 1.6f; v.wheelCount = 2;
            v.wheelRadius = 0.26f; v.wheelbase = 1.32f; v.track = 0f; v.seats = 2; v.engineBaseHz = 118f;
            v.driveType = DriveType.RearWheel; v.grip = 0.90f; v.paintOptions = LoudPaints;
            _all.Add(v);

            v = V("shrike", "Shrike 900", "Vireo Motors", VehicleClass.Motorcycle);
            v.length = 2.12f; v.width = 0.78f; v.height = 1.14f; v.mass = 196f; v.enginePower = 105f;
            v.topSpeedKph = 268f; v.price = 24000; v.spawnWeight = 1.1f; v.wheelCount = 2;
            v.wheelRadius = 0.32f; v.wheelbase = 1.44f; v.track = 0f; v.seats = 2; v.engineBaseHz = 92f;
            v.grip = 1.05f; v.paintOptions = LoudPaints; v.acceleration = 1.35f;
            _all.Add(v);

            v = V("nomad", "Nomad Cruiser", "Steadman", VehicleClass.Motorcycle);
            v.length = 2.42f; v.width = 0.88f; v.height = 1.22f; v.mass = 288f; v.enginePower = 72f;
            v.topSpeedKph = 198f; v.price = 18500; v.spawnWeight = 0.9f; v.wheelCount = 2;
            v.wheelRadius = 0.36f; v.wheelbase = 1.68f; v.track = 0f; v.seats = 2; v.engineBaseHz = 48f;
            v.engineHarshness = 0.85f; v.grip = 0.95f;
            _all.Add(v);

            v = V("dirtbike", "Scrub 250", "Steadman", VehicleClass.Motorcycle);
            v.length = 2.05f; v.width = 0.80f; v.height = 1.28f; v.mass = 132f; v.enginePower = 32f;
            v.topSpeedKph = 142f; v.price = 7400; v.spawnWeight = 0.7f; v.wheelCount = 2;
            v.wheelRadius = 0.38f; v.wheelbase = 1.46f; v.track = 0f; v.seats = 1; v.engineBaseHz = 105f;
            v.rideHeight = 0.42f; v.grip = 1.0f;
            v.preferredDistricts = new[] { DistrictType.Badlands, DistrictType.Forest, DistrictType.Farmland, DistrictType.Mountains };
            _all.Add(v);

            // ---------- Water ----------
            v = V("skiff", "Bayrunner", "Halcyon Marine", VehicleClass.Boat);
            v.length = 5.60f; v.width = 2.10f; v.height = 1.55f; v.mass = 1250f; v.enginePower = 140f;
            v.topSpeedKph = 96f; v.price = 32000; v.spawnWeight = 0.8f; v.buoyancy = 1.35f; v.waterDrag = 2.2f;
            v.seats = 4; v.wheelCount = 0; v.engineBaseHz = 66f;
            v.preferredDistricts = new[] { DistrictType.Marina, DistrictType.Port, DistrictType.Beach };
            _all.Add(v);

            v = V("dartboat", "Halcyon Dart", "Halcyon Marine", VehicleClass.Boat);
            v.length = 7.40f; v.width = 2.35f; v.height = 1.72f; v.mass = 1850f; v.enginePower = 320f;
            v.topSpeedKph = 138f; v.price = 128000; v.spawnWeight = 0.4f; v.buoyancy = 1.30f; v.waterDrag = 1.7f;
            v.seats = 6; v.wheelCount = 0; v.engineBaseHz = 58f; v.paintOptions = LoudPaints;
            v.preferredDistricts = new[] { DistrictType.Marina };
            _all.Add(v);

            v = V("yacht", "Sable Crown", "Halcyon Marine", VehicleClass.Yacht);
            v.length = 18.5f; v.width = 5.20f; v.height = 5.40f; v.mass = 24000f; v.enginePower = 620f;
            v.topSpeedKph = 68f; v.price = 1850000; v.spawnWeight = 0.08f; v.buoyancy = 1.22f; v.waterDrag = 3.4f;
            v.seats = 10; v.wheelCount = 0; v.engineBaseHz = 40f;
            v.preferredDistricts = new[] { DistrictType.Marina, DistrictType.Ocean };
            _all.Add(v);

            v = V("tugboat", "Iron Bay Tug", "Brackett Marine", VehicleClass.Boat);
            v.length = 14.0f; v.width = 4.60f; v.height = 4.80f; v.mass = 32000f; v.enginePower = 480f;
            v.topSpeedKph = 42f; v.price = 240000; v.spawnWeight = 0.15f; v.buoyancy = 1.20f; v.waterDrag = 4.2f;
            v.seats = 4; v.wheelCount = 0; v.engineBaseHz = 30f;
            v.preferredDistricts = new[] { DistrictType.Port };
            _all.Add(v);

            v = V("jetski", "Skipjack", "Halcyon Marine", VehicleClass.Boat);
            v.length = 3.10f; v.width = 1.20f; v.height = 1.10f; v.mass = 340f; v.enginePower = 120f;
            v.topSpeedKph = 112f; v.price = 14500; v.spawnWeight = 0.6f; v.buoyancy = 1.4f; v.waterDrag = 1.9f;
            v.seats = 2; v.wheelCount = 0; v.engineBaseHz = 96f; v.paintOptions = LoudPaints;
            v.preferredDistricts = new[] { DistrictType.Beach, DistrictType.Marina };
            _all.Add(v);

            // ---------- Air ----------
            v = V("heli-civ", "Skylark 300", "Aeris", VehicleClass.Helicopter);
            v.length = 12.2f; v.width = 2.60f; v.height = 3.40f; v.mass = 2400f; v.enginePower = 520f;
            v.topSpeedKph = 245f; v.price = 940000; v.spawnWeight = 0.2f; v.liftPower = 22f; v.rotorSpeed = 34f;
            v.seats = 5; v.wheelCount = 0; v.engineBaseHz = 26f;
            v.preferredDistricts = new[] { DistrictType.Airport, DistrictType.Downtown, DistrictType.Wealthy };
            _all.Add(v);

            v = V("heli-police", "SMPD Vigil", "Aeris", VehicleClass.Helicopter);
            v.length = 13.0f; v.width = 2.80f; v.height = 3.60f; v.mass = 2750f; v.enginePower = 600f;
            v.topSpeedKph = 268f; v.spawnWeight = 0f; v.liftPower = 24f; v.rotorSpeed = 36f; v.hasSiren = true;
            v.seats = 4; v.wheelCount = 0; v.maxHealth = 2200f;
            v.paintOptions = new[] { new Color(0.14f,0.16f,0.20f) };
            _all.Add(v);

            v = V("heli-heavy", "Derrick", "Aeris", VehicleClass.Helicopter);
            v.length = 16.5f; v.width = 3.40f; v.height = 4.60f; v.mass = 6800f; v.enginePower = 1100f;
            v.topSpeedKph = 215f; v.price = 2600000; v.spawnWeight = 0.05f; v.liftPower = 20f; v.rotorSpeed = 28f;
            v.seats = 12; v.wheelCount = 0; v.engineBaseHz = 20f; v.maxHealth = 3000f;
            v.preferredDistricts = new[] { DistrictType.Airport, DistrictType.Industrial };
            _all.Add(v);

            v = V("plane-light", "Redwater Kite", "Aeris", VehicleClass.Plane);
            v.length = 8.20f; v.width = 11.4f; v.height = 2.90f; v.mass = 1150f; v.enginePower = 230f;
            v.topSpeedKph = 305f; v.price = 620000; v.spawnWeight = 0.15f; v.liftPower = 16f;
            v.seats = 4; v.wheelCount = 3; v.wheelRadius = 0.26f; v.engineBaseHz = 44f;
            v.preferredDistricts = new[] { DistrictType.Airport };
            _all.Add(v);

            v = V("plane-jet", "Halcyon Meridian Jet", "Aeris", VehicleClass.Plane);
            v.length = 19.5f; v.width = 17.2f; v.height = 5.60f; v.mass = 9800f; v.enginePower = 1800f;
            v.topSpeedKph = 780f; v.price = 8400000; v.spawnWeight = 0.04f; v.liftPower = 19f;
            v.seats = 10; v.wheelCount = 3; v.wheelRadius = 0.38f; v.engineBaseHz = 30f; v.maxHealth = 3600f;
            v.preferredDistricts = new[] { DistrictType.Airport };
            _all.Add(v);

            v = V("plane-cargo", "Redwater Freighter", "Aeris", VehicleClass.Plane);
            v.length = 34.0f; v.width = 32.0f; v.height = 10.5f; v.mass = 48000f; v.enginePower = 4200f;
            v.topSpeedKph = 640f; v.price = 0; v.spawnWeight = 0.02f; v.liftPower = 21f;
            v.seats = 6; v.wheelCount = 3; v.wheelRadius = 0.62f; v.engineBaseHz = 24f; v.maxHealth = 8000f;
            v.preferredDistricts = new[] { DistrictType.Airport };
            _all.Add(v);

            // ---------- Military / faction ----------
            v = V("vanguard-apc", "Vanguard Bulwark", "Vanguard Security", VehicleClass.Military);
            v.length = 6.60f; v.width = 2.60f; v.height = 2.65f; v.mass = 9200f; v.enginePower = 430f;
            v.topSpeedKph = 138f; v.spawnWeight = 0f; v.wheelRadius = 0.55f; v.wheelbase = 4.1f;
            v.track = 2.20f; v.wheelCount = 6; v.seats = 8; v.maxHealth = 6000f; v.crashResistance = 3.2f;
            v.driveType = DriveType.AllWheel; v.cabinLengthRatio = 0.60f; v.cabinHeightRatio = 0.50f;
            v.paintOptions = new[] { new Color(0.22f,0.24f,0.20f) };
            _all.Add(v);

            v = V("cartel-runner", "Serrano Runner", "Steadman", VehicleClass.Muscle);
            v.length = 4.94f; v.width = 1.96f; v.height = 1.40f; v.mass = 1690f; v.enginePower = 355f;
            v.topSpeedKph = 254f; v.spawnWeight = 0.15f; v.grip = 0.98f; v.seats = 4; v.glassTint = 0.72f;
            v.maxHealth = 1250f; v.engineBaseHz = 41f; v.engineHarshness = 0.78f;
            v.paintOptions = new[] { new Color(0.06f,0.06f,0.08f), new Color(0.35f,0.05f,0.10f) };
            v.preferredDistricts = new[] { DistrictType.Marigold, DistrictType.Industrial, DistrictType.Badlands };
            _all.Add(v);

            v = V("syndicate-van", "Iron Bay Hauler", "Corvale", VehicleClass.Van);
            v.length = 5.55f; v.width = 2.05f; v.height = 2.38f; v.mass = 2420f; v.enginePower = 175f;
            v.topSpeedKph = 158f; v.spawnWeight = 0.2f; v.hasCargoBox = true; v.cabinLengthRatio = 0.28f;
            v.cabinHeightRatio = 0.62f; v.seats = 3; v.glassTint = 0.78f; v.maxHealth = 1400f;
            v.paintOptions = new[] { new Color(0.14f,0.16f,0.18f), new Color(0.30f,0.32f,0.34f) };
            v.preferredDistricts = new[] { DistrictType.Port, DistrictType.Industrial };
            _all.Add(v);
        }
    }
}
