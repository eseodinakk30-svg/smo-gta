using System.Collections;
using UnityEngine;
using SanMonica.Data;
using SanMonica.Utils;
using SanMonica.World;

namespace SanMonica.Core
{
    /// <summary>
    /// Boots San Monica and owns the game state. Creates every system in
    /// dependency order, generates the world, spawns the player and then runs
    /// the pause, death, arrest, shop and interior flows.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Booting;
        public bool WorldReady { get; private set; }
        public Vector3 LastSafePosition { get; private set; }

        private Transform _systems;
        private Coroutine _bootRoutine;
        private float _respawnTimer;
        private bool _respawning;

        // ------------------------------------------------------------------
        public void Boot(int seed)
        {
            if (_bootRoutine != null) return;
            _bootRoutine = StartCoroutine(BootSequence(seed));
        }

        private IEnumerator BootSequence(int seed)
        {
            Services.Game = this;
            _systems = new GameObject("Systems").transform;
            _systems.SetParent(transform, false);

            SetState(GameState.Booting);
            GameLayers.ApplyCollisionMatrix();
            Physics.gravity = new Vector3(0f, -19.6f, 0f);

            // ---- configuration and data ----
            Services.Config = WorldConfig.CreateDefault(seed);
            Services.Pools = new PoolRegistry(_systems);
            Services.Database = GameDatabase.Build();

            // ---- foundational services ----
            Services.Quality = Add<Optimization.QualityManager>("Quality");
            Services.Perf = Add<Optimization.PerformanceMonitor>("Performance");
            Services.Audio = Add<Audio.AudioSystem>("Audio");
            Services.Audio.Initialize();
            Services.Input = Add<Players.InputHub>("Input");
            Services.Save = Add<Saves.SaveSystem>("Save");
            Services.Save.Initialize();

            Services.UI = Add<UI.UIManager>("UI");
            Services.UI.Build();
            Services.UI.ShowLoading(true);
            Report(0.02f, "Preparing San Monica");
            yield return null;

            // ---- camera ----
            var cameraGo = new GameObject("CameraRig");
            cameraGo.transform.SetParent(_systems, false);
            Services.Camera = cameraGo.AddComponent<CameraRig.GameCamera>();
            Services.Camera.Initialize();
            yield return null;

            // ---- world description ----
            SetState(GameState.GeneratingWorld);
            Report(0.06f, "Surveying the coastline");
            Services.Map = new WorldMap(Services.Config);
            yield return null;
            Services.Map.BuildDistrictGrid();
            Report(0.14f, "Laying out districts");
            yield return null;

            Services.Roads = new RoadNetwork(Services.Config, Services.Map);
            Services.Roads.Build();
            Report(0.24f, "Building the street network");
            yield return null;

            Report(0.28f, "Zoning the city blocks");
            yield return null;
            var layout = new CityLayout(Services.Config, Services.Map, Services.Roads, Services.Database);
            layout.Generate();
            Report(0.36f, layout.Lots.Count.ToString("N0") + " lots placed");
            yield return null;

            Services.Landmarks = new LandmarkRegistry();
            Services.Landmarks.Initialize(layout);

            Services.Nav = new AI.NavGraph();
            Services.Nav.Initialize(Services.Roads, Services.Map);

            // ---- environment ----
            Services.Clock = Add<Atmosphere.TimeOfDaySystem>("Clock");
            Services.Weather = Add<Atmosphere.WeatherSystem>("Weather");
            Services.Weather.Initialize();
            Services.Sky = Add<Atmosphere.SkySystem>("Sky");
            Services.Sky.Initialize(Services.Clock, Services.Weather);
            Services.PostProcess = Add<Atmosphere.PostProcessRig>("PostProcessing");
            Services.PostProcess.Initialize(Services.Camera.Cam);
            Services.Water = Add<WaterSystem>("Water");
            Services.Water.Initialize(Services.Config, Services.Map);
            Services.Interiors = Add<InteriorSystem>("Interiors");
            Services.Interiors.Initialize();
            Report(0.44f, "Setting the weather");
            yield return null;

            // ---- streaming ----
            Services.Streamer = Add<ChunkStreamer>("Streaming");
            Services.Streamer.Initialize(Services.Config, Services.Map, Services.Roads, layout);
            yield return null;

            // ---- factories ----
            Services.Vehicles = Add<Vehicles.VehicleFactory>("Vehicles");
            Services.Vehicles.Initialize(Services.Database);
            Services.Peds = Add<Characters.PedFactory>("Pedestrians");
            Services.Peds.Initialize(Services.Database);
            Services.Weapons = Add<Weapons.WeaponCatalog>("Weapons");
            Services.Weapons.Initialize(Services.Database);
            Services.Effects = Add<Weapons.EffectsSystem>("Effects");
            Services.Effects.Initialize();
            yield return null;

            // ---- simulation ----
            Services.Population = Add<AI.PopulationManager>("Population");
            Services.Population.Initialize(Services.Config, Services.Map, Services.Nav, Services.Peds, Services.Database);
            Services.AiLod = Add<AI.AILodManager>("AiLod");
            Services.AiLod.Initialize(Services.Peds);
            Services.Traffic = Add<Traffic.TrafficManager>("Traffic");
            Services.Traffic.Initialize(Services.Config, Services.Map, Services.Roads, layout, Services.Vehicles, Services.Database);
            Services.Wanted = Add<Police.WantedSystem>("Wanted");
            Services.Police = Add<Police.PoliceDispatch>("Police");
            Services.Police.Initialize(Services.Config, Services.Map, Services.Roads, Services.Vehicles, Services.Peds, Services.Database);

            // ---- progression ----
            Services.Economy = Add<Economy.EconomySystem>("Economy");
            Services.Economy.Initialize();
            Services.Shops = Add<Economy.ShopSystem>("Shops");
            Services.Property = Add<Economy.PropertySystem>("Property");
            Services.Property.Initialize(layout);
            Services.Garage = Add<Economy.GarageSystem>("Garage");
            Services.Dialogue = Add<Missions.DialogueSystem>("Dialogue");
            Services.Missions = Add<Missions.MissionSystem>("Missions");
            Services.RandomEvents = Add<Missions.RandomEventSystem>("RandomEvents");
            Services.Radio = Add<Audio.RadioSystem>("Radio");
            Services.Radio.Initialize(Services.Database);
            Report(0.52f, "Waking the city up");
            yield return null;

            // ---- player ----
            Vector3 spawn = FindSpawnPoint();
            SpawnPlayer(spawn);
            Report(0.58f, "Arriving in San Monica");
            yield return null;

            // ---- geometry ----
            // The player is frozen until this finishes: they exist so the streamer
            // and the population have somewhere to centre on, but the world has no
            // colliders yet and gravity would drop them out of it.
            yield return StartCoroutine(Services.Streamer.PreloadAround(spawn, p => Report(0.58f + p * 0.28f, "Streaming the world")));
            SnapPlayerToGround();
            yield return StartCoroutine(Services.Streamer.BuildDistantWorld(p => Report(0.86f + p * 0.10f, "Drawing the horizon")));

            // ---- finish ----
            Services.Quality.Initialize();
            Services.Missions.Initialize();
            Services.RandomEvents.Initialize();
            Services.Save.ApplySettings();
            Report(0.99f, "Ready");
            yield return null;

            // Second pass: loading a save moves the player after the first snap.
            SnapPlayerToGround();

            WorldReady = true;
            Services.UI.ShowLoading(false);
            SetState(GameState.Playing);
            GameEvents.Notify("Welcome to San Monica", 4f);
            Services.Dialogue.Say("ROOK: You're back. Come find me at the garage.", 6f);
            _bootRoutine = null;
        }

