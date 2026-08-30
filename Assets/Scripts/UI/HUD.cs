using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SanMonica.Core;

namespace SanMonica.UI
{
    /// <summary>
    /// The in-game heads up display: vitals, money, clock, wanted level, weapon
    /// and ammunition, the current objective, notifications, subtitles, the
    /// radio banner and the minimap.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        private RectTransform _root;
        private Image _healthFill, _armourFill;
        private Text _moneyLabel, _clockLabel, _weaponLabel, _ammoLabel, _objectiveLabel, _timerLabel;
        private Text _subtitleLabel, _promptLabel, _radioLabel, _districtLabel, _speedLabel;
        private RectTransform _starsRoot, _notificationRoot, _armourRow;
        private readonly List<Image> _stars = new List<Image>(5);
        private readonly List<Text> _notifications = new List<Text>(4);
        private readonly List<float> _notificationTimers = new List<float>(4);
        private Minimap _minimap;
        private CanvasGroup _group;
        private float _promptTimer;

        public Minimap MinimapView => _minimap;

        public void Build(RectTransform parent)
        {
            _root = UIBuilder.Rect("HUD", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            BuildVitals();
            BuildStatus();
            BuildWeapon();
            BuildObjective();
            BuildMessages();

            var minimapGo = new GameObject("MinimapController");
            minimapGo.transform.SetParent(transform, false);
            _minimap = minimapGo.AddComponent<Minimap>();
            _minimap.Build(_root);

            GameEvents.Notification += OnNotification;
            GameEvents.SubtitleRequested += OnSubtitle;
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.WantedLevelChanged += OnWantedChanged;
        }

        private void OnDestroy()
        {
            GameEvents.Notification -= OnNotification;
            GameEvents.SubtitleRequested -= OnSubtitle;
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.WantedLevelChanged -= OnWantedChanged;
        }

        // ------------------------------------------------------------------
        private void BuildVitals()
        {
            var panel = UIBuilder.Anchored("Vitals", _root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(260f, 56f));

            var healthRow = UIBuilder.Anchored("HealthRow", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(260f, 22f));
            UIBuilder.Image(healthRow, new Color(0f, 0f, 0f, 0.45f));
            var healthFillRect = UIBuilder.Rect("Fill", healthRow, Vector2.zero, new Vector2(1f, 1f), new Vector2(2f, 2f), new Vector2(-2f, -2f));
            _healthFill = UIBuilder.Image(healthFillRect, UIBuilder.Good);
            _healthFill.type = Image.Type.Filled;
            _healthFill.fillMethod = Image.FillMethod.Horizontal;

            _armourRow = UIBuilder.Anchored("ArmourRow", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -28f), new Vector2(260f, 16f));
            UIBuilder.Image(_armourRow, new Color(0f, 0f, 0f, 0.45f));
            var armourFillRect = UIBuilder.Rect("Fill", _armourRow, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            _armourFill = UIBuilder.Image(armourFillRect, UIBuilder.AccentCool);
            _armourFill.type = Image.Type.Filled;
            _armourFill.fillMethod = Image.FillMethod.Horizontal;
        }

        private void BuildStatus()
        {
            var moneyRect = UIBuilder.Anchored("Money", _root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(240f, 34f));
            _moneyLabel = UIBuilder.Label(moneyRect, "$0", 30, UIBuilder.Good, TextAnchor.UpperRight, FontStyle.Bold);

            var clockRect = UIBuilder.Anchored("Clock", _root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -54f), new Vector2(240f, 24f));
            _clockLabel = UIBuilder.Label(clockRect, "08:30", 20, UIBuilder.TextMuted, TextAnchor.UpperRight);

            var districtRect = UIBuilder.Anchored("District", _root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -78f), new Vector2(320f, 22f));
            _districtLabel = UIBuilder.Label(districtRect, "", 18, UIBuilder.TextMuted, TextAnchor.UpperRight);

