using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SanMonica.Core;
using SanMonica.Players;

namespace SanMonica.UI
{
    /// <summary>
    /// The on-screen controls. A floating movement stick on the left, a look and
    /// aim surface on the right, and context sensitive action buttons that swap
    /// between the on-foot and driving layouts. Size, opacity, position and
    /// sensitivity are all adjustable and saved.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        [Header("Appearance")]
        [Range(0.6f, 1.8f)] public float Scale = 1f;
        [Range(0.15f, 1f)] public float Opacity = 0.55f;
        public bool Enabled = DefaultEnabled;

        /// <summary>
        /// On a phone the on-screen stick and buttons are the only way to play;
        /// on a desktop build they would sit on top of the picture for a player
        /// who is using the keyboard. A Windows machine with a touchscreen still
        /// gets them, and either way the settings screen can override this.
        /// </summary>
        public static bool DefaultEnabled => Application.isMobilePlatform || Input.touchSupported;
        public bool EditMode;

        private RectTransform _root;
        private RectTransform _footGroup;
        private RectTransform _vehicleGroup;
        private TouchStick _stick;
        private TouchLookArea _lookArea;
        private readonly List<TouchButton> _buttons = new List<TouchButton>(20);
        private InputHub _input;
        private bool _lastInVehicle;

