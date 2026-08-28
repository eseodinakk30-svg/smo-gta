using UnityEngine;

namespace SanMonica.Core
{
    /// <summary>
    /// Flat service locator. Populated once during boot by <see cref="GameManager"/>.
    /// Using explicit static references keeps per-frame lookups free of
    /// GetComponent / FindObjectOfType calls, which matters a lot on mobile.
    /// </summary>
    public static class Services
    {
        public static GameManager Game;
        public static PoolRegistry Pools;

        public static SanMonica.Data.WorldConfig Config;
        public static SanMonica.Data.GameDatabase Database;

        public static SanMonica.World.WorldMap Map;
        public static SanMonica.World.RoadNetwork Roads;
        public static SanMonica.World.ChunkStreamer Streamer;
        public static SanMonica.World.InteriorSystem Interiors;
        public static SanMonica.World.WaterSystem Water;
        public static SanMonica.World.LandmarkRegistry Landmarks;

        public static SanMonica.Characters.PedFactory Peds;
        public static SanMonica.Vehicles.VehicleFactory Vehicles;

        public static SanMonica.Players.PlayerController Player;
        public static SanMonica.CameraRig.GameCamera Camera;
        public static SanMonica.Players.InputHub Input;

        public static SanMonica.AI.NavGraph Nav;
        public static SanMonica.AI.PopulationManager Population;
        public static SanMonica.AI.AILodManager AiLod;
        public static SanMonica.Traffic.TrafficManager Traffic;

        public static SanMonica.Police.WantedSystem Wanted;
        public static SanMonica.Police.PoliceDispatch Police;

        public static SanMonica.Weapons.WeaponCatalog Weapons;
        public static SanMonica.Weapons.EffectsSystem Effects;

        public static SanMonica.Missions.MissionSystem Missions;
        public static SanMonica.Missions.DialogueSystem Dialogue;
        public static SanMonica.Missions.RandomEventSystem RandomEvents;

        public static SanMonica.Economy.EconomySystem Economy;
        public static SanMonica.Economy.ShopSystem Shops;
        public static SanMonica.Economy.PropertySystem Property;
        public static SanMonica.Economy.GarageSystem Garage;

        public static SanMonica.Atmosphere.TimeOfDaySystem Clock;
        public static SanMonica.Atmosphere.WeatherSystem Weather;
        public static SanMonica.Atmosphere.SkySystem Sky;
        public static SanMonica.Atmosphere.PostProcessRig PostProcess;

        public static SanMonica.Audio.AudioSystem Audio;
        public static SanMonica.Audio.RadioSystem Radio;

        public static SanMonica.UI.UIManager UI;
        public static SanMonica.Saves.SaveSystem Save;
        public static SanMonica.Optimization.QualityManager Quality;
        public static SanMonica.Optimization.PerformanceMonitor Perf;

        public static Transform PlayerTransform
        {
            get
            {
                if (Player == null) return null;
                return Player.CurrentVehicle != null ? Player.CurrentVehicle.transform : Player.transform;
            }
        }

        public static Vector3 PlayerPosition
        {
            get
            {
                var t = PlayerTransform;
                return t != null ? t.position : Vector3.zero;
            }
        }

        public static void Clear()
        {
            Game = null; Pools = null; Config = null; Database = null;
            Map = null; Roads = null; Streamer = null; Interiors = null; Water = null; Landmarks = null;
            Peds = null; Vehicles = null; Player = null; Camera = null; Input = null;
            Nav = null; Population = null; AiLod = null; Traffic = null;
            Wanted = null; Police = null; Weapons = null; Effects = null;
            Missions = null; Dialogue = null; RandomEvents = null;
            Economy = null; Shops = null; Property = null; Garage = null;
            Clock = null; Weather = null; Sky = null; PostProcess = null; Audio = null; Radio = null;
            UI = null; Save = null; Quality = null; Perf = null;
        }
    }
}
