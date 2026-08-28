using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.World;

namespace SanMonica.UI
{
    /// <summary>
    /// The full San Monica map, rasterised once from the generated world:
    /// districts, coastline, the river and the entire road network, with live
    /// blips, the player marker and tap-to-set waypoints.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        public int Resolution = 1024;

        private RectTransform _root;
        private RawImage _mapImage;
        private RectTransform _blipRoot;
        private RectTransform _playerMarker;
        private RectTransform _waypointMarker;
        private Text _title;
        private Texture2D _texture;
        private readonly List<Image> _blipPool = new List<Image>(64);
        private bool _built;
        private float _zoom = 1f;
        private Vector2 _pan;
        private MapBlip _waypoint;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Build(RectTransform parent)
        {
            _root = UIBuilder.Rect("MapScreen", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIBuilder.Image(_root, new Color(0.04f, 0.05f, 0.07f, 0.97f), false);

            var frame = UIBuilder.Rect("Frame", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            frame.anchorMin = new Vector2(0.08f, 0.10f);
            frame.anchorMax = new Vector2(0.92f, 0.92f);
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;

            _mapImage = frame.gameObject.AddComponent<RawImage>();
            _mapImage.color = Color.white;
            var interaction = frame.gameObject.AddComponent<MapInteraction>();
            interaction.Bind(this, frame);

            _blipRoot = UIBuilder.Rect("Blips", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _playerMarker = UIBuilder.Anchored("Player", _blipRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            UIBuilder.Circle(_playerMarker, UIBuilder.Accent);

            _waypointMarker = UIBuilder.Anchored("Waypoint", _blipRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            UIBuilder.Circle(_waypointMarker, new Color(0.95f, 0.35f, 0.85f));
            _waypointMarker.gameObject.SetActive(false);

            var titleRect = UIBuilder.Anchored("Title", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(800f, 40f));
            _title = UIBuilder.Label(titleRect, "SAN MONICA", 32, UIBuilder.TextPrimary, TextAnchor.UpperCenter, FontStyle.Bold);

            var closeRect = UIBuilder.Anchored("Close", _root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(140f, 52f));
            UIBuilder.Button(closeRect, "CLOSE", new Color(0.24f, 0.26f, 0.32f, 0.95f), UIBuilder.TextPrimary, Close);

            var hintRect = UIBuilder.Anchored("Hint", _root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 26f));
            UIBuilder.Label(hintRect, "Tap the map to set a waypoint   •   Pinch or drag to move around", 18, UIBuilder.TextMuted, TextAnchor.LowerCenter);

            _root.gameObject.SetActive(false);
        }

        public void Open()
        {
            if (_root == null) return;
            if (!_built) StartCoroutine(BuildTexture());
            _root.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            Services.Game?.CloseMap();
        }

        // ------------------------------------------------------------------
        private IEnumerator BuildTexture()
        {
            _built = true;
            var map = Services.Map;
            var roads = Services.Roads;
            var config = Services.Config;
            if (map == null || roads == null || config == null) yield break;

            _title.text = "SAN MONICA - drawing map...";
            _texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false) { name = "WorldMap", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Resolution * Resolution];

            float worldSize = config.worldSize;
            float half = config.HalfSize;
            float metresPerPixel = worldSize / Resolution;

            // District and water fill.
            for (int y = 0; y < Resolution; y++)
            {
                float wz = -half + (y + 0.5f) * metresPerPixel;
                for (int x = 0; x < Resolution; x++)
                {
                    float wx = -half + (x + 0.5f) * metresPerPixel;
                    var district = map.DistrictAt(wx, wz);
                    Color colour = DistrictCatalog.Get(district).mapColor;
                    if (district == DistrictType.Ocean) colour = new Color(0.09f, 0.20f, 0.34f);
                    // A little tonal noise stops large districts looking flat.
                    float n = SanMonica.Utils.Noise.Hash(x * 0.7f, y * 0.7f) * 0.06f - 0.03f;
                    colour = new Color(Mathf.Clamp01(colour.r + n), Mathf.Clamp01(colour.g + n), Mathf.Clamp01(colour.b + n));
                    pixels[y * Resolution + x] = colour;
                }
                if ((y & 63) == 0) yield return null;
            }

            // Roads.
            for (int i = 0; i < roads.Segments.Count; i++)
            {
                var segment = roads.Segments[i];
                Color colour = segment.Kind == RoadKind.Highway ? new Color(0.86f, 0.74f, 0.32f)
                    : segment.Kind == RoadKind.Avenue ? new Color(0.78f, 0.78f, 0.80f)
                    : segment.Kind == RoadKind.Runway || segment.Kind == RoadKind.Taxiway ? new Color(0.62f, 0.64f, 0.70f)
                    : segment.Kind == RoadKind.Dirt ? new Color(0.60f, 0.50f, 0.36f)
                    : new Color(0.56f, 0.57f, 0.60f);
                int thickness = segment.Kind == RoadKind.Highway ? 2 : (segment.Kind == RoadKind.Avenue ? 1 : 0);
                DrawLine(pixels, WorldToPixel(segment.A, half, metresPerPixel), WorldToPixel(segment.B, half, metresPerPixel), colour, thickness);
                if ((i & 511) == 0) yield return null;
            }

            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);
            if (_mapImage != null) _mapImage.texture = _texture;
            _title.text = "SAN MONICA";
        }

        private static Vector2Int WorldToPixel(Vector2 world, float half, float metresPerPixel)
        {
            return new Vector2Int(
                Mathf.RoundToInt((world.x + half) / metresPerPixel),
                Mathf.RoundToInt((world.y + half) / metresPerPixel));
        }

        private void DrawLine(Color32[] pixels, Vector2Int a, Vector2Int b, Color colour, int thickness)
        {
            int dx = Mathf.Abs(b.x - a.x), dy = Mathf.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1;
            int err = dx - dy;
            var c32 = (Color32)colour;
            int guard = 0;

            while (guard++ < 4096)
            {
                for (int ox = -thickness; ox <= thickness; ox++)
                for (int oy = -thickness; oy <= thickness; oy++)
                {
                    int px = a.x + ox, py = a.y + oy;
                    if (px < 0 || py < 0 || px >= Resolution || py >= Resolution) continue;
                    pixels[py * Resolution + px] = c32;
                }
                if (a.x == b.x && a.y == b.y) break;
                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; a.x += sx; }
                if (e2 < dx) { err += dx; a.y += sy; }
            }
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            if (!IsOpen) return;
            var config = Services.Config;
            if (config == null || _blipRoot == null) return;

            Rect area = _blipRoot.rect;
            Vector3 player = Services.PlayerPosition;

            Vector2 ToLocal(Vector3 world)
            {
                float u = (world.x + config.HalfSize) / config.worldSize;
                float v = (world.z + config.HalfSize) / config.worldSize;
                return new Vector2((u - 0.5f) * area.width, (v - 0.5f) * area.height);
            }

            _playerMarker.anchoredPosition = ToLocal(player);

            var landmarks = Services.Landmarks;
            int used = 0;
            if (landmarks != null)
            {
                for (int i = 0; i < landmarks.DynamicBlips.Count && used < 60; i++)
                {
                    var blip = landmarks.DynamicBlips[i];
                    var image = GetBlip(used++);
                    image.color = blip.Color;
                    ((RectTransform)image.transform).anchoredPosition = ToLocal(blip.Position);
                    ((RectTransform)image.transform).sizeDelta = Vector2.one * 14f;
                    image.gameObject.SetActive(true);
                }
                for (int i = 0; i < landmarks.StaticBlips.Count && used < 60; i++)
                {
                    var blip = landmarks.StaticBlips[i];
                    if ((blip.Position - player).sqrMagnitude > 2600f * 2600f) continue;
                    var image = GetBlip(used++);
                    image.color = blip.Color;
                    ((RectTransform)image.transform).anchoredPosition = ToLocal(blip.Position);
                    ((RectTransform)image.transform).sizeDelta = Vector2.one * 9f;
                    image.gameObject.SetActive(true);
                }
            }
            for (int i = used; i < _blipPool.Count; i++) _blipPool[i].gameObject.SetActive(false);

            if (_waypoint != null)
            {
                _waypointMarker.gameObject.SetActive(true);
                _waypointMarker.anchoredPosition = ToLocal(_waypoint.Position);
            }
        }

        private Image GetBlip(int index)
        {
            while (_blipPool.Count <= index)
            {
                var rect = UIBuilder.Anchored("MapBlip" + _blipPool.Count, _blipRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
                var image = UIBuilder.Circle(rect, Color.white);
                image.raycastTarget = false;
                _blipPool.Add(image);
            }
            return _blipPool[index];
        }

        public void SetWaypointFromLocal(Vector2 local, Rect area)
        {
            var config = Services.Config;
            var map = Services.Map;
            if (config == null || map == null) return;

            float u = local.x / area.width + 0.5f;
            float v = local.y / area.height + 0.5f;
            float wx = (u - 0.5f) * config.worldSize;
            float wz = (v - 0.5f) * config.worldSize;
            float wy = map.SampleHeight(wx, wz);

            if (_waypoint != null) Services.Landmarks?.RemoveDynamic(_waypoint);
            _waypoint = Services.Landmarks?.AddDynamic(BlipKind.Waypoint, new Vector3(wx, wy, wz), "Waypoint", new Color(0.95f, 0.35f, 0.85f));
            GameEvents.Notify("Waypoint set", 2f);
            Services.Audio?.PlayUi("click");
        }

        public void ClearWaypoint()
        {
            if (_waypoint != null) Services.Landmarks?.RemoveDynamic(_waypoint);
            _waypoint = null;
            if (_waypointMarker != null) _waypointMarker.gameObject.SetActive(false);
        }
    }

    public class MapInteraction : MonoBehaviour, IPointerClickHandler
    {
        private MapScreen _screen;
        private RectTransform _area;

        public void Bind(MapScreen screen, RectTransform area)
        {
            _screen = screen;
            _area = area;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_screen == null || _area == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position, eventData.pressEventCamera, out var local)) return;
            _screen.SetWaypointFromLocal(local, _area.rect);
        }
    }
}