        public void Build(RectTransform parent, InputHub input)
        {
            _input = input;
            _root = UIBuilder.Rect("TouchControls", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Look/aim surface covers the right half and sits behind the buttons.
            var lookRect = UIBuilder.Rect("LookArea", _root, new Vector2(0.38f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var lookImage = UIBuilder.Image(lookRect, new Color(1f, 1f, 1f, 0f), false);
            lookImage.raycastTarget = true;
            _lookArea = lookRect.gameObject.AddComponent<TouchLookArea>();
            _lookArea.Bind(input);

            // Movement stick on the left.
            var stickRect = UIBuilder.Rect("StickArea", _root, new Vector2(0f, 0f), new Vector2(0.38f, 0.72f), Vector2.zero, Vector2.zero);
            var stickImage = UIBuilder.Image(stickRect, new Color(1f, 1f, 1f, 0f), false);
            stickImage.raycastTarget = true;
            _stick = stickRect.gameObject.AddComponent<TouchStick>();
            _stick.Build(input);

            _footGroup = UIBuilder.Rect("OnFoot", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vehicleGroup = UIBuilder.Rect("InVehicle", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            BuildFootButtons();
            BuildVehicleButtons();
            BuildSharedButtons();
            ApplyAppearance();
            SetVehicleMode(false);
        }

        private TouchButton AddButton(RectTransform group, string id, string label, Vector2 anchoredPosition, float size, Color colour, bool hold)
        {
            var rect = UIBuilder.Anchored("Btn_" + id, group, new Vector2(1f, 0f), new Vector2(1f, 0f), anchoredPosition, new Vector2(size, size));
            var image = UIBuilder.Circle(rect, colour);
            var text = UIBuilder.Rect("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIBuilder.Label(text, label, Mathf.RoundToInt(size * 0.24f), UIBuilder.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);

            var button = rect.gameObject.AddComponent<TouchButton>();
            button.Bind(_input, id, hold, image, this);
            _buttons.Add(button);
            return button;
        }

        private void BuildFootButtons()
        {
            AddButton(_footGroup, "fire", "FIRE", new Vector2(-110f, 120f), 116f, new Color(0.85f, 0.24f, 0.20f, 1f), true);
            AddButton(_footGroup, "aim", "AIM", new Vector2(-240f, 180f), 92f, new Color(0.25f, 0.45f, 0.75f, 1f), true);
            AddButton(_footGroup, "jump", "JUMP", new Vector2(-120f, 250f), 84f, new Color(0.30f, 0.34f, 0.40f, 1f), false);
            AddButton(_footGroup, "sprint", "RUN", new Vector2(-250f, 90f), 84f, new Color(0.30f, 0.34f, 0.40f, 1f), true);
            AddButton(_footGroup, "crouch", "DUCK", new Vector2(-350f, 150f), 74f, new Color(0.28f, 0.30f, 0.36f, 1f), true);
            AddButton(_footGroup, "reload", "RELD", new Vector2(-40f, 240f), 74f, new Color(0.34f, 0.30f, 0.24f, 1f), false);
            AddButton(_footGroup, "melee", "HIT", new Vector2(-40f, 330f), 74f, new Color(0.40f, 0.28f, 0.24f, 1f), false);
        }

        private void BuildVehicleButtons()
        {
            AddButton(_vehicleGroup, "throttle", "GAS", new Vector2(-110f, 120f), 124f, new Color(0.24f, 0.62f, 0.34f, 1f), true);
            AddButton(_vehicleGroup, "brake", "BRK", new Vector2(-250f, 110f), 104f, new Color(0.75f, 0.28f, 0.22f, 1f), true);
            AddButton(_vehicleGroup, "handbrake", "HAND", new Vector2(-120f, 262f), 88f, new Color(0.35f, 0.35f, 0.42f, 1f), true);
            AddButton(_vehicleGroup, "horn", "HORN", new Vector2(-256f, 240f), 78f, new Color(0.36f, 0.40f, 0.48f, 1f), true);
            AddButton(_vehicleGroup, "fire", "FIRE", new Vector2(-360f, 150f), 84f, new Color(0.85f, 0.24f, 0.20f, 1f), true);
            AddButton(_vehicleGroup, "radio", "RADIO", new Vector2(-40f, 330f), 72f, new Color(0.30f, 0.36f, 0.48f, 1f), false);
        }

        private void BuildSharedButtons()
        {
            AddButton(_root, "entervehicle", "ENTER", new Vector2(-40f, 40f), 82f, new Color(0.32f, 0.36f, 0.44f, 1f), false);
            AddButton(_root, "interact", "USE", new Vector2(-150f, 30f), 74f, new Color(0.32f, 0.44f, 0.36f, 1f), false);
            AddButton(_root, "nextweapon", "WPN", new Vector2(-250f, 20f), 66f, new Color(0.34f, 0.32f, 0.40f, 1f), false);
            AddButton(_root, "camera", "CAM", new Vector2(-345f, 22f), 62f, new Color(0.30f, 0.32f, 0.38f, 1f), false);

            var mapRect = UIBuilder.Anchored("Btn_map", _root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -22f), new Vector2(64f, 64f));
            var mapImage = UIBuilder.Circle(mapRect, new Color(0.30f, 0.34f, 0.42f, 1f));
            UIBuilder.Label(UIBuilder.Rect("Label", mapRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), "MAP", 15, UIBuilder.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            var mapButton = mapRect.gameObject.AddComponent<TouchButton>();
            mapButton.Bind(_input, "map", false, mapImage, this);
            _buttons.Add(mapButton);

            var pauseRect = UIBuilder.Anchored("Btn_pause", _root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(286f, -22f), new Vector2(64f, 64f));
            var pauseImage = UIBuilder.Circle(pauseRect, new Color(0.30f, 0.34f, 0.42f, 1f));
            UIBuilder.Label(UIBuilder.Rect("Label", pauseRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), "II", 20, UIBuilder.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            var pauseButton = pauseRect.gameObject.AddComponent<TouchButton>();
            pauseButton.Bind(_input, "pause", false, pauseImage, this);
            _buttons.Add(pauseButton);
        }

        // ------------------------------------------------------------------
        public void ApplyAppearance()
        {
            foreach (var button in _buttons)
            {
                if (button == null) continue;
                button.ApplyAppearance(Scale, Opacity);
            }
            if (_stick != null) _stick.ApplyAppearance(Scale, Opacity);
            if (_root != null) _root.gameObject.SetActive(Enabled);
        }

        public void SetVehicleMode(bool inVehicle)
        {
            _lastInVehicle = inVehicle;
            if (_footGroup != null) _footGroup.gameObject.SetActive(!inVehicle);
            if (_vehicleGroup != null) _vehicleGroup.gameObject.SetActive(inVehicle);
        }

        public void SetInteractive(bool interactive)
        {
            if (_root != null) _root.gameObject.SetActive(Enabled && interactive);
        }

        public void BeginLayoutEdit(bool enabled)
        {
            EditMode = enabled;
            foreach (var button in _buttons) if (button != null) button.EditMode = enabled;
            GameEvents.Notify(enabled ? "Drag buttons to reposition them" : "Layout saved", 3f);
        }

        public Dictionary<string, Vector2> CaptureLayout()
        {
            var map = new Dictionary<string, Vector2>();
            foreach (var button in _buttons)
                if (button != null) map[button.Id] = ((RectTransform)button.transform).anchoredPosition;
            return map;
        }

        public void RestoreLayout(Dictionary<string, Vector2> layout)
        {
            if (layout == null) return;
            foreach (var button in _buttons)
            {
                if (button == null) continue;
                if (layout.TryGetValue(button.Id, out var position))
                    ((RectTransform)button.transform).anchoredPosition = position;
            }
        }

        private void Update()
        {
            var player = Services.Player;
            bool inVehicle = player != null && player.InVehicle;
            if (inVehicle != _lastInVehicle) SetVehicleMode(inVehicle);
        }
    }

    // ------------------------------------------------------------------
    public class TouchStick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private InputHub _input;
        private RectTransform _base;
        private RectTransform _knob;
        private Image _baseImage, _knobImage;
        private int _pointerId = -99;
        private Vector2 _origin;
        private float _radius = 100f;
        private float _opacity = 0.5f;

        public void Build(InputHub input)
        {
            _input = input;
            _base = UIBuilder.Anchored("StickBase", (RectTransform)transform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(200f, 200f), new Vector2(200f, 200f));
            _baseImage = UIBuilder.Circle(_base, new Color(1f, 1f, 1f, 0.16f));
            _knob = UIBuilder.Anchored("StickKnob", _base, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            _knobImage = UIBuilder.Circle(_knob, new Color(1f, 1f, 1f, 0.4f));
            _base.gameObject.SetActive(false);
        }

        public void ApplyAppearance(float scale, float opacity)
        {
            _opacity = opacity;
            _radius = 100f * scale;
            if (_base != null) _base.sizeDelta = new Vector2(200f * scale, 200f * scale);
            if (_knob != null) _knob.sizeDelta = new Vector2(84f * scale, 84f * scale);
            if (_baseImage != null) UIBuilder.SetAlpha(_baseImage, opacity * 0.35f);
            if (_knobImage != null) UIBuilder.SetAlpha(_knobImage, opacity * 0.8f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != -99) return;
            _pointerId = eventData.pointerId;
            _origin = eventData.position;
            var parent = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var local);
            _base.anchoredPosition = local;
            _base.gameObject.SetActive(true);
            _knob.anchoredPosition = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Vector2 delta = eventData.position - _origin;
            float scale = 1f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) scale = canvas.scaleFactor;
            Vector2 clamped = Vector2.ClampMagnitude(delta / Mathf.Max(0.01f, scale), _radius);
            _knob.anchoredPosition = clamped;
            _input?.SetTouchMove(clamped / _radius);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _pointerId = -99;
            _base.gameObject.SetActive(false);
            _input?.SetTouchMove(Vector2.zero);
        }
    }

    // ------------------------------------------------------------------
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public float Sensitivity = 0.16f;
        private InputHub _input;
        private int _pointerId = -99;

        public void Bind(InputHub input) { _input = input; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId == -99) _pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId || _input == null) return;
            _input.AddTouchLook(eventData.delta * Sensitivity);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) _pointerId = -99;
        }
    }

