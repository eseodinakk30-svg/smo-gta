using System.Collections.Generic;
using UnityEngine;
using SanMonica.Characters;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.World;

namespace SanMonica.Economy
{
    public struct ShopOffer
    {
        public string Id;
        public string Name;
        public string Detail;
        public long Price;
        public ShopOfferKind Kind;
        public object Payload;
        public bool Owned;
    }

    public enum ShopOfferKind { Weapon, Ammo, Armour, Clothing, Haircut, Vehicle, Fuel, Food, Repair, Upgrade, Health, TimeSkip, PayFines }

    /// <summary>
    /// Every store in San Monica actually sells things: guns and ammunition,
    /// clothes, haircuts, fuel, food, repairs, upgrades and cars. Prices scale
    /// with the shop's own multiplier.
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        public ShopInstance CurrentShop { get; private set; }

        public void OpenShop(ShopInstance shop)
        {
            CurrentShop = shop;
        }

        public void CloseShop()
        {
            CurrentShop = null;
        }

        public List<ShopOffer> BuildCatalogue(ShopInstance shop)
        {
            var offers = new List<ShopOffer>();
            if (shop == null) return offers;
            var db = Services.Database;
            var player = Services.Player;
            float multiplier = shop.Definition.priceMultiplier;

            switch (shop.Definition.type)
            {
                case ShopType.GunStore:
                case ShopType.AmmuNation:
                {
                    foreach (var weapon in db.WeaponsForSale())
                    {
                        bool owned = player != null && player.Weapons != null && player.Weapons.HasWeapon(weapon.id);
                        offers.Add(new ShopOffer
                        {
                            Id = weapon.id,
                            Name = weapon.displayName,
                            Detail = weapon.category + "  DMG " + Mathf.RoundToInt(weapon.damage) + "  MAG " + weapon.magazineSize,
                            Price = owned ? Mathf.RoundToInt(weapon.ammoPrice * weapon.magazineSize * multiplier) : Mathf.RoundToInt(weapon.price * multiplier),
                            Kind = owned ? ShopOfferKind.Ammo : ShopOfferKind.Weapon,
                            Payload = weapon,
                            Owned = owned
                        });
                    }
                    offers.Add(new ShopOffer { Id = "armour", Name = "Body Armour", Detail = "Full plate", Price = (long)(2500 * multiplier), Kind = ShopOfferKind.Armour, Payload = 100f });
                    break;
                }

                case ShopType.ClothingStore:
                {
                    int worn = player != null ? player.OutfitIndex : -1;
                    for (int i = 0; i < Wardrobe.Outfits.Length; i++)
                    {
                        var outfit = Wardrobe.Outfits[i];
                        offers.Add(new ShopOffer
                        {
                            Id = "outfit_" + outfit.Id, Name = outfit.Name, Detail = outfit.Detail,
                            Price = (long)(outfit.Price * multiplier), Kind = ShopOfferKind.Clothing,
                            Payload = i, Owned = i == worn
                        });
                    }
                    break;
                }

                case ShopType.Barber:
                {
                    int current = player != null ? player.HairstyleIndex : -1;
                    for (int i = 0; i < Wardrobe.Hairstyles.Length; i++)
                    {
                        var style = Wardrobe.Hairstyles[i];
                        offers.Add(new ShopOffer
                        {
                            Id = "hair_" + style.Id, Name = style.Name, Detail = style.Detail,
                            Price = (long)(style.Price * multiplier), Kind = ShopOfferKind.Haircut,
                            Payload = i, Owned = i == current
                        });
                    }
                    break;
                }

                case ShopType.Mechanic:
                {
                    offers.Add(new ShopOffer { Id = "repair", Name = "Full Repair", Detail = "Bodywork and mechanical", Price = (long)(650 * multiplier), Kind = ShopOfferKind.Repair });
                    offers.Add(new ShopOffer { Id = "upgrade_engine", Name = "Engine Tune", Detail = "+18% power", Price = (long)(4200 * multiplier), Kind = ShopOfferKind.Upgrade, Payload = "engine" });
                    offers.Add(new ShopOffer { Id = "upgrade_brakes", Name = "Brake Kit", Detail = "+25% braking", Price = (long)(2600 * multiplier), Kind = ShopOfferKind.Upgrade, Payload = "brakes" });
                    offers.Add(new ShopOffer { Id = "upgrade_grip", Name = "Sport Tyres", Detail = "+15% grip", Price = (long)(3100 * multiplier), Kind = ShopOfferKind.Upgrade, Payload = "grip" });
                    offers.Add(new ShopOffer { Id = "upgrade_armour", Name = "Reinforced Body", Detail = "+60% durability", Price = (long)(5400 * multiplier), Kind = ShopOfferKind.Upgrade, Payload = "armour" });
                    offers.Add(new ShopOffer { Id = "respray", Name = "Respray", Detail = "New colour, cools police interest", Price = (long)(900 * multiplier), Kind = ShopOfferKind.Upgrade, Payload = "respray" });
                    break;
                }

                case ShopType.Dealership:
                case ShopType.Marine:
                case ShopType.Aviation:
                {
                    DealerStock stock;
                    if (shop.Definition.type == ShopType.Marine) stock = DealerStock.Marine;
                    else if (shop.Definition.type == ShopType.Aviation) stock = DealerStock.Aviation;
                    else stock = shop.Definition.id == "luxury-dealer" ? DealerStock.Luxury : DealerStock.Everyday;

                    bool garageFull = Services.Garage != null && Services.Garage.IsFull;
                    foreach (var vehicle in db.VehiclesForSale(stock))
                        offers.Add(new ShopOffer
                        {
                            Id = vehicle.id, Name = vehicle.displayName,
                            Detail = vehicle.manufacturer + "  " + Mathf.RoundToInt(vehicle.topSpeedKph) + " km/h",
                            Price = (long)(vehicle.price * multiplier), Kind = ShopOfferKind.Vehicle,
                            Payload = vehicle, Owned = garageFull
                        });
                    break;
                }

                case ShopType.GasStation:
                    offers.Add(new ShopOffer { Id = "fuel", Name = "Full Tank", Detail = "Refuel current vehicle", Price = (long)(90 * multiplier), Kind = ShopOfferKind.Fuel });
                    offers.Add(new ShopOffer { Id = "snack", Name = "Snack", Detail = "+25 health", Price = (long)(12 * multiplier), Kind = ShopOfferKind.Food, Payload = 25f });
                    break;

                case ShopType.Restaurant:
                    offers.Add(new ShopOffer { Id = "meal", Name = "Full Meal", Detail = "+70 health", Price = (long)(28 * multiplier), Kind = ShopOfferKind.Food, Payload = 70f });
                    offers.Add(new ShopOffer { Id = "coffee", Name = "Coffee", Detail = "+15 health", Price = (long)(6 * multiplier), Kind = ShopOfferKind.Food, Payload = 15f });
                    break;

                case ShopType.Pharmacy:
                case ShopType.Hospital:
                    offers.Add(new ShopOffer { Id = "medkit", Name = "Medical Kit", Detail = "Full health", Price = (long)(320 * multiplier), Kind = ShopOfferKind.Health });
                    offers.Add(new ShopOffer { Id = "painkillers", Name = "Painkillers", Detail = "+45 health", Price = (long)(60 * multiplier), Kind = ShopOfferKind.Food, Payload = 45f });
                    break;

                case ShopType.Nightclub:
                    offers.Add(new ShopOffer { Id = "drink", Name = "Drink", Detail = "+20 health", Price = (long)(14 * multiplier), Kind = ShopOfferKind.Food, Payload = 20f });
                    offers.Add(new ShopOffer { Id = "bottle", Name = "Bottle Service", Detail = "+80 health, and a table", Price = (long)(950 * multiplier), Kind = ShopOfferKind.Food, Payload = 80f });
                    offers.Add(new ShopOffer { Id = "nightout", Name = "Stay Until Closing", Detail = "Drink through to the morning", Price = (long)(320 * multiplier), Kind = ShopOfferKind.TimeSkip, Payload = 6f });
                    break;

                case ShopType.Hardware:
                {
                    foreach (var id in new[] { "wrench", "bat", "machete", "knife" })
                    {
                        var tool = db.Weapon(id);
                        if (tool == null) continue;
                        bool held = player != null && player.Weapons != null && player.Weapons.HasWeapon(tool.id);
                        offers.Add(new ShopOffer
                        {
                            Id = tool.id, Name = tool.displayName, Detail = "Tool  DMG " + Mathf.RoundToInt(tool.damage),
                            Price = (long)(tool.price * multiplier), Kind = ShopOfferKind.Weapon, Payload = tool, Owned = held
                        });
                    }
                    offers.Add(new ShopOffer { Id = "toolbox", Name = "Toolbox Repair", Detail = "Patch up the vehicle outside", Price = (long)(420 * multiplier), Kind = ShopOfferKind.Repair });
                    break;
                }

                case ShopType.PoliceStation:
                {
                    int level = Services.Wanted != null ? Services.Wanted.Level : 0;
                    long fine = 1500L * Mathf.Max(1, level) * Mathf.Max(1, level);
                    offers.Add(new ShopOffer
                    {
                        Id = "fines", Name = "Settle Outstanding Fines",
                        Detail = level > 0 ? "Clears " + level + " star" + (level == 1 ? "" : "s") : "Nothing outstanding",
                        Price = (long)(fine * multiplier), Kind = ShopOfferKind.PayFines, Owned = level == 0
                    });
                    break;
                }

                default:
                    offers.Add(new ShopOffer { Id = "snack", Name = "Snack", Detail = "+20 health", Price = (long)(10 * multiplier), Kind = ShopOfferKind.Food, Payload = 20f });
                    offers.Add(new ShopOffer { Id = "drink", Name = "Cold Drink", Detail = "+10 health", Price = (long)(5 * multiplier), Kind = ShopOfferKind.Food, Payload = 10f });
                    offers.Add(new ShopOffer { Id = "armour_light", Name = "Vest", Detail = "+50 armour", Price = (long)(900 * multiplier), Kind = ShopOfferKind.Armour, Payload = 50f });
                    break;
            }

            return offers;
        }

