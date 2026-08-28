using UnityEngine;

namespace SanMonica.Core
{
    /// <summary>
    /// Central definition of every physics layer used by the game.
    /// Layer indices mirror ProjectSettings/TagManager.asset.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Ground = 8;
        public const int Building = 9;
        public const int Prop = 10;
        public const int Player = 11;
        public const int Ped = 12;
        public const int Vehicle = 13;
        public const int VehicleWheel = 14;
        public const int Projectile = 15;
        public const int Interactable = 16;
        public const int Ragdoll = 17;
        public const int Foliage = 18;
        public const int Terrain = 19;
        public const int Road = 20;
        public const int MinimapOnly = 21;
        public const int Trigger = 22;

        public static readonly int GroundMask = (1 << Ground) | (1 << Building) | (1 << Terrain) | (1 << Road) | (1 << Prop) | (1 << Default);
        public static readonly int WorldMask = GroundMask | (1 << Vehicle);
        public static readonly int ShootableMask = GroundMask | (1 << Vehicle) | (1 << Ped) | (1 << Player) | (1 << Ragdoll) | (1 << Foliage);
        public static readonly int CharacterMask = (1 << Ped) | (1 << Player);
        public static readonly int VisionBlockMask = (1 << Building) | (1 << Ground) | (1 << Terrain) | (1 << Prop);
        public static readonly int CameraCollisionMask = (1 << Building) | (1 << Ground) | (1 << Terrain) | (1 << Prop) | (1 << Road);
        public static readonly int WaterMask = 1 << Water;
        public static readonly int VehicleMask = 1 << Vehicle;

        /// <summary>Configures layer collision rules. Called once during boot.</summary>
        public static void ApplyCollisionMatrix()
        {
            // Wheels only collide with drivable surfaces.
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(VehicleWheel, i, true);
            Physics.IgnoreLayerCollision(VehicleWheel, Ground, false);
            Physics.IgnoreLayerCollision(VehicleWheel, Road, false);
            Physics.IgnoreLayerCollision(VehicleWheel, Terrain, false);
            Physics.IgnoreLayerCollision(VehicleWheel, Building, false);

            Physics.IgnoreLayerCollision(Ped, Ped, true);
            Physics.IgnoreLayerCollision(Ped, Ragdoll, true);
            Physics.IgnoreLayerCollision(Ragdoll, Ragdoll, true);
            Physics.IgnoreLayerCollision(Projectile, Projectile, true);
            Physics.IgnoreLayerCollision(Projectile, Trigger, true);
            Physics.IgnoreLayerCollision(Trigger, Trigger, true);
            Physics.IgnoreLayerCollision(MinimapOnly, MinimapOnly, true);
            Physics.IgnoreLayerCollision(Foliage, Ped, true);
            Physics.IgnoreLayerCollision(Foliage, Foliage, true);
            Physics.IgnoreLayerCollision(Water, Water, true);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
