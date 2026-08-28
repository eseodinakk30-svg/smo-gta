using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Data
{
    /// <summary>
    /// Single point of access to every piece of static game data. Built at boot
    /// from the code-defined catalogues so the project needs no binary assets,
    /// but any entry can be replaced by an authored ScriptableObject asset.
    /// </summary>
    public class GameDatabase : ScriptableObject
    {
        public List<VehicleDefinition> vehicles = new List<VehicleDefinition>();
        public List<WeaponDefinition> weapons = new List<WeaponDefinition>();
        public List<PedArchetype> peds = new List<PedArchetype>();
        public List<ShopDefinition> shops = new List<ShopDefinition>();
        public List<RadioStationDefinition> radioStations = new List<RadioStationDefinition>();

        private Dictionary<string, VehicleDefinition> _vehicleById;
        private Dictionary<string, WeaponDefinition> _weaponById;
        private Dictionary<string, PedArchetype> _pedById;
        private Dictionary<string, ShopDefinition> _shopById;

        public static GameDatabase Build()
        {
            var db = CreateInstance<GameDatabase>();
            db.name = "GameDatabase";
            db.vehicles.AddRange(VehicleCatalogData.All);
            db.weapons.AddRange(WeaponCatalogData.All);
            db.peds.AddRange(PedCatalogData.All);
            db.shops.AddRange(ShopCatalogData.All);
            db.radioStations.AddRange(RadioCatalogData.All);
            db.Index();
            return db;
        }

        public void Index()
        {
            _vehicleById = new Dictionary<string, VehicleDefinition>(vehicles.Count);
            foreach (var v in vehicles) if (v != null) _vehicleById[v.id] = v;
            _weaponById = new Dictionary<string, WeaponDefinition>(weapons.Count);
            foreach (var w in weapons) if (w != null) _weaponById[w.id] = w;
            _pedById = new Dictionary<string, PedArchetype>(peds.Count);
            foreach (var p in peds) if (p != null) _pedById[p.id] = p;
            _shopById = new Dictionary<string, ShopDefinition>(shops.Count);
            foreach (var s in shops) if (s != null) _shopById[s.id] = s;
        }

        public VehicleDefinition Vehicle(string id)
        {
            if (_vehicleById == null) Index();
            return _vehicleById != null && _vehicleById.TryGetValue(id, out var v) ? v : null;
        }

        public WeaponDefinition Weapon(string id)
        {
            if (_weaponById == null) Index();
            return _weaponById != null && _weaponById.TryGetValue(id, out var w) ? w : null;
        }

        public PedArchetype Ped(string id)
        {
            if (_pedById == null) Index();
            return _pedById != null && _pedById.TryGetValue(id, out var p) ? p : null;
        }

        public ShopDefinition Shop(string id)
        {
            if (_shopById == null) Index();
            return _shopById != null && _shopById.TryGetValue(id, out var s) ? s : null;
        }

        /// <summary>Weighted random vehicle appropriate for a district and traffic role.</summary>
        public VehicleDefinition PickTrafficVehicle(ref SanMonica.Core.Rng rng, DistrictType district)
        {
            float total = 0f;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v.spawnWeight <= 0f || !v.IsGroundCar) continue;
                total += Weight(v, district);
            }
            if (total <= 0f) return Vehicle("meridian");
            float pick = rng.Value * total;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v.spawnWeight <= 0f || !v.IsGroundCar) continue;
                pick -= Weight(v, district);
                if (pick <= 0f) return v;
            }
            return Vehicle("meridian");
        }

        public VehicleDefinition PickParkedVehicle(ref SanMonica.Core.Rng rng, DistrictType district)
            => PickTrafficVehicle(ref rng, district);

        public VehicleDefinition PickBoat(ref SanMonica.Core.Rng rng)
        {
            var list = new List<VehicleDefinition>();
            foreach (var v in vehicles) if (v.IsWatercraft && v.spawnWeight > 0f) list.Add(v);
            return list.Count > 0 ? list[rng.Range(0, list.Count)] : null;
        }

        public VehicleDefinition PickAircraft(ref SanMonica.Core.Rng rng)
        {
            var list = new List<VehicleDefinition>();
            foreach (var v in vehicles) if (v.IsAircraft && v.spawnWeight > 0f) list.Add(v);
            return list.Count > 0 ? list[rng.Range(0, list.Count)] : null;
        }

        private static float Weight(VehicleDefinition v, DistrictType d)
        {
            float w = v.spawnWeight;
            if (v.preferredDistricts != null && v.preferredDistricts.Length > 0)
            {
                bool match = false;
                for (int i = 0; i < v.preferredDistricts.Length; i++)
                    if (v.preferredDistricts[i] == d) { match = true; break; }
                w *= match ? 3.5f : 0.25f;
            }
            return w;
        }

        /// <summary>Weighted random pedestrian archetype for a district and hour of day.</summary>
        public PedArchetype PickPed(ref SanMonica.Core.Rng rng, DistrictType district, int hour)
        {
            float total = 0f;
            for (int i = 0; i < peds.Count; i++) total += PedWeight(peds[i], district, hour);
            if (total <= 0f) return Ped("citizen");
            float pick = rng.Value * total;
            for (int i = 0; i < peds.Count; i++)
            {
                pick -= PedWeight(peds[i], district, hour);
                if (pick <= 0f) return peds[i];
            }
            return Ped("citizen");
        }

        private static float PedWeight(PedArchetype p, DistrictType d, int hour)
        {
            if (p == null || p.spawnWeight <= 0f) return 0f;
            if (!p.IsActiveAt(hour)) return 0f;
            float w = p.spawnWeight;
            if (p.preferredDistricts != null && p.preferredDistricts.Length > 0)
            {
                bool match = false;
                for (int i = 0; i < p.preferredDistricts.Length; i++)
                    if (p.preferredDistricts[i] == d) { match = true; break; }
                w *= match ? 4f : 0.15f;
            }
            return w;
        }

        public List<WeaponDefinition> WeaponsForSale()
        {
            var list = new List<WeaponDefinition>();
            foreach (var w in weapons)
                if (w.price > 0 && w.category != WeaponCategory.Unarmed) list.Add(w);
            return list;
        }

        public List<VehicleDefinition> VehiclesForSale(bool luxuryOnly)
        {
            var list = new List<VehicleDefinition>();
            foreach (var v in vehicles)
            {
                if (v.price <= 0 || v.IsEmergency) continue;
                if (luxuryOnly && v.price < 90000) continue;
                if (!luxuryOnly && v.price >= 200000) continue;
                list.Add(v);
            }
            return list;
        }
    }
}
