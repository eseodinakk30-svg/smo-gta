using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Vehicles;

namespace SanMonica.Economy
{
    /// <summary>
    /// The player's collection: vehicles bought or claimed, their upgrades, and
    /// the ability to have one delivered to the nearest owned property.
    /// </summary>
    public class GarageSystem : MonoBehaviour
    {
        [System.Serializable]
        public class OwnedVehicle
        {
            public string DefinitionId;
            public int Engine;
            public int Brakes;
            public int Grip;
            public int Armour;
            public float PaintR = 1f, PaintG = 1f, PaintB = 1f;

            public Color Paint => new Color(PaintR, PaintG, PaintB);
        }

        public readonly List<OwnedVehicle> Collection = new List<OwnedVehicle>(16);
        private readonly Dictionary<string, VehicleDefinition> _modifiedDefinitions = new Dictionary<string, VehicleDefinition>();

        public int Capacity = 20;
        public Vehicle DeliveredVehicle { get; private set; }

        public bool AddOwnedVehicle(string definitionId, Color? paint = null)
        {
            if (string.IsNullOrEmpty(definitionId)) return false;
            if (Collection.Count >= Capacity)
            {
                GameEvents.Notify("Your garages are full", 3f);
                return false;
            }
            var entry = new OwnedVehicle { DefinitionId = definitionId };
            if (paint.HasValue)
            {
                entry.PaintR = paint.Value.r; entry.PaintG = paint.Value.g; entry.PaintB = paint.Value.b;
            }
            else
            {
                var def = Services.Database?.Vehicle(definitionId);
                Color c = def != null && Services.Vehicles != null ? Services.Vehicles.PickPaint(def) : Color.grey;
                entry.PaintR = c.r; entry.PaintG = c.g; entry.PaintB = c.b;
            }
            Collection.Add(entry);
            return true;
        }

        /// <summary>Claims the vehicle the player is currently driving.</summary>
        public bool ClaimCurrentVehicle()
        {
            var player = Services.Player;
            if (player == null || player.CurrentVehicle == null) return false;
            var vehicle = player.CurrentVehicle;
            if (vehicle.Definition == null) return false;
            if (!AddOwnedVehicle(vehicle.Definition.id, vehicle.Paint)) return false;
            vehicle.IsPlayerOwned = true;
            vehicle.HasOwner = false;
            GameEvents.Notify(vehicle.DisplayName + " added to your collection", 3f);
            return true;
        }

        public void ApplyUpgrade(Vehicle vehicle, string upgrade)
        {
            if (vehicle == null || string.IsNullOrEmpty(upgrade))
            {
                GameEvents.Notify("Drive a vehicle in first", 2.5f);
                return;
            }
            var entry = FindEntry(vehicle);
            if (entry == null)
            {
                if (!AddOwnedVehicle(vehicle.Definition.id, vehicle.Paint)) return;
                entry = Collection[Collection.Count - 1];
                vehicle.IsPlayerOwned = true;
            }

            switch (upgrade)
            {
                case "engine": entry.Engine = Mathf.Min(3, entry.Engine + 1); break;
                case "brakes": entry.Brakes = Mathf.Min(3, entry.Brakes + 1); break;
                case "grip": entry.Grip = Mathf.Min(3, entry.Grip + 1); break;
                case "armour": entry.Armour = Mathf.Min(3, entry.Armour + 1); break;
                case "respray":
                {
                    var paint = Services.Vehicles != null ? Services.Vehicles.PickPaint(vehicle.Definition) : Color.grey;
                    entry.PaintR = paint.r; entry.PaintG = paint.g; entry.PaintB = paint.b;
                    Services.Wanted?.AddHeat(-1f, vehicle.transform.position);
                    GameEvents.Notify("Resprayed", 2.5f);
                    return;
                }
            }

            ApplyToLiveVehicle(vehicle, entry);
            GameEvents.Notify("Upgrade installed", 2.5f);
        }