    // ------------------------------------------------------------------
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public string Id;
        public bool Hold;
        public bool EditMode;

        private InputHub _input;
        private Image _image;
        private TouchControls _owner;
        private float _baseSize;
        private float _opacity = 0.55f;
        private bool _pressed;

        public void Bind(InputHub input, string id, bool hold, Image image, TouchControls owner)
        {
            _input = input;
            Id = id;
            Hold = hold;
            _image = image;
            _owner = owner;
            _baseSize = ((RectTransform)transform).sizeDelta.x;
        }

        public void ApplyAppearance(float scale, float opacity)
        {
            _opacity = opacity;
            var rect = (RectTransform)transform;
            rect.sizeDelta = Vector2.one * (_baseSize * scale);
            if (_image != null) UIBuilder.SetAlpha(_image, opacity);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (EditMode) return;
            _pressed = true;
            if (_image != null) UIBuilder.SetAlpha(_image, Mathf.Min(1f, _opacity + 0.3f));
            if (Hold) _input?.SetTouchButton(Id, true);
            else _input?.PressTouchButton(Id);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (EditMode) return;
            _pressed = false;
            if (_image != null) UIBuilder.SetAlpha(_image, _opacity);
            if (Hold) _input?.SetTouchButton(Id, false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!EditMode) return;
            var rect = (RectTransform)transform;
            float scale = 1f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) scale = canvas.scaleFactor;
            rect.anchoredPosition += eventData.delta / Mathf.Max(0.01f, scale);
        }

        private void OnDisable()
        {
            if (_pressed && Hold) _input?.SetTouchButton(Id, false);
            _pressed = false;
        }
    }
}