        /// <summary>
        /// Whether this purchase can actually happen. Money is taken before the
        /// goods are handed over, so anything that might quietly do nothing -
        /// a full ammunition pouch, a fuel pump with no car at it, an upgrade
        /// already at its highest grade - has to say no here instead.
        /// </summary>
        private bool CanDeliver(in ShopOffer offer, SanMonica.Players.PlayerController player)
        {
            switch (offer.Kind)
            {
                case ShopOfferKind.Ammo:
                    if (offer.Payload is WeaponDefinition ammoFor && player.Weapons != null
                        && player.Weapons.IsAmmoFull(ammoFor.ammoType))
                    {
                        GameEvents.Notify("You are already carrying all the " + ammoFor.ammoType + " ammunition you can", 2.6f);
                        return false;
                    }
                    return true;

                case ShopOfferKind.Fuel:
                {
                    var vehicle = player.ServiceableVehicle();
                    if (vehicle == null) { GameEvents.Notify("Bring a vehicle to the pump", 2.4f); return false; }
                    if (vehicle.Definition != null && vehicle.Fuel >= vehicle.Definition.fuelCapacity - 0.5f)
                    { GameEvents.Notify("That tank is already full", 2.2f); return false; }
                    return true;
                }

                case ShopOfferKind.Repair:
                {
                    var vehicle = player.ServiceableVehicle();
                    if (vehicle == null) { GameEvents.Notify("Bring a vehicle to the ramp", 2.4f); return false; }
                    if (vehicle.Definition != null && vehicle.Health >= vehicle.Definition.maxHealth - 1f)
                    { GameEvents.Notify("Nothing on it needs fixing", 2.2f); return false; }
                    return true;
                }

                case ShopOfferKind.Upgrade:
                {
                    var vehicle = player.ServiceableVehicle();
                    if (vehicle == null) { GameEvents.Notify("Bring a vehicle to the ramp", 2.4f); return false; }
                    if (Services.Garage != null && !Services.Garage.CanUpgrade(vehicle, offer.Payload as string))
                    { GameEvents.Notify("That is already fitted at the highest grade", 2.4f); return false; }
                    return true;
                }

                case ShopOfferKind.Health:
                case ShopOfferKind.Food:
                    if (player.Health != null && player.Health.Health >= player.Health.MaxHealth - 0.5f)
                    { GameEvents.Notify("You are not hurt", 2f); return false; }
                    return true;

                case ShopOfferKind.Armour:
                {
                    float have = player.Health != null ? player.Health.Armour : 0f;
                    float max = player.Health != null ? player.Health.MaxArmour : 0f;
                    if (have >= max - 0.5f) { GameEvents.Notify("Your armour is already at its limit", 2.4f); return false; }
                    return true;
                }

                case ShopOfferKind.Clothing:
                    if (offer.Payload is int outfit && player.OutfitIndex == outfit)
                    { GameEvents.Notify("You are wearing that already", 2.2f); return false; }
                    return true;

                case ShopOfferKind.Haircut:
                    if (offer.Payload is int hair && player.HairstyleIndex == hair)
                    { GameEvents.Notify("That is the cut you have", 2.2f); return false; }
                    return true;

                case ShopOfferKind.Weapon:
                    if (offer.Payload is WeaponDefinition weapon && player.Weapons != null
                        && player.Weapons.HasWeapon(weapon.id))
                    { GameEvents.Notify("You already own that", 2.2f); return false; }
                    return true;

                case ShopOfferKind.Vehicle:
                    if (offer.Payload is VehicleDefinition bought && Services.Garage != null
                        && Services.Garage.IsFull)
                    { GameEvents.Notify("Your garages are full - sell something first", 3f); return false; }
                    return true;

                case ShopOfferKind.PayFines:
                    if (Services.Wanted == null || Services.Wanted.Level <= 0)
                    { GameEvents.Notify("You have nothing outstanding", 2.2f); return false; }
                    return true;

                default:
                    return true;
            }
        }

