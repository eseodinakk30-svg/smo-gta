using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SanMonica.Core;
using SanMonica.Economy;
using SanMonica.World;

namespace SanMonica.UI
{
    /// <summary>
    /// Builds and drives every screen: loading, HUD, touch controls, pause,
    /// settings, the map, shops and save slots. All of it is constructed in
    /// code and scales from a phone to a tablet.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public HUD Hud { get; private set; }
        public TouchControls Touch { get; private set; }
        public MapScreen Map { get; private set; }

        private RectTransform _rootRect;
        private RectTransform _loadingScreen;
        private Text _loadingTitle, _loadingStatus;
        private Image _loadingBar;
        private RectTransform _pauseScreen, _settingsScreen, _shopScreen, _saveScreen, _deathScreen, _garageScreen;
        private RectTransform _shopContent, _saveContent, _settingsContent, _garageContent;
        private Text _shopTitle, _deathTitle, _deathSubtitle;
        private ShopInstance _activeShop;
        private CanvasScaler _scaler;

        // ------------------------------------------------------------------
        public void Build()
        {
            var canvasGo = new GameObject("UICanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = GameLayers.UI;
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;

            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _rootRect = (RectTransform)canvasGo.transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventGo = new GameObject("EventSystem");
                eventGo.transform.SetParent(transform, false);
                eventGo.AddComponent<EventSystem>();
                eventGo.AddComponent<StandaloneInputModule>();
            }

            BuildLoadingScreen();

            var hudGo = new GameObject("HUDController");
            hudGo.transform.SetParent(transform, false);
            Hud = hudGo.AddComponent<HUD>();
            Hud.Build(_rootRect);

            var touchGo = new GameObject("TouchController");
            touchGo.transform.SetParent(transform, false);
            Touch = touchGo.AddComponent<TouchControls>();
            Touch.Build(_rootRect, Services.Input);

            var mapGo = new GameObject("MapController");
            mapGo.transform.SetParent(transform, false);
            Map = mapGo.AddComponent<MapScreen>();
            Map.Build(_rootRect);

            BuildPauseScreen();
            BuildGarageScreen();
            BuildSettingsScreen();
            BuildShopScreen();
            BuildSaveScreen();
            BuildDeathScreen();

            _loadingScreen.SetAsLastSibling();
        }

        private void Update()
        {
            // Touch controls only make sense while the world is being played.
            if (Touch != null && Services.Game != null)
            {
                bool playing = Services.Game.State == GameState.Playing;
                Touch.SetInteractive(playing);
            }
        }

        // ------------------------------------------------------------------
        private void BuildLoadingScreen()
        {
            _loadingScreen = UIBuilder.Rect("Loading", _rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIBuilder.Image(_loadingScreen, new Color(0.03f, 0.035f, 0.05f, 1f), false);

            var titleRect = UIBuilder.Anchored("Title", _loadingScreen, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 90f));
            _loadingTitle = UIBuilder.Label(titleRect, "SAN MONICA", 76, UIBuilder.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);

            var subtitleRect = UIBuilder.Anchored("Subtitle", _loadingScreen, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 40f));
            UIBuilder.Label(subtitleRect, "SALTWATER DEBT", 28, UIBuilder.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);

