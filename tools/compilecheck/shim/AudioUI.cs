using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class AudioClip : Object
    {
        public float length => 1f;
        public int samples => 0;
        public int channels => 1;
        public int frequency => 22050;
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => null;
        public bool SetData(float[] data, int offsetSamples) => true;
        public bool GetData(float[] data, int offsetSamples) => true;
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public bool isPlaying => false;
        public float volume { get; set; }
        public float pitch { get; set; }
        public float spatialBlend { get; set; }
        public float minDistance { get; set; }
        public float maxDistance { get; set; }
        public float dopplerLevel { get; set; }
        public float time { get; set; }
        public AudioRolloffMode rolloffMode { get; set; }
        public void Play() { }
        public void Stop() { }
        public void Pause() { }
        public void UnPause() { }
        public void PlayOneShot(AudioClip clip) { }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
    }

    public class AudioListener : Behaviour
    {
        public static float volume { get; set; }
        public static bool pause { get; set; }
    }

    // ---------------- Particles ----------------
    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemShapeType { Sphere, Hemisphere, Cone, Box, Mesh, Circle, Edge }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }
    public enum ParticleSystemCurveMode { Constant, Curve, TwoCurves, TwoConstants }

    public class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public MinMaxCurve(float constant) { this.constant = constant; }
            public MinMaxCurve(float multiplier, AnimationCurve curve) { this.constant = multiplier; }
            public float constant;
            public static implicit operator MinMaxCurve(float f) => new MinMaxCurve(f);
        }

        public struct MinMaxGradient
        {
            public MinMaxGradient(Color c) { }
            public static implicit operator MinMaxGradient(Color c) => new MinMaxGradient(c);
        }

        public struct Burst
        {
            public Burst(float time, short count) { this.time = time; }
            public float time;
        }

        public struct MainModule
        {
            public float duration { get; set; }
            public bool loop { get; set; }
            public bool playOnAwake { get; set; }
            public MinMaxCurve startLifetime { get; set; }
            public MinMaxCurve startSpeed { get; set; }
            public MinMaxCurve startSize { get; set; }
            public MinMaxGradient startColor { get; set; }
            public MinMaxCurve gravityModifier { get; set; }
            public ParticleSystemSimulationSpace simulationSpace { get; set; }
            public int maxParticles { get; set; }
        }

        public struct EmissionModule
        {
            public bool enabled { get; set; }
            public MinMaxCurve rateOverTime { get; set; }
            public void SetBursts(Burst[] bursts) { }
        }

        public struct ShapeModule
        {
            public bool enabled { get; set; }
            public ParticleSystemShapeType shapeType { get; set; }
            public float angle { get; set; }
            public float radius { get; set; }
            public Vector3 scale { get; set; }
        }

        public struct SizeOverLifetimeModule
        {
            public bool enabled { get; set; }
            public MinMaxCurve size { get; set; }
        }

        public MainModule main => default;
        public EmissionModule emission => default;
        public ShapeModule shape => default;
        public SizeOverLifetimeModule sizeOverLifetime => default;
        public void Play() { }
        public void Stop() { }
        public void Clear() { }
        public bool isPlaying => false;
    }

    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode { get; set; }
        public float velocityScale { get; set; }
        public float lengthScale { get; set; }
    }
}

namespace UnityEngine.UI
{
    public class Graphic : Component
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public bool enabled { get; set; }
        public RectTransform rectTransform => null;
    }

    public class MaskableGraphic : Graphic { }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public bool preserveAspect { get; set; }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public float lineSpacing { get; set; }
    }

    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
    }

    public class Selectable : Component
    {
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
        public bool interactable { get; set; }
    }

    public class ButtonClickedEvent { public void AddListener(Action a) { } public void RemoveAllListeners() { } }
    public class Button : Selectable { public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent(); }

    public class SliderEvent { public void AddListener(Action<float> a) { } public void RemoveAllListeners() { } }
    public class Slider : Selectable
    {
        public float value { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public bool wholeNumbers { get; set; }
        public RectTransform fillRect { get; set; }
        public RectTransform handleRect { get; set; }
        public SliderEvent onValueChanged { get; } = new SliderEvent();
    }

    public class ToggleEvent { public void AddListener(Action<bool> a) { } public void RemoveAllListeners() { } }
    public class Toggle : Selectable
    {
        public bool isOn { get; set; }
        public Graphic graphic { get; set; }
        public ToggleEvent onValueChanged { get; } = new ToggleEvent();
    }

    public class Mask : Component { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : Component { }

    public class ScrollRect : Component
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float scrollSensitivity { get; set; }
        public Vector2 normalizedPosition { get; set; }
    }

    public class CanvasScaler : Component
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : Component { }
    public class LayoutElement : Component { }
    public class VerticalLayoutGroup : Component { }
    public class HorizontalLayoutGroup : Component { }
    public class ContentSizeFitter : Component { }
}

namespace UnityEngine
{
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Canvas : Component
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public Camera worldCamera { get; set; }
        public float scaleFactor { get; set; }
        public bool overrideSorting { get; set; }
    }

    public class CanvasGroup : Component
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : Component { }
    public class StandaloneInputModule : Component { }
    public class BaseInputModule : Component { }

    public class PointerEventData
    {
        public int pointerId;
        public Vector2 position;
        public Vector2 delta;
        public Camera pressEventCamera;
        public GameObject pointerPress;
    }

    public interface IPointerDownHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IDragHandler { void OnDrag(PointerEventData eventData); }
    public interface IBeginDragHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IEndDragHandler { void OnEndDrag(PointerEventData eventData); }
}

namespace UnityEngine
{
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        { localPoint = default; return false; }
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => false;
    }
}

// ---------------------------------------------------------------------------
// IMGUI. Used only by the on-device diagnostic overlay, which deliberately does
// not go through the game's own UI so it still draws when that is what is broken.
namespace UnityEngine
{
    public class GUIStyleState
    {
        public Color textColor { get; set; }
        public Texture2D background { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public bool wordWrap { get; set; }
        public GUIStyleState normal { get; } = new GUIStyleState();
    }

    public class GUISkin
    {
        public GUIStyle label { get; } = new GUIStyle();
        public GUIStyle box { get; } = new GUIStyle();
    }

    public static class GUI
    {
        public static GUISkin skin { get; set; } = new GUISkin();
        public static Color color { get; set; }
        public static void Label(Rect r, string text) { }
        public static void Label(Rect r, string text, GUIStyle style) { }
        public static void Box(Rect r, string text) { }
        public static void DrawTexture(Rect r, Texture texture) { }
        public static bool Button(Rect r, string text) => false;
    }
}
