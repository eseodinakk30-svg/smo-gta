using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Saves
{
    [System.Serializable]
    public class WeaponSaveEntry
    {
        public string id;
        public int magazine;
    }

    [System.Serializable]
    public class AmmoSaveEntry
    {
        public int type;
        public int amount;
    }

    [System.Serializable]
    public class VehicleSaveEntry
    {
        public string definitionId;
        public int engine, brakes, grip, armour;
        public float r = 1f, g = 1f, b = 1f;
    }

    [System.Serializable]
    public class TouchButtonLayoutEntry
    {
        public string id;
        public float x, y;
    }

    /// <summary>Everything a San Monica save file remembers.</summary>
    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public string savedAtUtc;
        public string displayName = "San Monica";

        // World
        public int worldSeed;
        public float timeOfDay = 8.5f;
        public int day = 1;
        public int weatherCurrent;
        public int weatherNext;
        public float weatherBlend;
        public float weatherTimer;

        // Player
        public float px, py, pz, heading;
        public float health = 200f, maxHealth = 200f, armour;
        public int outfit = -1;
        public int hairstyle = -1;
        public bool inVehicle;
        public string vehicleDefinitionId;

        // Progress
        public long money;
        public long totalEarned;
        public long totalSpent;
        public int wantedLevel;
        public int chapter = 1;
        public int respect;
        public List<string> completedMissions = new List<string>();
        public List<int> hostileFactions = new List<int>();
        public List<string> ownedProperties = new List<string>();
        public List<VehicleSaveEntry> garage = new List<VehicleSaveEntry>();
        public List<WeaponSaveEntry> weapons = new List<WeaponSaveEntry>();
        public List<AmmoSaveEntry> ammo = new List<AmmoSaveEntry>();
        public int radioStation = -1;

        // Statistics
        public float playSeconds;
        public int kills;
        public int vehiclesDestroyed;
        public int distanceDriven;
    }

    /// <summary>Options are stored separately so they survive starting a new game.</summary>
    [System.Serializable]
    public class SettingsData
    {
        public int qualityPreset = 2;
        public bool autoQuality = true;
        public float renderScale = 1f;
        public float drawDistance = 1f;
        public float pedDensity = 1f;
        public float trafficDensity = 1f;
        public int targetFrameRate = 60;

        public float masterVolume = 1f;
        public float musicVolume = 0.65f;
        public float sfxVolume = 0.9f;
        public float ambienceVolume = 0.6f;
        public float uiVolume = 0.8f;

        public float lookSensitivity = 1f;
        public float aimSensitivity = 0.55f;
        public bool invertY;
        public float touchScale = 1f;
        public float touchOpacity = 0.55f;
        public bool touchEnabled = SanMonica.UI.TouchControls.DefaultEnabled;
        public float fieldOfView = 62f;

        public List<TouchButtonLayoutEntry> touchLayout = new List<TouchButtonLayoutEntry>();
    }
}
