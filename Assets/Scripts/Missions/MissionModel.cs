using System.Collections.Generic;
using UnityEngine;
using SanMonica.Data;

namespace SanMonica.Missions
{
    public enum ObjectiveType
    {
        GoTo, GoToInVehicle, KillTarget, KillAll, StealVehicle, DeliverVehicle,
        Deliver, Survive, Escape, LoseWanted, Protect, Collect, Race, Wait,
        Follow, DestroyVehicle, EnterVehicle, ExitVehicle, Talk
    }

    public enum AnchorKind { WorldPoint, DistrictCentre, NearestShop, RandomInDistrict, PlayerRelative, Landmark }

    public enum LandmarkKind { Downtown, Port, Marina, Airport, University, Crestwood, Foundry, Marigold, Park, Beach, Badlands, Farmland, Mountains }

    /// <summary>Late-bound position: the world is generated, so mission spots resolve at runtime.</summary>
    [System.Serializable]
    public struct MissionAnchor
    {
        public AnchorKind Kind;
        public Vector3 Point;
        public DistrictType District;
        public ShopType Shop;
        public LandmarkKind Landmark;
        public float Radius;

        public static MissionAnchor At(Vector3 p) => new MissionAnchor { Kind = AnchorKind.WorldPoint, Point = p };
        public static MissionAnchor In(DistrictType d, float radius = 180f) => new MissionAnchor { Kind = AnchorKind.RandomInDistrict, District = d, Radius = radius };
        public static MissionAnchor Centre(DistrictType d) => new MissionAnchor { Kind = AnchorKind.DistrictCentre, District = d };
        public static MissionAnchor Near(ShopType s) => new MissionAnchor { Kind = AnchorKind.NearestShop, Shop = s };
        public static MissionAnchor Mark(LandmarkKind l, float radius = 0f) => new MissionAnchor { Kind = AnchorKind.Landmark, Landmark = l, Radius = radius };
        public static MissionAnchor FromPlayer(float radius) => new MissionAnchor { Kind = AnchorKind.PlayerRelative, Radius = radius };
    }

    [System.Serializable]
    public class MissionObjective
    {
        public ObjectiveType Type;
        public string Description = "Objective";
        public MissionAnchor Anchor;
        public float Radius = 8f;
        public float TimeLimit;                 // 0 = untimed
        public int Count = 1;
        public string PedArchetype;             // enemies to spawn
        public string VehicleId;
        public int EnemyCount;
        public bool EnemiesArmed = true;
        public bool ShowMarker = true;
        public string[] Lines;                  // dialogue played when the objective starts
        public Faction EnemyFaction = Faction.SerranoCartel;
        public bool FailIfTargetDies;
        public int WantedLevelOnStart;
        public float SurviveSeconds;
    }

    public enum MissionKind { Story, Side, Race, Delivery, Heist, Assassination, Protection, Chase, Random }

    [System.Serializable]
    public class MissionDefinition
    {
        public string Id;
        public string Title;
        public string Giver;
        public int Chapter;
        public MissionKind Kind = MissionKind.Story;
        public string Briefing;
        public MissionAnchor StartAnchor;
        public string[] Prerequisites;
        public int RewardCash;
        public int RewardRespect;
        public string[] IntroLines;
        public string[] OutroLines;
        public List<MissionObjective> Objectives = new List<MissionObjective>();
        public bool RepeatableAfterCompletion;
        public int MinChapterUnlocked;
        public Faction TurnsHostile = Faction.Civilian;
    }
}
