using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;

namespace SanMonica.Saves
{
    /// <summary>
    /// Persistence for San Monica: three manual slots plus an autosave, and a
    /// separate settings file. Saves store progress and state, never world
    /// geometry - the world is regenerated from its seed, so files stay tiny.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public const int SlotCount = 3;
        public const string AutoSaveName = "autosave";

        public SettingsData Settings { get; private set; } = new SettingsData();
        public float PlaySeconds { get; private set; }
        public int Kills { get; private set; }
        public int VehiclesDestroyed { get; private set; }
        public int Outfit { get; private set; } = -1;
        public int Hairstyle { get; private set; } = -1;

        private float _autoSaveTimer;
        public float AutoSaveIntervalSeconds = 300f;
        public bool AutoSaveEnabled = true;

        private string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, "sanmonica_slot" + slot + ".json");
        private string AutoPath => Path.Combine(Application.persistentDataPath, "sanmonica_" + AutoSaveName + ".json");
        private string SettingsPath => Path.Combine(Application.persistentDataPath, "sanmonica_settings.json");

        public void Initialize()
        {
            LoadSettings();
            GameEvents.PedKilled += (victim, killer) =>
            {
                if (Services.Player != null && killer == Services.Player.gameObject) Kills++;
            };
            GameEvents.VehicleDestroyed += _ => VehiclesDestroyed++;
        }

        private void Update()
        {
            if (Services.Game == null || Services.Game.State != GameState.Playing) return;
            PlaySeconds += Time.deltaTime;

            if (!AutoSaveEnabled) return;
            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= AutoSaveIntervalSeconds)
            {
                _autoSaveTimer = 0f;
                AutoSave();
            }
        }

        public void SetOutfit(int outfit) => Outfit = outfit;
        public void SetHairstyle(int hairstyle) => Hairstyle = hairstyle;

        // ------------------------------------------------------------------
        public SaveData Capture()
        {
            var data = new SaveData();
            data.savedAtUtc = System.DateTime.UtcNow.ToString("u");
            data.worldSeed = Services.Config != null ? Services.Config.seed : 0;

            var clock = Services.Clock;
            if (clock != null) { data.timeOfDay = clock.TimeOfDay; data.day = clock.Day; }

            var weather = Services.Weather;
            if (weather != null)
            {
                var state = weather.CaptureState();
                data.weatherCurrent = state.current;
                data.weatherNext = state.next;
                data.weatherBlend = state.blend;
                data.weatherTimer = state.timer;
            }

            var player = Services.Player;
            if (player != null)
            {
                Vector3 position = player.InVehicle && player.CurrentVehicle != null
                    ? player.CurrentVehicle.transform.position : player.transform.position;
                data.px = position.x; data.py = position.y; data.pz = position.z;
                data.heading = player.transform.eulerAngles.y;
                if (player.Health != null)
                {
                    data.health = player.Health.Health;
                    data.maxHealth = player.Health.MaxHealth;
                    data.armour = player.Health.Armour;
                }
                data.inVehicle = player.InVehicle;
                data.vehicleDefinitionId = player.CurrentVehicle != null && player.CurrentVehicle.Definition != null
                    ? player.CurrentVehicle.Definition.id : null;

                if (player.Weapons != null)
                {
                    foreach (var slot in player.Weapons.Slots)
                    {
                        if (slot.Definition == null) continue;
                        data.weapons.Add(new WeaponSaveEntry { id = slot.Definition.id, magazine = slot.Magazine });
                    }
                    foreach (var kv in player.Weapons.ReserveSnapshot())
                        data.ammo.Add(new AmmoSaveEntry { type = (int)kv.Key, amount = kv.Value });
                }
            }

            var economy = Services.Economy;
            if (economy != null)
            {
                data.money = economy.Money;
                data.totalEarned = economy.TotalEarned;
                data.totalSpent = economy.TotalSpent;
            }

            data.wantedLevel = Services.Wanted != null ? Services.Wanted.Level : 0;

            var missions = Services.Missions;
            if (missions != null)
            {
                var state = missions.CaptureState();
                data.completedMissions = state.completed;
                data.chapter = state.chapter;
                data.respect = state.respect;
                data.hostileFactions = state.hostileFactions;
            }

            if (Services.Property != null) data.ownedProperties = Services.Property.CaptureState();

            if (Services.Garage != null)
            {
                foreach (var entry in Services.Garage.Collection)
                    data.garage.Add(new VehicleSaveEntry
                    {
                        definitionId = entry.DefinitionId,
                        engine = entry.Engine, brakes = entry.Brakes, grip = entry.Grip, armour = entry.Armour,
                        r = entry.PaintR, g = entry.PaintG, b = entry.PaintB
                    });
            }

            data.radioStation = Services.Radio != null ? Services.Radio.CaptureState() : -1;
            data.outfit = Outfit;
            data.hairstyle = Hairstyle;
            data.playSeconds = PlaySeconds;
            data.kills = Kills;
            data.vehiclesDestroyed = VehiclesDestroyed;
            data.displayName = DescribeProgress(data);
            return data;
        }

        private static string DescribeProgress(SaveData data)
        {
            return "Chapter " + data.chapter + " • $" + data.money.ToString("N0") + " • " + data.completedMissions.Count + " missions";
        }

        // ------------------------------------------------------------------
        public void Apply(SaveData data)
        {
            if (data == null) return;

            var clock = Services.Clock;
            if (clock != null) clock.SetTime(data.timeOfDay, data.day);

            Services.Weather?.RestoreState(new SanMonica.Atmosphere.WeatherSaveState
            {
                current = data.weatherCurrent, next = data.weatherNext,
                blend = data.weatherBlend, timer = data.weatherTimer
            });

            Services.Economy?.SetMoney(data.money);
            Services.Wanted?.SetLevelDirect(data.wantedLevel);

            Services.Missions?.RestoreState(new SanMonica.Missions.MissionSaveState
            {
                completed = data.completedMissions,
                chapter = data.chapter,
                respect = data.respect,
                hostileFactions = data.hostileFactions
            });

            Services.Property?.RestoreState(data.ownedProperties);

            if (Services.Garage != null)
            {
                var collection = new List<SanMonica.Economy.GarageSystem.OwnedVehicle>();
                foreach (var entry in data.garage)
                    collection.Add(new SanMonica.Economy.GarageSystem.OwnedVehicle
                    {
                        DefinitionId = entry.definitionId,
                        Engine = entry.engine, Brakes = entry.brakes, Grip = entry.grip, Armour = entry.armour,
                        PaintR = entry.r, PaintG = entry.g, PaintB = entry.b
                    });
                Services.Garage.RestoreState(collection);
            }

            var player = Services.Player;
            if (player != null)
            {
                if (player.InVehicle) player.ExitVehicle();
                player.Teleport(new Vector3(data.px, data.py + 0.5f, data.pz), data.heading);
                if (player.Health != null)
                {
                    player.Health.ResetVitals(data.maxHealth, data.armour);
                    player.Health.Health = Mathf.Clamp(data.health, 1f, data.maxHealth);
                }

                if (player.Weapons != null)
                {
                    player.Weapons.ClearAll();
                    var database = Services.Database;
                    foreach (var entry in data.weapons)
                    {
                        var definition = database?.Weapon(entry.id);
                        if (definition == null) continue;
                        player.Weapons.GiveWeapon(definition, 0, false);
                    }
                    foreach (var entry in data.ammo)
                        player.Weapons.AddAmmo((AmmoType)entry.type, entry.amount);
                }

                if (data.inVehicle && !string.IsNullOrEmpty(data.vehicleDefinitionId) && Services.Vehicles != null)
                {
                    var definition = Services.Database?.Vehicle(data.vehicleDefinitionId);
                    if (definition != null)
                    {
                        var vehicle = Services.Vehicles.Spawn(definition,
                            new Vector3(data.px, data.py + definition.wheelRadius + 0.2f, data.pz),
                            Quaternion.Euler(0f, data.heading, 0f));
                        if (vehicle != null)
                        {
                            vehicle.IsPlayerOwned = true;
                            vehicle.HasOwner = false;
                            player.EnterVehicle(vehicle, 0);
                        }
                    }
                }
            }

            Services.Radio?.RestoreState(data.radioStation);
            Outfit = data.outfit;
            Hairstyle = data.hairstyle;
            // The saved look was written down and then never worn again.
            Services.Player?.RestoreAppearance(Outfit, Hairstyle);
            PlaySeconds = data.playSeconds;
            Kills = data.kills;
            VehiclesDestroyed = data.vehiclesDestroyed;
        }

        // ------------------------------------------------------------------
        public void SaveToSlot(int slot)
        {
            try
            {
                var data = Capture();
                File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
                GameEvents.Notify("Saved to slot " + (slot + 1), 2.5f);
                Services.Audio?.PlayUi("purchase");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Save failed: " + e.Message);
                GameEvents.Notify("Save failed", 3f);
            }
        }

        public void AutoSave()
        {
            try
            {
                var data = Capture();
                File.WriteAllText(AutoPath, JsonUtility.ToJson(data));
                GameEvents.Notify("Progress saved", 1.8f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Autosave failed: " + e.Message);
            }
        }

        public bool LoadFromSlot(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) { GameEvents.Notify("Slot " + (slot + 1) + " is empty", 2.5f); return false; }
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                Apply(data);
                GameEvents.Notify("Loaded slot " + (slot + 1), 2.5f);
                Services.Game?.Resume();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Load failed: " + e.Message);
                GameEvents.Notify("Load failed", 3f);
                return false;
            }
        }

        public bool LoadAutoSave()
        {
            if (!File.Exists(AutoPath)) return false;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(AutoPath));
                Apply(data);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Autosave load failed: " + e.Message);
                return false;
            }
        }

        public bool HasAutoSave() => File.Exists(AutoPath);

        public string DescribeSlot(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return "Empty";
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data.displayName + "\n" + data.savedAtUtc;
            }
            catch { return "Corrupt save"; }
        }

        // ------------------------------------------------------------------
        public void SaveSettings()
        {
            var quality = Services.Quality;
            var audio = Services.Audio;
            var input = Services.Input;
            var ui = Services.UI;
            var camera = Services.Camera;

            if (quality != null)
            {
                Settings.qualityPreset = (int)quality.Preset;
                Settings.autoQuality = quality.AutoQuality;
                Settings.renderScale = quality.RenderScale;
                Settings.drawDistance = quality.DrawDistanceScale;
                Settings.pedDensity = quality.PedDensity;
                Settings.trafficDensity = quality.TrafficDensity;
                Settings.targetFrameRate = quality.TargetFrameRate;
            }
            if (audio != null)
            {
                Settings.masterVolume = audio.MasterVolume;
                Settings.musicVolume = audio.MusicVolume;
                Settings.sfxVolume = audio.SfxVolume;
                Settings.ambienceVolume = audio.AmbienceVolume;
                Settings.uiVolume = audio.UiVolume;
            }
            if (input != null)
            {
                Settings.lookSensitivity = input.LookSensitivity;
                Settings.aimSensitivity = input.AimSensitivityMultiplier;
                Settings.invertY = input.InvertY;
            }
            if (ui != null && ui.Touch != null)
            {
                Settings.touchScale = ui.Touch.Scale;
                Settings.touchOpacity = ui.Touch.Opacity;
                Settings.touchEnabled = ui.Touch.Enabled;
                Settings.touchLayout.Clear();
                foreach (var kv in ui.Touch.CaptureLayout())
                    Settings.touchLayout.Add(new TouchButtonLayoutEntry { id = kv.Key, x = kv.Value.x, y = kv.Value.y });
            }
            if (camera != null) Settings.fieldOfView = camera.BaseFov;

            try { File.WriteAllText(SettingsPath, JsonUtility.ToJson(Settings, true)); }
            catch (System.Exception e) { Debug.LogWarning("Settings save failed: " + e.Message); }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    Settings = JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsPath)) ?? new SettingsData();
            }
            catch { Settings = new SettingsData(); }
        }

        public void ApplySettings()
        {
            var quality = Services.Quality;
            var audio = Services.Audio;
            var input = Services.Input;
            var ui = Services.UI;
            var camera = Services.Camera;

            if (quality != null)
            {
                quality.ApplyPreset((SanMonica.Optimization.QualityPreset)Mathf.Clamp(Settings.qualityPreset, 0, 3));
                quality.AutoQuality = Settings.autoQuality;
                quality.SetRenderScale(Settings.renderScale);
                quality.SetDrawDistance(Settings.drawDistance);
                quality.SetPedDensity(Settings.pedDensity);
                quality.SetTrafficDensity(Settings.trafficDensity);
                quality.SetTargetFrameRate(Settings.targetFrameRate);
            }
            if (audio != null)
            {
                audio.MasterVolume = Settings.masterVolume;
                audio.MusicVolume = Settings.musicVolume;
                audio.SfxVolume = Settings.sfxVolume;
                audio.AmbienceVolume = Settings.ambienceVolume;
                audio.UiVolume = Settings.uiVolume;
                audio.ApplyVolumes();
            }
            if (input != null)
            {
                input.LookSensitivity = Settings.lookSensitivity;
                input.AimSensitivityMultiplier = Settings.aimSensitivity;
                input.InvertY = Settings.invertY;
            }
            if (ui != null && ui.Touch != null)
            {
                ui.Touch.Scale = Settings.touchScale;
                ui.Touch.Opacity = Settings.touchOpacity;
                ui.Touch.Enabled = Settings.touchEnabled;
                var layout = new Dictionary<string, Vector2>();
                foreach (var entry in Settings.touchLayout) layout[entry.id] = new Vector2(entry.x, entry.y);
                ui.Touch.RestoreLayout(layout);
                ui.Touch.ApplyAppearance();
            }
            if (camera != null) camera.BaseFov = Settings.fieldOfView;
        }
    }
}
