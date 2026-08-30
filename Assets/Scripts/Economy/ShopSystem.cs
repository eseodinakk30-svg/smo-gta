using System.Collections.Generic;
using UnityEngine;
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

    public enum ShopOfferKind { Weapon, Ammo, Armour, Clothing, Haircut, Vehicle, Fuel, Food, Repair, Upgrade, Health }

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
                    offers.Add(new ShopOffer { Id = "armour", Name = "Body Armour", Detail = "Full plate", Price = (long)(2500 * multiplier), Kind = ShopOfferKind.Armour });
                    break;
                }

                case ShopType.ClothingStore:
                {
                    string[] outfits = { "Street", "Workwear", "Sharp Suit", "Track Set", "Coastal Linen", "Night Out", "Utility Jacket", "Formal Black" };
                    for (int i = 0; i < outfits.Length; i++)
                        offers.Add(new ShopOffer
                        {
                            Id = "outfit_" + i, Name = outfits[i], Detail = "Change of clothes",
                            Price = (long)((180 + i * 220) * multiplier), Kind = ShopOfferKind.Clothing, Payload = i
                        });
                    break;
                }

                case ShopType.Barber:
                    for (int i = 0; i < 6; i++)
                        offers.Add(new ShopOffer { Id = "hair_" + i, Name = "Style " + (i + 1), Detail = "Cut and colour", Price = (long)(120 * multiplier), Kind = ShopOfferKind.Haircut, Payload = i });
                    break;

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
                    bool luxury = shop.Definition.id == "luxury-dealer";
                    foreach (var vehicle in db.VehiclesForSale(luxury))
                    {
                        if (shop.Definition.type == ShopType.Marine && !vehicle.IsWatercraft) continue;
                        if (shop.Definition.type == ShopType.Aviation && !vehicle.IsAircraft) continue;
                        if (shop.Definition.type == ShopType.Dealership && (vehicle.IsWatercraft || vehicle.IsAircraft)) continue;
                        offers.Add(new ShopOffer
                        {
                            Id = vehicle.id, Name = vehicle.displayName,
                            Detail = vehicle.manufacturer + "  " + Mathf.RoundToInt(vehicle.topSpeedKph) + " km/h",
                            Price = (long)(vehicle.price * multiplier), Kind = ShopOfferKind.Vehicle, Payload = vehicle
                        });
                    }
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

                default:
                    offers.Add(new ShopOffer { Id = "snack", Name = "Snack", Detail = "+20 health", Price = (long)(10 * multiplier), Kind = ShopOfferKind.Food, Payload = 20f });
                    offers.Add(new ShopOffer { Id = "drink", Name = "Cold Drink", Detail = "+10 health", Price = (long)(5 * multiplier), Kind = ShopOfferKind.Food, Payload = 10f });
                    offers.Add(new ShopOffer { Id = "armour_light", Name = "Vest", Detail = "+50 armour", Price = (long)(900 * multiplier), Kind = ShopOfferKind.Armour, Payload = 50f });
                    break;
            }

            return offers;
        }

        public bool Purchase(in ShopOffer offer)
        {
            var economy = Services.Economy;
            var player = Services.Player;
            if (economy == null || player == null) return false;
            // Never take money for something that cannot be delivered.
            if (offer.Kind == ShopOfferKind.Ammo && offer.Payload is WeaponDefinition ammoFor
                && player.Weapons != null && player.Weapons.IsAmmoFull(ammoFor.ammoType))
            {
                GameEvents.Notify("You are already carrying all the " + ammoFor.ammoType + " ammunition you can", 2.6f);
                return false;
            }

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
                    GameEvents.Notify("Outfit changed", 2f);
                    Services.Save?.SetOutfit(offer.Payload is int i ? i : 0);
                    break;
                case ShopOfferKind.Haircut:
                    GameEvents.Notify("New look", 2f);
                    break;
                case ShopOfferKind.Fuel:
                    if (player.CurrentVehicle != null)
                    {
                        player.CurrentVehicle.Fuel = player.CurrentVehicle.Definition.fuelCapacity;
                        GameEvents.Notify("Tank full", 2f);
                    }
                    break;
                case ShopOfferKind.Repair:
                    if (player.CurrentVehicle != null)
                    {
                        player.CurrentVehicle.Health = player.CurrentVehicle.Definition.maxHealth;
                        GameEvents.Notify("Vehicle repaired", 2f);
                    }
                    break;
                case ShopOfferKind.Upgrade:
                    Services.Garage?.ApplyUpgrade(player.CurrentVehicle, offer.Payload as string);
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