        private T Add<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_systems, false);
            return go.AddComponent<T>();
        }

        private void Report(float progress, string status)
        {
            Services.UI?.SetLoadingProgress(progress, status);
            GameEvents.RaiseStreamProgress(progress);
        }

        // ------------------------------------------------------------------
        private Vector3 FindSpawnPoint()
        {
            var map = Services.Map;
            var roads = Services.Roads;
            var layout = Services.Landmarks != null ? Services.Landmarks.Layout : null;

            // Start next to Rook's garage if one was generated, otherwise downtown.
            if (layout != null)
            {
                var garage = layout.NearestShop(new Vector3(map.MarigoldCenter.x, 0f, map.MarigoldCenter.y), ShopType.Mechanic, 6000f);
                if (garage != null) return garage.Position + garage.Forward * 3f + Vector3.up * 0.6f;
            }

            Vector2 fallback = map.DowntownCenter;
            int segment = roads.NearestSegment(fallback, 900f);
            if (segment >= 0)
            {
                Vector3 point = roads.SidewalkPoint(segment, true, 0.5f);
                return point + Vector3.up * 0.6f;
            }
            return new Vector3(fallback.x, map.SampleHeight(fallback.x, fallback.y) + 0.6f, fallback.y);
        }

        private void SpawnPlayer(Vector3 spawn)
        {
            var go = new GameObject("Player");
            go.transform.position = spawn;

            var rng = new Rng(Services.Config.seed ^ 0xD0D0);
            var archetype = Services.Database.Ped("citizen");
            var appearance = Characters.CharacterAppearance.Random(ref rng, archetype);
            appearance.Height = 1.83f;
            appearance.Build = 1.06f;
            appearance.Shirt = new Color(0.16f, 0.18f, 0.22f);
            appearance.Trousers = new Color(0.20f, 0.22f, 0.26f);
            appearance.Hair = new Color(0.10f, 0.08f, 0.07f);
            appearance.ShortHair = true;
            appearance.Hat = false;
            appearance.Vest = false;
            appearance.Backpack = false;

            go.AddComponent<CharacterController>();
            var player = go.AddComponent<Players.PlayerController>();
            Services.Player = player;
            player.Initialize(appearance);

            player.Frozen = true;
            gameObject.AddComponent<UI.DebugOverlay>();
            Services.Camera.SetTarget(go.transform);
            Services.Camera.SnapBehind(go.transform);
            LastSafePosition = spawn;

            var fists = Services.Database.Weapon("fists");
            if (fists != null) player.Weapons.GiveWeapon(fists, 0, true);
        }

        /// <summary>
        /// Puts the player on whatever surface is actually there - road, pavement
        /// or bare terrain - and unfreezes them. The ray starts above the terrain
        /// rather than above the player, so it still finds the ground if the
        /// player has been moved somewhere unexpected by a save or a mission.
        /// </summary>
        private void SnapPlayerToGround()
        {
            var player = Services.Player;
            if (player == null) return;

            Vector3 position = player.transform.position;
            float terrain = Services.Map != null ? Services.Map.SampleHeight(position.x, position.z) : 0f;
            float from = Mathf.Max(position.y, terrain) + 200f;

            if (Physics.Raycast(new Vector3(position.x, from, position.z), Vector3.down, out var hit, 600f,
                                GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                player.Teleport(hit.point + Vector3.up * 0.4f, player.transform.eulerAngles.y);
            else
                player.Teleport(new Vector3(position.x, terrain + 0.6f, position.z), player.transform.eulerAngles.y);

            player.Frozen = false;
        }

        // ------------------------------------------------------------------
        public void SetState(GameState state)
        {
            if (State == state) return;
            State = state;
            GameEvents.RaiseGameState(state);

            bool frozen = state == GameState.Paused || state == GameState.InMenu || state == GameState.Shopping;
            Time.timeScale = frozen ? 0f : 1f;
            Services.Audio?.SetPaused(frozen);
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Paused);
            Services.UI?.OpenPause();
        }

        public void Resume()
        {
            Services.UI?.ClosePause();
            Services.UI?.CloseShop();
            if (State == GameState.Dead || State == GameState.Busted) return;
            SetState(GameState.Playing);
        }

        public void OpenMap()
        {
            if (State == GameState.Playing) SetState(GameState.InMenu);
            Services.UI?.Map?.Open();
        }

        public void CloseMap()
        {
            if (State == GameState.InMenu) SetState(GameState.Playing);
        }

        public void QuitGame()
        {
            Services.Save?.AutoSave();
            Services.Save?.SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        public void EnterShop(ShopInstance shop)
        {
            if (shop == null || Services.Interiors == null) return;
            if (!shop.Definition.IsOpen(Services.Clock != null ? Services.Clock.Hour : 12))
            {
                GameEvents.Notify(shop.Definition.displayName + " is closed", 2.5f);
                return;
            }

            var player = Services.Player;
            if (player == null) return;
            if (player.InVehicle) player.ExitVehicle();

            Vector3 inside = Services.Interiors.EnterShop(shop);
            player.Teleport(inside, 0f);
            Services.Shops?.OpenShop(shop);
            Services.UI?.OpenShop(shop);
            SetState(GameState.Shopping);
        }

        public void InteractWithProperty(PropertyInstance property)
        {
            if (property == null) return;
            var propertySystem = Services.Property;
            if (propertySystem == null) return;

            if (!propertySystem.IsOwned(property))
            {
                propertySystem.TryBuy(property);
                return;
            }

            var player = Services.Player;
            if (player == null) return;
            if (player.InVehicle) player.ExitVehicle();
            Vector3 inside = Services.Interiors.EnterProperty(property);
            player.Teleport(inside, 0f);
            propertySystem.UseSafehouse(property);
            SetState(GameState.Playing);
        }

        public void LeaveInterior()
        {
            if (Services.Interiors == null || !Services.Interiors.IsInside)
            {
                Services.UI?.CloseShop();
                Resume();
                return;
            }
            Vector3 outside = Services.Interiors.Exit();
            float heading = Services.Interiors.ExitHeading;
            Services.Shops?.CloseShop();
            Services.UI?.CloseShop();
            Services.Player?.Teleport(outside + Vector3.up * 0.3f, heading);
            SetState(GameState.Playing);
        }

        // ------------------------------------------------------------------
        public void Busted()
        {
            if (State == GameState.Busted || State == GameState.Dead) return;
            SetState(GameState.Busted);
            long fine = Services.Economy != null ? Services.Economy.ApplyBustedPenalty() : 0;
            Services.UI?.ShowDeathScreen(true, "Bail and fines: $" + fine.ToString("N0"));
            Services.Missions?.FailMission("You were arrested");
            _respawnTimer = 3.2f;
            _respawning = true;
        }

        private void OnEnable() { GameEvents.PlayerDied += OnPlayerDied; }
        private void OnDisable() { GameEvents.PlayerDied -= OnPlayerDied; }

        private void OnPlayerDied()
        {
            if (State == GameState.Dead) return;
            SetState(GameState.Dead);
            long fee = Services.Economy != null ? Services.Economy.ApplyHospitalPenalty() : 0;
            Services.UI?.ShowDeathScreen(false, "Medical fees: $" + fee.ToString("N0"));
            _respawnTimer = 3.6f;
            _respawning = true;
        }

        private void Respawn(bool busted)
        {
            var player = Services.Player;
            if (player == null) return;

            Vector3 target = busted
                ? (Services.Landmarks != null ? Services.Landmarks.NearestPoliceStation(player.transform.position) : player.transform.position)
                : (Services.Landmarks != null ? Services.Landmarks.NearestHospital(player.transform.position) : player.transform.position);

            if (Services.Map != null) target = Services.Map.FindGroundPoint(target, 60f);
            target += Vector3.up * 0.6f;

            Services.Wanted?.ResetWanted();
            Services.Police?.StandDown();
            player.Health.ResetVitals(player.Health.MaxHealth, 0f);
            player.Teleport(target, Random.Range(0f, 360f));

            Services.UI?.HideDeathScreen();
            SetState(GameState.Playing);
            GameEvents.RaisePlayerRespawned();
            GameEvents.Notify(busted ? "Released from custody" : "Discharged from San Monica General", 3.5f);
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            if (!WorldReady) return;
            var input = Services.Input;

            if (_respawning)
            {
                _respawnTimer -= Time.unscaledDeltaTime;
                if (_respawnTimer <= 0f)
                {
                    _respawning = false;
                    Respawn(State == GameState.Busted);
                }
                return;
            }

            if (input == null) return;

            if (input.PausePressed)
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
                else if (State == GameState.InMenu) { Services.UI?.Map?.Close(); }
                else if (State == GameState.Shopping) LeaveInterior();
            }

            if (input.MapPressed)
            {
                if (State == GameState.Playing) OpenMap();
                else if (State == GameState.InMenu) Services.UI?.Map?.Close();
            }

            if (State == GameState.Playing)
            {
                if (input.RadioNextPressed) Services.Radio?.NextStation();
                var player = Services.Player;
                if (player != null && player.IsGrounded && !player.IsSwimming)
                    LastSafePosition = player.transform.position;

                _outOfWorldTimer -= Time.unscaledDeltaTime;
                if (_outOfWorldTimer <= 0f)
                {
                    _outOfWorldTimer = 0.25f;
                    RecoverIfOutOfWorld(player);
                }
            }
        }

        private float _outOfWorldTimer;

        /// <summary>
        /// Last line of defence against falling out of the world. A chunk that
        /// has not streamed in yet, a bad exit from a vehicle or a mission that
        /// moves the player somewhere unsupported all end the same way - dropping
        /// forever through empty space with no way back. Rather than leave the
        /// player stuck, put them back on the last ground they stood on.
        /// </summary>
        private void RecoverIfOutOfWorld(Players.PlayerController player)
        {
            if (player == null || player.InVehicle || !player.Health.IsAlive) return;

            Vector3 position = player.transform.position;
            float terrain = Services.Map != null ? Services.Map.SampleHeight(position.x, position.z) : 0f;
            if (position.y > terrain - 30f && position.y > -150f) return;

            Vector3 target = LastSafePosition;
            if (Services.Map != null)
                target.y = Mathf.Max(target.y, Services.Map.SampleHeight(target.x, target.z) + 0.6f);
            player.Teleport(target, player.transform.eulerAngles.y);
            Debug.LogWarning("[San Monica] Player fell out of the world at " + position + "; returned to " + target);
        }
    }
}
