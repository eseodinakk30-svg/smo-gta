using UnityEngine;

namespace SanMonica.Data
{
    public enum VehicleClass
    {
        Compact, Sedan, Coupe, Sports, Super, Muscle, SUV, Pickup, Van, Truck,
        Bus, Taxi, Police, Ambulance, FireTruck, Motorcycle, Scooter, Boat,
        Yacht, Helicopter, Plane, Military, Utility
    }

    public enum DriveType { FrontWheel, RearWheel, AllWheel }

    /// <summary>
    /// Data driven description of a vehicle. The mesh, colliders, physics and
    /// audio for every car in the game are generated from these numbers, so a
    /// new vehicle is one entry in the catalogue - no art pipeline required.
    /// </summary>
    [CreateAssetMenu(menuName = "San Monica/Vehicle", fileName = "Vehicle")]
    public class VehicleDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "vehicle";
        public string displayName = "Vehicle";
        public string manufacturer = "Vireo Motors";
        public VehicleClass vehicleClass = VehicleClass.Sedan;
        public int price = 15000;
        public float spawnWeight = 1f;
        [Tooltip("Districts where this vehicle spawns naturally. Empty = anywhere.")]
        public DistrictType[] preferredDistricts;

        [Header("Body (metres)")]
        public float length = 4.6f;
        public float width = 1.85f;
        public float height = 1.42f;
        public float cabinLengthRatio = 0.46f;
        public float cabinHeightRatio = 0.38f;
        public float noseSlope = 0.16f;
        public float roofTaper = 0.78f;
        public float rideHeight = 0.28f;

        [Header("Wheels")]
        public float wheelRadius = 0.34f;
        public float wheelWidth = 0.24f;
        public float wheelbase = 2.75f;
        public float track = 1.58f;
        public int wheelCount = 4;

        [Header("Powertrain")]
        public float mass = 1450f;
        public float enginePower = 210f;        // kW at peak
        public float topSpeedKph = 195f;
        public float acceleration = 1f;         // tuning multiplier
        public float brakeTorque = 3200f;
        public float handbrakeTorque = 5200f;
        public DriveType driveType = DriveType.RearWheel;
        public int gearCount = 6;
        public float grip = 1f;
        public float steerAngle = 32f;
        public float downforce = 40f;
        public float suspensionDistance = 0.22f;
        public float suspensionSpring = 32000f;
        public float suspensionDamper = 4200f;
        public float centerOfMassHeight = -0.35f;

        [Header("Flight / marine (used by air and water vehicles)")]
        public float liftPower = 0f;
        public float rotorSpeed = 0f;
        public float buoyancy = 0f;
        public float waterDrag = 2.4f;

        [Header("Condition")]
        public float maxHealth = 1000f;
        public float crashResistance = 1f;
        public float fuelCapacity = 55f;

        [Header("Seats & doors")]
        public int seats = 4;
        public bool convertible = false;

        [Header("Look")]
        public Color[] paintOptions;
        public bool hasSiren = false;
        public bool hasRoofRack = false;
        public bool hasBed = false;          // pickups
        public bool hasCargoBox = false;     // vans / trucks
        public float glassTint = 0.35f;

        [Header("Audio")]
        public float engineBaseHz = 62f;
        public float engineHarshness = 0.45f;
        public float turboWhistle = 0f;

        public bool IsAircraft => vehicleClass == VehicleClass.Helicopter || vehicleClass == VehicleClass.Plane;
        public bool IsWatercraft => vehicleClass == VehicleClass.Boat || vehicleClass == VehicleClass.Yacht;
        public bool IsBike => vehicleClass == VehicleClass.Motorcycle || vehicleClass == VehicleClass.Scooter;
        public bool IsEmergency => vehicleClass == VehicleClass.Police || vehicleClass == VehicleClass.Ambulance || vehicleClass == VehicleClass.FireTruck;
        public bool IsGroundCar => !IsAircraft && !IsWatercraft && !IsBike;

        public float TopSpeedMs => topSpeedKph / 3.6f;
    }
}