            _starsRoot = UIBuilder.Anchored("Wanted", _root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -106f), new Vector2(190f, 30f));
            for (int i = 0; i < 5; i++)
            {
                var star = UIBuilder.Anchored("Star" + i, _starsRoot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-i * 34f, 0f), new Vector2(26f, 26f));
                var image = UIBuilder.Circle(star, new Color(1f, 1f, 1f, 0.14f));
                _stars.Add(image);
            }
        }

        private void BuildWeapon()
        {
            var weaponRect = UIBuilder.Anchored("Weapon", _root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 66f), new Vector2(300f, 26f));
            _weaponLabel = UIBuilder.Label(weaponRect, "", 20, UIBuilder.TextPrimary, TextAnchor.LowerRight, FontStyle.Bold);

            var ammoRect = UIBuilder.Anchored("Ammo", _root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 30f), new Vector2(300f, 34f));
            _ammoLabel = UIBuilder.Label(ammoRect, "", 28, UIBuilder.Accent, TextAnchor.LowerRight, FontStyle.Bold);

            var speedRect = UIBuilder.Anchored("Speed", _root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 104f), new Vector2(300f, 26f));
            _speedLabel = UIBuilder.Label(speedRect, "", 22, UIBuilder.TextMuted, TextAnchor.LowerRight);
        }

        private void BuildObjective()
        {
            var panel = UIBuilder.Anchored("Objective", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(620f, 30f));
            _objectiveLabel = UIBuilder.Label(panel, "", 22, UIBuilder.Accent, TextAnchor.UpperCenter, FontStyle.Bold);

            var timerRect = UIBuilder.Anchored("Timer", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(300f, 26f));
            _timerLabel = UIBuilder.Label(timerRect, "", 22, UIBuilder.Danger, TextAnchor.UpperCenter, FontStyle.Bold);
        }

        private void BuildMessages()
        {
            _notificationRoot = UIBuilder.Anchored("Notifications", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(760f, 140f));

            var subtitleRect = UIBuilder.Anchored("Subtitle", _root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(900f, 64f));
            _subtitleLabel = UIBuilder.LabelWrapped(subtitleRect, "", 24, UIBuilder.TextPrimary, TextAnchor.LowerCenter);

            var promptRect = UIBuilder.Anchored("Prompt", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(640f, 30f));
            _promptLabel = UIBuilder.Label(promptRect, "", 22, UIBuilder.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);

            var radioRect = UIBuilder.Anchored("Radio", _root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -84f), new Vector2(520f, 24f));
            _radioLabel = UIBuilder.Label(radioRect, "", 18, UIBuilder.AccentCool, TextAnchor.UpperLeft);
        }

        // ------------------------------------------------------------------
        private void OnNotification(string message, float duration)
        {
            var label = GetNotificationSlot();
            label.text = message;
            label.gameObject.SetActive(true);
            int index = _notifications.IndexOf(label);
            if (index >= 0) _notificationTimers[index] = duration;
        }

        private Text GetNotificationSlot()
        {
            for (int i = 0; i < _notifications.Count; i++)
                if (!_notifications[i].gameObject.activeSelf) return _notifications[i];

            if (_notifications.Count < 5)
            {
                var rect = UIBuilder.Anchored("Note" + _notifications.Count, _notificationRoot,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_notifications.Count * 28f), new Vector2(760f, 26f));
                var label = UIBuilder.Label(rect, "", 20, UIBuilder.TextPrimary, TextAnchor.UpperCenter);
                _notifications.Add(label);
                _notificationTimers.Add(0f);
                return label;
            }

            // Recycle the oldest.
            int oldest = 0;
            for (int i = 1; i < _notificationTimers.Count; i++)
                if (_notificationTimers[i] < _notificationTimers[oldest]) oldest = i;
            return _notifications[oldest];
        }

        private void OnSubtitle(string line)
        {
            if (_subtitleLabel != null) _subtitleLabel.text = line ?? "";
        }

        private void OnMoneyChanged(long balance, long delta)
        {
            if (_moneyLabel != null) _moneyLabel.text = "$" + balance.ToString("N0");
        }

        private void OnWantedChanged(int level)
        {
            for (int i = 0; i < _stars.Count; i++)
                _stars[i].color = i < level ? UIBuilder.Danger : new Color(1f, 1f, 1f, 0.12f);
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _notifications.Count; i++)
            {
                if (!_notifications[i].gameObject.activeSelf) continue;
                _notificationTimers[i] -= dt;
                if (_notificationTimers[i] <= 0f) _notifications[i].gameObject.SetActive(false);
                else UIBuilder.SetAlpha(_notifications[i], Mathf.Clamp01(_notificationTimers[i]));
            }

            var player = Services.Player;
            if (player != null && player.Health != null)
            {
                _healthFill.fillAmount = Mathf.Clamp01(player.Health.Health / Mathf.Max(1f, player.Health.MaxHealth));
                float armour = Mathf.Clamp01(player.Health.Armour / Mathf.Max(1f, player.Health.MaxArmour));
                _armourFill.fillAmount = armour;
                _armourRow.gameObject.SetActive(armour > 0.001f);

                if (player.Weapons != null)
                {
                    var definition = player.Weapons.CurrentDefinition;
                    if (definition != null && definition.IsGun)
                    {
                        _weaponLabel.text = definition.displayName;
                        _ammoLabel.text = player.Weapons.MagazineAmmo + " / " + player.Weapons.ReserveAmmo;
                    }
                    else
                    {
                        _weaponLabel.text = definition != null ? definition.displayName : "";
                        _ammoLabel.text = "";
                    }
                }

                _promptTimer -= dt;
                if (!string.IsNullOrEmpty(player.NearbyPrompt))
                {
                    _promptLabel.text = player.NearbyPrompt;
                    _promptTimer = 0.4f;
                }
                else if (_promptTimer <= 0f) _promptLabel.text = "";

                if (player.InVehicle && player.CurrentVehicle != null)
                {
                    var driven = player.CurrentVehicle;
                    string gear = driven.Gear < 0 ? "" :
                        (driven.Gear == 0 ? "   R" : "   " + driven.Gear + "/" + driven.GearCount);
                    _speedLabel.text = Mathf.RoundToInt(driven.AbsSpeedKph) + " km/h" + gear;
                }
                else _speedLabel.text = "";
            }

            var clock = Services.Clock;
            if (clock != null && _clockLabel != null) _clockLabel.text = "Day " + clock.Day + "   " + clock.ClockText;

            if (_districtLabel != null && Services.Map != null)
                _districtLabel.text = Services.Map.DistrictName(Services.PlayerPosition);

            var missions = Services.Missions;
            if (missions != null)
            {
                _objectiveLabel.text = missions.Active != null ? missions.CurrentObjectiveText : "";
                if (missions.Active != null && missions.ObjectiveTimeRemaining > 0.01f)
                {
                    int seconds = Mathf.CeilToInt(missions.ObjectiveTimeRemaining);
                    _timerLabel.text = (seconds / 60).ToString("0") + ":" + (seconds % 60).ToString("00");
                }
                else _timerLabel.text = "";
            }

            var radio = Services.Radio;
            if (radio != null && _radioLabel != null)
                _radioLabel.text = radio.IsOn && player != null && player.InVehicle
                    ? radio.StationName + "   " + radio.NowPlaying : "";
        }

        public void SetVisible(bool visible)
        {
            if (_group != null) _group.alpha = visible ? 1f : 0f;
        }
    }
}