            var barBack = UIBuilder.Anchored("BarBack", _loadingScreen, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 14f));
            UIBuilder.Image(barBack, new Color(1f, 1f, 1f, 0.12f));
            var barFill = UIBuilder.Rect("BarFill", barBack, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            _loadingBar = UIBuilder.Image(barFill, UIBuilder.Accent);
            _loadingBar.type = Image.Type.Filled;
            _loadingBar.fillMethod = Image.FillMethod.Horizontal;
            _loadingBar.fillAmount = 0f;

            var statusRect = UIBuilder.Anchored("Status", _loadingScreen, new Vector2(0.5f, 0.30f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 30f));
            _loadingStatus = UIBuilder.Label(statusRect, "Booting", 22, UIBuilder.TextMuted, TextAnchor.MiddleCenter);
        }

        public void ShowLoading(bool show)
        {
            if (_loadingScreen != null) _loadingScreen.gameObject.SetActive(show);
            if (show && _loadingScreen != null) _loadingScreen.SetAsLastSibling();
        }

        public void SetLoadingProgress(float progress, string status)
        {
            if (_loadingBar != null) _loadingBar.fillAmount = Mathf.Clamp01(progress);
            if (_loadingStatus != null && !string.IsNullOrEmpty(status)) _loadingStatus.text = status;
        }

        // ------------------------------------------------------------------
        private RectTransform BuildOverlay(string name, string title, out RectTransform body)
        {
            var screen = UIBuilder.Rect(name, _rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIBuilder.Image(screen, new Color(0.03f, 0.04f, 0.06f, 0.94f), false);

            var titleRect = UIBuilder.Anchored("Title", screen, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1200f, 52f));
            UIBuilder.Label(titleRect, title, 40, UIBuilder.TextPrimary, TextAnchor.UpperCenter, FontStyle.Bold);

            body = UIBuilder.Rect("Body", screen, new Vector2(0.18f, 0.10f), new Vector2(0.82f, 0.84f), Vector2.zero, Vector2.zero);
            screen.gameObject.SetActive(false);
            return screen;
        }

        private void BuildPauseScreen()
        {
            _pauseScreen = BuildOverlay("PauseScreen", "PAUSED", out var body);

            string[] labels = { "RESUME", "MAP", "GARAGE", "SETTINGS", "SAVE GAME", "LOAD GAME", "QUIT TO DESKTOP" };
            System.Action[] actions =
            {
                () => Services.Game?.Resume(),
                () => { ClosePause(); Services.Game?.OpenMap(); },
                OpenGarage,
                OpenSettings,
                () => OpenSaveLoad(true),
                () => OpenSaveLoad(false),
                () => Services.Game?.QuitGame()
            };

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var rect = UIBuilder.Anchored("Btn" + i, body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * 74f - 20f), new Vector2(460f, 62f));
                UIBuilder.Button(rect, labels[i], new Color(0.14f, 0.16f, 0.21f, 0.98f), UIBuilder.TextPrimary, () => actions[index]());
            }

            var statsRect = UIBuilder.Anchored("Stats", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(900f, 130f));
            var stats = UIBuilder.LabelWrapped(statsRect, "", 20, UIBuilder.TextMuted, TextAnchor.UpperCenter);
            stats.gameObject.AddComponent<PauseStats>().Bind(stats);
        }

        public void OpenPause()
        {
            if (_pauseScreen != null) { _pauseScreen.gameObject.SetActive(true); _pauseScreen.SetAsLastSibling(); }
        }

        public void ClosePause()
        {
            if (_pauseScreen != null) _pauseScreen.gameObject.SetActive(false);
            if (_settingsScreen != null) _settingsScreen.gameObject.SetActive(false);
            if (_saveScreen != null) _saveScreen.gameObject.SetActive(false);
            if (_garageScreen != null) _garageScreen.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        private void BuildGarageScreen()
        {
            _garageScreen = BuildOverlay("GarageScreen", "YOUR COLLECTION", out var body);
            var scrollRect = UIBuilder.Rect("Scroll", body, Vector2.zero, Vector2.one, new Vector2(0f, 130f), Vector2.zero);
            UIBuilder.ScrollView(scrollRect, out _garageContent);

            var claimRect = UIBuilder.Anchored("Claim", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(520f, 54f));
            UIBuilder.Button(claimRect, "CLAIM THE VEHICLE I AM DRIVING", new Color(0.18f, 0.32f, 0.24f, 0.98f), UIBuilder.TextPrimary,
                () => { if (Services.Garage != null && Services.Garage.ClaimCurrentVehicle()) OpenGarage(); }, 20);

            var closeRect = UIBuilder.Anchored("Close", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(320f, 56f));
            UIBuilder.Button(closeRect, "BACK", new Color(0.20f, 0.22f, 0.28f, 0.98f), UIBuilder.TextPrimary,
                () => { if (_garageScreen != null) _garageScreen.gameObject.SetActive(false); });
        }

        public void OpenGarage()
        {
            if (_garageScreen == null || _garageContent == null) return;
            for (int i = _garageContent.childCount - 1; i >= 0; i--) Destroy(_garageContent.GetChild(i).gameObject);

            var garage = Services.Garage;
            var database = Services.Database;
            float cursor = 8f;

            if (garage == null || garage.Collection.Count == 0)
            {
                var empty = UIBuilder.Anchored("Empty", _garageContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -cursor), new Vector2(900f, 60f));
                UIBuilder.Label(empty, "You do not own any vehicles yet. Buy one from a dealership, or claim the car you are driving.",
                    20, UIBuilder.TextMuted, TextAnchor.MiddleCenter);
                cursor += 70f;
            }
            else
            {
                for (int i = 0; i < garage.Collection.Count; i++)
                {
                    var entry = garage.Collection[i];
                    var definition = database?.Vehicle(entry.DefinitionId);
                    string title = definition != null ? definition.displayName : entry.DefinitionId;
                    string detail = "Engine " + entry.Engine + "  Brakes " + entry.Brakes + "  Grip " + entry.Grip + "  Armour " + entry.Armour;

                    var row = UIBuilder.Anchored("Vehicle" + i, _garageContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -cursor), new Vector2(900f, 68f));
                    UIBuilder.Image(row, new Color(0.10f, 0.12f, 0.16f, 0.95f));

                    var nameRect = UIBuilder.Anchored("Name", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 13f), new Vector2(560f, 28f));
                    UIBuilder.Label(nameRect, title, 24, UIBuilder.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
                    var detailRect = UIBuilder.Anchored("Detail", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -13f), new Vector2(560f, 22f));
                    UIBuilder.Label(detailRect, detail, 17, UIBuilder.TextMuted);

                    var deliverRect = UIBuilder.Anchored("Deliver", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(250f, 48f));
                    var captured = entry;
                    UIBuilder.Button(deliverRect, "DELIVER", new Color(0.20f, 0.26f, 0.38f, 0.98f), UIBuilder.TextPrimary,
                        () =>
                        {
                            garage.Deliver(captured, Services.PlayerPosition);
                            if (_garageScreen != null) _garageScreen.gameObject.SetActive(false);
                            ClosePause();
                            Services.Game?.Resume();
                        }, 20);

                    cursor += 76f;
                }
            }

            _garageContent.sizeDelta = new Vector2(0f, cursor + 20f);
            _garageScreen.gameObject.SetActive(true);
            _garageScreen.SetAsLastSibling();
        }

        // ------------------------------------------------------------------
        private void BuildSettingsScreen()
        {
            _settingsScreen = BuildOverlay("SettingsScreen", "SETTINGS", out var body);
            var scrollRect = UIBuilder.Rect("Scroll", body, Vector2.zero, Vector2.one, new Vector2(0f, 70f), Vector2.zero);
            UIBuilder.ScrollView(scrollRect, out _settingsContent);

            var closeRect = UIBuilder.Anchored("Close", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(320f, 56f));
            UIBuilder.Button(closeRect, "BACK", new Color(0.20f, 0.22f, 0.28f, 0.98f), UIBuilder.TextPrimary,
                () => { if (_settingsScreen != null) _settingsScreen.gameObject.SetActive(false); Services.Save?.SaveSettings(); });
        }

        private float _settingsCursor;

        private RectTransform SettingsRow(string label, float height = 56f)
        {
            var row = UIBuilder.Anchored("Row_" + label, _settingsContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -_settingsCursor), new Vector2(880f, height));
            _settingsCursor += height + 8f;
            _settingsContent.sizeDelta = new Vector2(0f, _settingsCursor + 20f);

            var labelRect = UIBuilder.Anchored("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(340f, height));
            UIBuilder.Label(labelRect, label, 22, UIBuilder.TextPrimary);
            return row;
        }

        private void PopulateSettings()
        {
            if (_settingsContent == null) return;
            for (int i = _settingsContent.childCount - 1; i >= 0; i--) Destroy(_settingsContent.GetChild(i).gameObject);
            _settingsCursor = 10f;

            var quality = Services.Quality;
            var audio = Services.Audio;
            var input = Services.Input;
            var camera = Services.Camera;

            // ---- Graphics ----
            SectionHeader("GRAPHICS");
            if (quality != null)
            {
                var row = SettingsRow("Quality preset");
                string[] presets = { "LOW", "MEDIUM", "HIGH", "ULTRA" };
                for (int i = 0; i < presets.Length; i++)
                {
                    int index = i;
                    var buttonRect = UIBuilder.Anchored("Q" + i, row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-i * 128f - 8f, 0f), new Vector2(120f, 44f));
                    UIBuilder.Button(buttonRect, presets[presets.Length - 1 - i],
                        new Color(0.16f, 0.18f, 0.24f, 0.98f), UIBuilder.TextPrimary,
                        () => { quality.ApplyPreset((SanMonica.Optimization.QualityPreset)(presets.Length - 1 - index)); PopulateSettings(); }, 18);
                }

                var autoRow = SettingsRow("Auto quality");
                UIBuilder.Toggle(UIBuilder.Anchored("Toggle", autoRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(320f, 44f)),
                    "Adapt to keep the frame rate", quality.AutoQuality, v => quality.AutoQuality = v);

                var scaleRow = SettingsRow("Render scale");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", scaleRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.5f, 1.2f, quality.RenderScale, v => quality.SetRenderScale(v));

                var drawRow = SettingsRow("Draw distance");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", drawRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.5f, 2f, quality.DrawDistanceScale, v => quality.SetDrawDistance(v));

                var pedRow = SettingsRow("Pedestrian density");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", pedRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.1f, 2f, quality.PedDensity, v => quality.SetPedDensity(v));

                var trafficRow = SettingsRow("Traffic density");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", trafficRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.1f, 2f, quality.TrafficDensity, v => quality.SetTrafficDensity(v));

                var fpsRow = SettingsRow("Target frame rate");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", fpsRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    30f, 120f, quality.TargetFrameRate, v => quality.SetTargetFrameRate(Mathf.RoundToInt(v)));
            }

            // ---- Audio ----
            SectionHeader("AUDIO");
            if (audio != null)
            {
                AudioSlider("Master volume", audio.MasterVolume, v => { audio.MasterVolume = v; audio.ApplyVolumes(); });
                AudioSlider("Music", audio.MusicVolume, v => audio.MusicVolume = v);
                AudioSlider("Effects", audio.SfxVolume, v => audio.SfxVolume = v);
                AudioSlider("Ambience", audio.AmbienceVolume, v => audio.AmbienceVolume = v);
                AudioSlider("Interface", audio.UiVolume, v => audio.UiVolume = v);
            }

            // ---- Controls ----
            SectionHeader("CONTROLS");
            if (input != null)
            {
                var sensitivityRow = SettingsRow("Look sensitivity");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", sensitivityRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.3f, 3f, input.LookSensitivity, v => input.LookSensitivity = v);

                var aimRow = SettingsRow("Aim sensitivity");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", aimRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.2f, 1.5f, input.AimSensitivityMultiplier, v => input.AimSensitivityMultiplier = v);

                var invertRow = SettingsRow("Invert vertical look");
                UIBuilder.Toggle(UIBuilder.Anchored("Toggle", invertRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(320f, 44f)),
                    "Inverted", input.InvertY, v => input.InvertY = v);
            }

            if (Touch != null)
            {
                var sizeRow = SettingsRow("Button size");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", sizeRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.6f, 1.8f, Touch.Scale, v => { Touch.Scale = v; Touch.ApplyAppearance(); });

                var opacityRow = SettingsRow("Button opacity");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", opacityRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    0.15f, 1f, Touch.Opacity, v => { Touch.Opacity = v; Touch.ApplyAppearance(); });

                var touchRow = SettingsRow("On-screen controls");
                UIBuilder.Toggle(UIBuilder.Anchored("Toggle", touchRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(320f, 44f)),
                    "Visible", Touch.Enabled, v => { Touch.Enabled = v; Touch.ApplyAppearance(); });

                var layoutRow = SettingsRow("Custom layout");
                UIBuilder.Button(UIBuilder.Anchored("Edit", layoutRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(300f, 44f)),
                    Touch.EditMode ? "FINISH EDITING" : "MOVE BUTTONS",
                    new Color(0.18f, 0.20f, 0.26f, 0.98f), UIBuilder.TextPrimary,
                    () => { Touch.BeginLayoutEdit(!Touch.EditMode); PopulateSettings(); }, 18);
            }

            if (camera != null)
            {
                var fovRow = SettingsRow("Field of view");
                UIBuilder.Slider(UIBuilder.Anchored("Slider", fovRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                    45f, 85f, camera.BaseFov, v => camera.BaseFov = v);
            }
        }

        private void SectionHeader(string title)
        {
            var row = UIBuilder.Anchored("Header_" + title, _settingsContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -_settingsCursor), new Vector2(880f, 40f));
            UIBuilder.Label(row, title, 24, UIBuilder.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            _settingsCursor += 48f;
        }

        private void AudioSlider(string label, float value, System.Action<float> onChanged)
        {
            var row = SettingsRow(label);
            UIBuilder.Slider(UIBuilder.Anchored("Slider", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(460f, 44f)),
                0f, 1f, value, onChanged);
        }

        public void OpenSettings()
        {
            PopulateSettings();
            if (_settingsScreen != null) { _settingsScreen.gameObject.SetActive(true); _settingsScreen.SetAsLastSibling(); }
        }

        // ------------------------------------------------------------------
        private void BuildShopScreen()
        {
            _shopScreen = BuildOverlay("ShopScreen", "STORE", out var body);
            _shopTitle = _shopScreen.GetComponentInChildren<Text>();

            var scrollRect = UIBuilder.Rect("Scroll", body, Vector2.zero, Vector2.one, new Vector2(0f, 70f), Vector2.zero);
            UIBuilder.ScrollView(scrollRect, out _shopContent);

            var closeRect = UIBuilder.Anchored("Close", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(320f, 56f));
            UIBuilder.Button(closeRect, "LEAVE", new Color(0.20f, 0.22f, 0.28f, 0.98f), UIBuilder.TextPrimary, () => Services.Game?.LeaveInterior());
        }

        public void OpenShop(ShopInstance shop)
        {
            _activeShop = shop;
            if (_shopScreen == null) return;
            if (_shopTitle != null) _shopTitle.text = shop != null ? shop.Definition.displayName.ToUpperInvariant() : "STORE";
            PopulateShop();
            _shopScreen.gameObject.SetActive(true);
            _shopScreen.SetAsLastSibling();
        }

        public void CloseShop()
        {
            _activeShop = null;
            if (_shopScreen != null) _shopScreen.gameObject.SetActive(false);
        }

        private void PopulateShop()
        {
            if (_shopContent == null || Services.Shops == null) return;
            for (int i = _shopContent.childCount - 1; i >= 0; i--) Destroy(_shopContent.GetChild(i).gameObject);

            var offers = Services.Shops.BuildCatalogue(_activeShop);
            float cursor = 8f;
            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                var row = UIBuilder.Anchored("Offer" + i, _shopContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -cursor), new Vector2(900f, 64f));
                UIBuilder.Image(row, new Color(0.10f, 0.12f, 0.16f, 0.95f));

                var nameRect = UIBuilder.Anchored("Name", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 12f), new Vector2(520f, 28f));
                UIBuilder.Label(nameRect, offer.Name, 24, UIBuilder.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);

                var detailRect = UIBuilder.Anchored("Detail", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -14f), new Vector2(520f, 22f));
                UIBuilder.Label(detailRect, offer.Detail, 17, UIBuilder.TextMuted);

                var buyRect = UIBuilder.Anchored("Buy", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(230f, 46f));
                var captured = offer;
                UIBuilder.Button(buyRect, "$" + offer.Price.ToString("N0"),
                    new Color(0.18f, 0.34f, 0.24f, 0.98f), UIBuilder.TextPrimary,
                    () => { if (Services.Shops.Purchase(in captured)) PopulateShop(); }, 20);

                cursor += 72f;
            }
            _shopContent.sizeDelta = new Vector2(0f, cursor + 20f);
        }

        // ------------------------------------------------------------------
        private void BuildSaveScreen()
        {
            _saveScreen = BuildOverlay("SaveScreen", "SAVE GAME", out var body);
            var scrollRect = UIBuilder.Rect("Scroll", body, Vector2.zero, Vector2.one, new Vector2(0f, 70f), Vector2.zero);
            UIBuilder.ScrollView(scrollRect, out _saveContent);

            var closeRect = UIBuilder.Anchored("Close", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(320f, 56f));
            UIBuilder.Button(closeRect, "BACK", new Color(0.20f, 0.22f, 0.28f, 0.98f), UIBuilder.TextPrimary,
                () => { if (_saveScreen != null) _saveScreen.gameObject.SetActive(false); });
        }

        public void OpenSaveLoad(bool saving)
        {
            if (_saveScreen == null || Services.Save == null) return;
            var title = _saveScreen.GetComponentInChildren<Text>();
            if (title != null) title.text = saving ? "SAVE GAME" : "LOAD GAME";

            for (int i = _saveContent.childCount - 1; i >= 0; i--) Destroy(_saveContent.GetChild(i).gameObject);

            float cursor = 8f;
            for (int slot = 0; slot < SanMonica.Saves.SaveSystem.SlotCount; slot++)
            {
                int index = slot;
                string summary = Services.Save.DescribeSlot(slot);
                var row = UIBuilder.Anchored("Slot" + slot, _saveContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -cursor), new Vector2(900f, 78f));
                UIBuilder.Image(row, new Color(0.10f, 0.12f, 0.16f, 0.95f));

                var labelRect = UIBuilder.Anchored("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(560f, 60f));
                UIBuilder.LabelWrapped(labelRect, "Slot " + (slot + 1) + "\n" + summary, 20, UIBuilder.TextPrimary, TextAnchor.MiddleLeft);

                var actionRect = UIBuilder.Anchored("Action", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(230f, 52f));
                UIBuilder.Button(actionRect, saving ? "SAVE" : "LOAD",
                    saving ? new Color(0.18f, 0.32f, 0.24f, 0.98f) : new Color(0.20f, 0.26f, 0.38f, 0.98f),
                    UIBuilder.TextPrimary,
                    () =>
                    {
                        if (saving) { Services.Save.SaveToSlot(index); OpenSaveLoad(true); }
                        else { _saveScreen.gameObject.SetActive(false); ClosePause(); Services.Save.LoadFromSlot(index); }
                    }, 20);

                cursor += 86f;
            }
            _saveContent.sizeDelta = new Vector2(0f, cursor + 20f);
            _saveScreen.gameObject.SetActive(true);
            _saveScreen.SetAsLastSibling();
        }

        // ------------------------------------------------------------------
        private void BuildDeathScreen()
        {
            _deathScreen = UIBuilder.Rect("DeathScreen", _rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIBuilder.Image(_deathScreen, new Color(0.12f, 0.02f, 0.02f, 0.72f), false);

            var titleRect = UIBuilder.Anchored("Title", _deathScreen, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 90f));
            _deathTitle = UIBuilder.Label(titleRect, "WASTED", 84, new Color(0.92f, 0.24f, 0.20f), TextAnchor.MiddleCenter, FontStyle.Bold);

            var subtitleRect = UIBuilder.Anchored("Subtitle", _deathScreen, new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 40f));
            _deathSubtitle = UIBuilder.Label(subtitleRect, "", 26, UIBuilder.TextMuted, TextAnchor.MiddleCenter);

            _deathScreen.gameObject.SetActive(false);
        }

        public void ShowDeathScreen(bool busted, string subtitle)
        {
            if (_deathScreen == null) return;
            _deathTitle.text = busted ? "BUSTED" : "WASTED";
            _deathTitle.color = busted ? new Color(0.35f, 0.60f, 0.95f) : new Color(0.92f, 0.24f, 0.20f);
            _deathSubtitle.text = subtitle;
            _deathScreen.gameObject.SetActive(true);
            _deathScreen.SetAsLastSibling();
        }

        public void HideDeathScreen()
        {
            if (_deathScreen != null) _deathScreen.gameObject.SetActive(false);
        }

        public void SetHudVisible(bool visible)
        {
            Hud?.SetVisible(visible);
        }
    }

    /// <summary>Live statistics shown on the pause screen.</summary>
    public class PauseStats : MonoBehaviour
    {
        private Text _label;
        private float _timer;

        public void Bind(Text label) { _label = label; }

        private void OnEnable() { _timer = 0f; }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f || _label == null) return;
            _timer = 0.5f;

            var missions = Services.Missions;
            var economy = Services.Economy;
            var perf = Services.Perf;
            var property = Services.Property;
            var garage = Services.Garage;

            _label.text =
                "Chapter " + (missions != null ? missions.Chapter : 1) +
                "   •   Missions completed " + (missions != null ? missions.Completed.Count : 0) +
                "   •   Respect " + (missions != null ? missions.Respect : 0) + "\n" +
                "Money $" + (economy != null ? economy.Money.ToString("N0") : "0") +
                "   •   Property " + (property != null ? property.Owned.Count : 0) +
                "   •   Vehicles " + (garage != null ? garage.Collection.Count : 0) + "\n" +
                (perf != null ? perf.Summary : "");
        }
    }
}