        public bool Purchase(in ShopOffer offer)
        {
            var economy = Services.Economy;
            var player = Services.Player;
            if (economy == null || player == null) return false;
            // Never take money for something that cannot be delivered.
            if (!CanDeliver(in offer, player)) return false;

            if (!economy.TrySpend(offer.Price, offer.Name)) return false;

            switch (offer.Kind)
            {
                case ShopOfferKind.Weapon:
                {
                    var weapon = offer.Payload as WeaponDefinition;
                    if (weapon != null)
                    {
                        player.Weapons.GiveWeapon(weapon, weapon.magazineSize * 3, true);
                        GameEvents.Notify("Bought " + weapon.displayName, 2.5f);
                    }
                    break;
                }
                case ShopOfferKind.Ammo:
                {
                    var weapon = offer.Payload as WeaponDefinition;
                    if (weapon != null)
                    {
                        player.Weapons.AddAmmo(weapon.ammoType, weapon.magazineSize * 2);
                        GameEvents.Notify("Ammunition purchased", 2f);
                    }
                    break;
                }
                case ShopOfferKind.Armour:
                {
                    float amount = offer.Payload is float f ? f : 100f;
                    player.Health.AddArmour(amount);
                    GameEvents.Notify("Armour equipped", 2f);
                    break;
                }
                case ShopOfferKind.Health:
                    player.Health.Heal(player.Health.MaxHealth);
                    GameEvents.Notify("Fully healed", 2f);
                    break;
                case ShopOfferKind.Food:
                {
                    float amount = offer.Payload is float f2 ? f2 : 20f;
                    player.Health.Heal(amount);
                    break;
                }
                case ShopOfferKind.Clothing:
                {
                    int index = offer.Payload is int i ? i : 0;
                    player.SetOutfit(index);
                    Services.Save?.SetOutfit(index);
                    GameEvents.Notify("Now wearing " + Wardrobe.Outfit(index).Name, 2.4f);
                    break;
                }
                case ShopOfferKind.Haircut:
                {
                    int index = offer.Payload is int i2 ? i2 : 0;
                    player.SetHairstyle(index);
                    Services.Save?.SetHairstyle(index);
                    GameEvents.Notify(Wardrobe.Hair(index).Name, 2.4f);
                    break;
                }
                case ShopOfferKind.Fuel:
                {
                    // You buy fuel standing at a counter, not sitting in the car:
                    // the old code needed CurrentVehicle and so never refuelled
                    // anything, having already taken the money.
                    var vehicle = player.ServiceableVehicle();
                    if (vehicle != null && vehicle.Definition != null)
                    {
                        vehicle.Fuel = vehicle.Definition.fuelCapacity;
                        GameEvents.Notify("Tank full", 2f);
                    }
                    break;
                }
                case ShopOfferKind.Repair:
                {
                    var vehicle = player.ServiceableVehicle();
                    if (vehicle != null && vehicle.Definition != null)
                    {
                        vehicle.Health = vehicle.Definition.maxHealth;
                        GameEvents.Notify("Vehicle repaired", 2f);
                    }
                    break;
                }
                case ShopOfferKind.Upgrade:
                    Services.Garage?.ApplyUpgrade(player.ServiceableVehicle(), offer.Payload as string);
                    break;
                case ShopOfferKind.TimeSkip:
                {
                    float hours = offer.Payload is float h ? h : 4f;
                    Services.Clock?.SkipHours(hours);
                    player.Health?.Heal(40f);
                    GameEvents.Notify("You leave as the sun comes up", 3f);
                    break;
                }
                case ShopOfferKind.PayFines:
                    Services.Wanted?.ResetWanted();
                    GameEvents.Notify("Fines settled - you are clear", 3f);
                    break;
                case ShopOfferKind.Vehicle:
                {
                    var vehicle = offer.Payload as VehicleDefinition;
                    if (vehicle != null && Services.Garage != null)
                    {
                        Services.Garage.AddOwnedVehicle(vehicle.id);
                        GameEvents.Notify("Bought " + vehicle.displayName + " - delivered to your garage", 4f);
                    }
                    break;
                }
            }
            return true;
        }
    }
}