        private void ApplyToLiveVehicle(Vehicle vehicle, OwnedVehicle entry)
        {
            // Upgrades act on a private clone of the definition so the shared
            // catalogue entry is never mutated.
            var modified = GetModifiedDefinition(vehicle.Definition, entry);
            if (modified == null) return;
            vehicle.Construct(modified, entry.Paint, vehicle.GetInstanceID());
            vehicle.IsPlayerOwned = true;
        }

        public VehicleDefinition GetModifiedDefinition(VehicleDefinition source, OwnedVehicle entry)
        {
            if (source == null || entry == null) return null;
            if (entry.Engine == 0 && entry.Brakes == 0 && entry.Grip == 0 && entry.Armour == 0) return source;

            string key = source.id + "_m" + entry.Engine + entry.Brakes + entry.Grip + entry.Armour;
            if (_modifiedDefinitions.TryGetValue(key, out var cached)) return cached;

            var clone = Instantiate(source);
            clone.name = key;
            clone.id = key;
            clone.enginePower *= 1f + entry.Engine * 0.18f;
            clone.topSpeedKph *= 1f + entry.Engine * 0.07f;
            clone.acceleration *= 1f + entry.Engine * 0.10f;
            clone.brakeTorque *= 1f + entry.Brakes * 0.25f;
            clone.handbrakeTorque *= 1f + entry.Brakes * 0.18f;
            clone.grip *= 1f + entry.Grip * 0.15f;
            clone.maxHealth *= 1f + entry.Armour * 0.60f;
            clone.crashResistance *= 1f + entry.Armour * 0.45f;
            _modifiedDefinitions[key] = clone;
            return clone;
        }

        private OwnedVehicle FindEntry(Vehicle vehicle)
        {
            if (vehicle == null || vehicle.Definition == null) return null;
            string baseId = vehicle.Definition.id;
            int cut = baseId.IndexOf("_m", System.StringComparison.Ordinal);
            if (cut > 0) baseId = baseId.Substring(0, cut);
            foreach (var entry in Collection) if (entry.DefinitionId == baseId) return entry;
            return null;
        }

        /// <summary>Delivers a stored vehicle to the nearest owned property or to the kerb.</summary>
        public Vehicle Deliver(OwnedVehicle entry, Vector3 near)
        {
            if (entry == null || Services.Vehicles == null || Services.Database == null) return null;
            var source = Services.Database.Vehicle(entry.DefinitionId);
            if (source == null) return null;
            var definition = GetModifiedDefinition(source, entry);

            Vector3 spot = near;
            var property = Services.Property?.FindOwnedNearest(near);
            if (property != null && Vector3.Distance(property.Definition.spawnPoint, near) < 220f)
                spot = property.Definition.spawnPoint;
            else if (Services.Roads != null)
            {
                int segment = Services.Roads.NearestSegment(new Vector2(near.x, near.z), 120f);
                if (segment >= 0) spot = Services.Roads.LanePoint(segment, 0, true, 0.5f);
            }

            if (Services.Map != null) spot.y = Services.Map.SampleHeight(spot.x, spot.z) + definition.wheelRadius + 0.15f;

            DeliveredVehicle = Services.Vehicles.Spawn(definition, spot, Quaternion.Euler(0f, Random.value * 360f, 0f), entry.Paint);
            if (DeliveredVehicle != null)
            {
                DeliveredVehicle.IsPlayerOwned = true;
                DeliveredVehicle.HasOwner = false;
                GameEvents.Notify(source.displayName + " delivered", 3f);
            }
            return DeliveredVehicle;
        }

        public List<OwnedVehicle> CaptureState() => new List<OwnedVehicle>(Collection);

        public void RestoreState(List<OwnedVehicle> state)
        {
            Collection.Clear();
            if (state != null) Collection.AddRange(state);
        }
    }
}
