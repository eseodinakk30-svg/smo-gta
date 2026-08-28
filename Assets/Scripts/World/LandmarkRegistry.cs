using System.Collections.Generic;
using UnityEngine;
using SanMonica.Data;

namespace SanMonica.World
{
    public enum BlipKind
    {
        Player, Mission, MissionGiver, Shop, GunStore, Clothing, Barber, Mechanic,
        Dealership, GasStation, Restaurant, Store, Hospital, Police, Property,
        Garage, Vehicle, Enemy, Waypoint, RandomEvent, Nightclub, Airport, Marina
    }

    public class MapBlip
    {
        public BlipKind Kind;
        public Vector3 Position;
        public string Label;
        public Color Color = Color.white;
        public bool ShowOnMinimap = true;
        public bool Persistent = true;
        public float Radius;              // >0 draws an area circle
        public object UserData;
    }

    /// <summary>
    /// Everything the map and the mission system need to know about places in
    /// San Monica: shops, property, precincts, hospitals and dynamic markers.
    /// </summary>
    public class LandmarkRegistry
    {
        public CityLayout Layout { get; private set; }
        public readonly List<MapBlip> StaticBlips = new List<MapBlip>(512);
        public readonly List<MapBlip> DynamicBlips = new List<MapBlip>(64);

        public void Initialize(CityLayout layout)
        {
            Layout = layout;
            StaticBlips.Clear();

            foreach (var shop in layout.Shops)
            {
                var kind = KindFor(shop.Definition.type);
                StaticBlips.Add(new MapBlip
                {
                    Kind = kind,
                    Position = shop.Position,
                    Label = shop.Definition.displayName,
                    Color = shop.Definition.signColor,
                    UserData = shop
                });
            }

            foreach (var prop in layout.Properties)
            {
                StaticBlips.Add(new MapBlip
                {
                    Kind = prop.Definition.kind == PropertyKind.Garage ? BlipKind.Garage : BlipKind.Property,
                    Position = prop.Definition.position,
                    Label = prop.Definition.displayName,
                    Color = new Color(0.30f, 0.85f, 0.45f),
                    UserData = prop
                });
            }
        }

        private static BlipKind KindFor(ShopType t)
        {
            switch (t)
            {
                case ShopType.GunStore: return BlipKind.GunStore;
                case ShopType.ClothingStore: return BlipKind.Clothing;
                case ShopType.Barber: return BlipKind.Barber;
                case ShopType.Mechanic: return BlipKind.Mechanic;
                case ShopType.Dealership:
                case ShopType.Marine: return BlipKind.Dealership;
                case ShopType.Aviation: return BlipKind.Airport;
                case ShopType.GasStation: return BlipKind.GasStation;
                case ShopType.Restaurant: return BlipKind.Restaurant;
                case ShopType.Hospital: return BlipKind.Hospital;
                case ShopType.PoliceStation: return BlipKind.Police;
                case ShopType.Nightclub: return BlipKind.Nightclub;
                default: return BlipKind.Store;
            }
        }

        public MapBlip AddDynamic(BlipKind kind, Vector3 pos, string label, Color color, float radius = 0f)
        {
            var b = new MapBlip { Kind = kind, Position = pos, Label = label, Color = color, Persistent = false, Radius = radius };
            DynamicBlips.Add(b);
            return b;
        }

        public void RemoveDynamic(MapBlip blip)
        {
            if (blip != null) DynamicBlips.Remove(blip);
        }

        public void ClearDynamic(BlipKind kind)
        {
            DynamicBlips.RemoveAll(b => b.Kind == kind);
        }

        public ShopInstance NearestShop(Vector3 pos, ShopType type) => Layout?.NearestShop(pos, type);

        public Vector3 NearestHospital(Vector3 pos)
        {
            var s = Layout?.NearestShop(pos, ShopType.Hospital);
            return s != null ? s.Position : pos;
        }

        public Vector3 NearestPoliceStation(Vector3 pos)
        {
            var s = Layout?.NearestShop(pos, ShopType.PoliceStation);
            return s != null ? s.Position : pos;
        }

        /// <summary>Shop whose doorway is within reach of a position, for the interact prompt.</summary>
        public ShopInstance ShopAt(Vector3 pos, float radius = 2.6f)
        {
            if (Layout == null) return null;
            float best = radius * radius;
            ShopInstance found = null;
            foreach (var s in Layout.Shops)
            {
                float d = (s.Position - pos).sqrMagnitude;
                if (d < best) { best = d; found = s; }
            }
            return found;
        }

        public PropertyInstance PropertyAt(Vector3 pos, float radius = 2.6f)
        {
            if (Layout == null) return null;
            float best = radius * radius;
            PropertyInstance found = null;
            foreach (var p in Layout.Properties)
            {
                float d = (p.Definition.position - pos).sqrMagnitude;
                if (d < best) { best = d; found = p; }
            }
            return found;
        }
    }
}
