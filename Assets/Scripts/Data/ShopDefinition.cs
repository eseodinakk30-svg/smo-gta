using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Data
{
    public enum ShopType
    {
        GunStore, ClothingStore, Barber, Mechanic, Dealership, GasStation,
        Restaurant, ConvenienceStore, Pharmacy, Hospital, PoliceStation,
        AmmuNation, Nightclub, Hardware, Marine, Aviation
    }

    /// <summary>Which part of the vehicle catalogue a showroom carries.</summary>
    public enum DealerStock { Everyday, Luxury, Marine, Aviation }

    [System.Serializable]
    public class ShopDefinition
    {
        public string id = "shop";
        public string displayName = "Store";
        public ShopType type = ShopType.ConvenienceStore;
        public float priceMultiplier = 1f;
        public bool hasInterior = true;
        public int openHour = 8;
        public int closeHour = 22;
        public Color signColor = new Color(0.9f, 0.6f, 0.2f);
        public DistrictType[] districts;

        public bool IsOpen(int hour)
        {
            if (openHour == closeHour) return true;
            if (openHour < closeHour) return hour >= openHour && hour < closeHour;
            return hour >= openHour || hour < closeHour;
        }
    }

    public static class ShopCatalogData
    {
        private static List<ShopDefinition> _all;
        public static List<ShopDefinition> All { get { if (_all == null) Build(); return _all; } }

        private static void Build()
        {
            _all = new List<ShopDefinition>
            {
                new ShopDefinition { id="ammunation", displayName="Coastline Arms", type=ShopType.GunStore,
                    openHour=9, closeHour=22, signColor=new Color(0.85f,0.16f,0.12f),
                    districts=new[]{ DistrictType.Commercial, DistrictType.Industrial, DistrictType.Marigold, DistrictType.Suburb, DistrictType.Badlands } },

                new ShopDefinition { id="threads", displayName="Threadline", type=ShopType.ClothingStore,
                    openHour=9, closeHour=21, signColor=new Color(0.35f,0.55f,0.85f),
                    districts=new[]{ DistrictType.Commercial, DistrictType.Downtown, DistrictType.Beach, DistrictType.Marigold } },

                new ShopDefinition { id="highend", displayName="Crestwood Atelier", type=ShopType.ClothingStore,
                    priceMultiplier=3.4f, openHour=10, closeHour=20, signColor=new Color(0.85f,0.78f,0.45f),
                    districts=new[]{ DistrictType.Wealthy, DistrictType.Marina, DistrictType.Downtown } },

                new ShopDefinition { id="barber", displayName="Cut Above", type=ShopType.Barber,
                    openHour=9, closeHour=20, signColor=new Color(0.85f,0.35f,0.35f),
                    districts=new[]{ DistrictType.Commercial, DistrictType.Residential, DistrictType.Marigold } },

                new ShopDefinition { id="mechanic", displayName="Rook's Garage", type=ShopType.Mechanic,
                    openHour=7, closeHour=23, signColor=new Color(0.95f,0.62f,0.10f),
                    districts=new[]{ DistrictType.Industrial, DistrictType.Residential, DistrictType.Suburb, DistrictType.Port } },

                new ShopDefinition { id="dealership", displayName="Vireo Motors Showroom", type=ShopType.Dealership,
                    openHour=9, closeHour=20, signColor=new Color(0.20f,0.60f,0.90f),
                    districts=new[]{ DistrictType.Commercial, DistrictType.Downtown } },

                new ShopDefinition { id="luxury-dealer", displayName="Falcorne Prestige", type=ShopType.Dealership,
                    priceMultiplier=1f, openHour=10, closeHour=19, signColor=new Color(0.80f,0.72f,0.30f),
                    districts=new[]{ DistrictType.Wealthy, DistrictType.Marina } },

                new ShopDefinition { id="marine-dealer", displayName="Halcyon Marine Sales", type=ShopType.Marine,
                    openHour=8, closeHour=19, signColor=new Color(0.20f,0.55f,0.70f),
                    districts=new[]{ DistrictType.Marina, DistrictType.Port } },

                new ShopDefinition { id="aviation-dealer", displayName="Aeris Flight Center", type=ShopType.Aviation,
                    openHour=7, closeHour=20, signColor=new Color(0.55f,0.60f,0.70f),
                    districts=new[]{ DistrictType.Airport } },

                new ShopDefinition { id="gas", displayName="Cinder Fuel", type=ShopType.GasStation,
                    openHour=0, closeHour=0, signColor=new Color(0.95f,0.75f,0.10f),
                    districts=new[]{ DistrictType.Suburb, DistrictType.Highway, DistrictType.Residential, DistrictType.Industrial, DistrictType.Farmland, DistrictType.Badlands, DistrictType.Commercial } },

                new ShopDefinition { id="diner", displayName="Blue Heron Diner", type=ShopType.Restaurant,
                    openHour=6, closeHour=1, signColor=new Color(0.30f,0.65f,0.85f),
                    districts=new[]{ DistrictType.Residential, DistrictType.Suburb, DistrictType.Highway, DistrictType.Commercial, DistrictType.Beach } },

                new ShopDefinition { id="taqueria", displayName="Marigold Taqueria", type=ShopType.Restaurant,
                    openHour=10, closeHour=2, signColor=new Color(0.92f,0.55f,0.12f),
                    districts=new[]{ DistrictType.Marigold, DistrictType.Residential } },

                new ShopDefinition { id="minimart", displayName="Corner Ten", type=ShopType.ConvenienceStore,
                    openHour=0, closeHour=0, signColor=new Color(0.25f,0.75f,0.45f),
                    districts=new[]{ DistrictType.Residential, DistrictType.Marigold, DistrictType.Commercial, DistrictType.Suburb, DistrictType.Beach, DistrictType.Industrial } },

                new ShopDefinition { id="pharmacy", displayName="Meridian Pharmacy", type=ShopType.Pharmacy,
                    openHour=8, closeHour=22, signColor=new Color(0.30f,0.80f,0.60f),
                    districts=new[]{ DistrictType.Commercial, DistrictType.Residential, DistrictType.Downtown } },

                new ShopDefinition { id="hospital", displayName="San Monica General", type=ShopType.Hospital,
                    openHour=0, closeHour=0, signColor=new Color(0.90f,0.25f,0.25f),
                    districts=new[]{ DistrictType.Downtown, DistrictType.Commercial, DistrictType.Suburb } },

                new ShopDefinition { id="precinct", displayName="SMPD Precinct", type=ShopType.PoliceStation,
                    openHour=0, closeHour=0, signColor=new Color(0.20f,0.35f,0.75f),
                    districts=new[]{ DistrictType.Downtown, DistrictType.Commercial, DistrictType.Residential, DistrictType.Suburb, DistrictType.Port } },

                new ShopDefinition { id="club", displayName="Static Room", type=ShopType.Nightclub,
                    openHour=21, closeHour=5, signColor=new Color(0.75f,0.20f,0.85f),
                    districts=new[]{ DistrictType.Downtown, DistrictType.Marigold, DistrictType.Beach } },

                new ShopDefinition { id="armoury", displayName="Vela Armoury", type=ShopType.AmmuNation,
                    priceMultiplier=0.85f, openHour=0, closeHour=0, signColor=new Color(0.72f,0.20f,0.18f),
                    districts=new[]{ DistrictType.Industrial, DistrictType.Port, DistrictType.Badlands, DistrictType.Marigold } },

                new ShopDefinition { id="hardware", displayName="Foundry Supply", type=ShopType.Hardware,
                    openHour=7, closeHour=19, signColor=new Color(0.60f,0.45f,0.25f),
                    districts=new[]{ DistrictType.Industrial, DistrictType.Suburb, DistrictType.Farmland } },
            };
        }
    }

    public enum PropertyKind { Apartment, House, Villa, Garage, Business, Warehouse, Penthouse }

    [System.Serializable]
    public class PropertyDefinition
    {
        public string id;
        public string displayName;
        public PropertyKind kind;
        public int price;
        public int dailyIncome;
        public int garageSlots;
        public bool allowsSave = true;
        public bool allowsWardrobe = true;
        public DistrictType district;
        public Vector3 position;
        public Vector3 spawnPoint;
        public float heading;
    }

    [System.Serializable]
    public class RadioStationDefinition
    {
        public string id;
        public string displayName;
        public string genre;
        public string dj;
        public int rootNote = 45;
        public float bpm = 110f;
        public float energy = 0.6f;
        public float distortion = 0.2f;
        public bool talkOnly = false;
        public Color accent = Color.white;
        public string[] djLines;
        public string[] adverts;
        public string[] newsLines;
    }
}
