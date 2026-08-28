using UnityEngine;
using UnityEngine.UI;

namespace SanMonica.UI
{
    /// <summary>
    /// Helpers for assembling the interface in code. The whole HUD, map, shop
    /// and settings UI is built from these primitives, which keeps the project
    /// free of prefab dependencies and lets the layout adapt to any screen.
    /// </summary>
    public static class UIBuilder
    {
        private static Font _font;
        private static Sprite _roundedSprite;
        private static Sprite _circleSprite;

        public static readonly Color Panel = new Color(0.06f, 0.07f, 0.09f, 0.82f);
        public static readonly Color PanelSolid = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        public static readonly Color Accent = new Color(0.98f, 0.72f, 0.16f);
        public static readonly Color AccentCool = new Color(0.30f, 0.72f, 0.95f);
        public static readonly Color Danger = new Color(0.92f, 0.24f, 0.20f);
        public static readonly Color Good = new Color(0.30f, 0.86f, 0.44f);
        public static readonly Color TextPrimary = new Color(0.96f, 0.96f, 0.97f);
        public static readonly Color TextMuted = new Color(0.68f, 0.70f, 0.74f);

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null)
                {
                    var names = Font.GetOSInstalledFontNames();
                    if (names != null && names.Length > 0) _font = Font.CreateDynamicFontFromOSFont(names[0], 16);
                }
                return _font;
            }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite != null) return _roundedSprite;
                _roundedSprite = BuildRounded(48, 12);
                return _roundedSprite;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite != null) return _circleSprite;
                _circleSprite = BuildCircle(96);
                return _circleSprite;
            }
        }

        private static Sprite BuildRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UIRounded", filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        private static Sprite BuildCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UICircle", filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(r - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------
        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(RectTransform rect, Color color, bool rounded = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = RoundedSprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return image;
        }

        public static Image Circle(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = CircleSprite;
            return image;
        }

        public static Text Label(RectTransform rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = anchor;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Text LabelWrapped(RectTransform rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var label = Label(rect, text, size, color, anchor);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        public static Button Button(RectTransform rect, string text, Color background, Color textColor, System.Action onClick, int fontSize = 22)
        {
            var image = Image(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var labelRect = Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            Label(labelRect, text, fontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            if (onClick != null) button.onClick.AddListener(() => { Services_PlayClick(); onClick(); });
            return button;
        }

        private static void Services_PlayClick()
        {
            SanMonica.Core.Services.Audio?.PlayUi("click");
        }

        public static Slider Slider(RectTransform rect, float min, float max, float value, System.Action<float> onChanged)
        {
            var slider = rect.gameObject.AddComponent<Slider>();
            var background = Rect("Background", rect, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);
            Image(background, new Color(1f, 1f, 1f, 0.14f));

            var fillArea = Rect("FillArea", rect, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillImage = Image(fill, Accent);

            var handleArea = Rect("HandleArea", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = Anchored("Handle", handleArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            Circle(handle, TextPrimary);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = fillImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        public static Toggle Toggle(RectTransform rect, string text, bool value, System.Action<bool> onChanged)
        {
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var box = Anchored("Box", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(28f, 28f));
            var boxImage = Image(box, new Color(1f, 1f, 1f, 0.16f));
            var check = Rect("Check", box, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            var checkImage = Image(check, Accent);

            var labelRect = Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(56f, 0f), new Vector2(-8f, 0f));
            Label(labelRect, text, 20, TextPrimary);

            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(v => { Services_PlayClick(); onChanged(v); });
            return toggle;
        }

        public static ScrollRect ScrollView(RectTransform rect, out RectTransform content)
        {
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mask = viewport.gameObject.AddComponent<Image>();
            mask.color = new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 28f;
            return scroll;
        }

        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }
}
