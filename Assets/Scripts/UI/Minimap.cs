using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SanMonica.Core;
using SanMonica.World;

namespace SanMonica.UI
{
    /// <summary>
    /// Rotating minimap: a low resolution top-down camera render with world
    /// blips drawn on top. The camera renders at a reduced rate so the map costs
    /// very little on a phone.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        public float Size = 180f;
        public float ViewRange = 95f;
        public int Resolution = 192;
        public int RenderEveryNthFrame = 2;
        public bool RotateWithPlayer = true;

        private Camera _camera;
        private RenderTexture _texture;
        private RawImage _display;
        private RectTransform _blipRoot;
        private RectTransform _playerArrow;
        private readonly List<Image> _blipPool = new List<Image>(48);
        private int _frame;

        public void Build(RectTransform parent)
        {
            var frame = UIBuilder.Anchored("Minimap", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(Size, Size));
            UIBuilder.Circle(frame, new Color(0.04f, 0.05f, 0.07f, 0.9f));

            var inner = UIBuilder.Rect("Inner", frame, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            _display = inner.gameObject.AddComponent<RawImage>();
            _display.raycastTarget = false;

            var mask = inner.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            _blipRoot = UIBuilder.Rect("Blips", inner, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _playerArrow = UIBuilder.Anchored("Player", inner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
            UIBuilder.Circle(_playerArrow, UIBuilder.Accent);

            CreateCamera();
        }

        private void CreateCamera()
        {
            _texture = new RenderTexture(Resolution, Resolution, 16, RenderTextureFormat.Default)
            {
                name = "MinimapRT",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };
            _texture.Create();
            if (_display != null) _display.texture = _texture;

            var go = new GameObject("MinimapCamera");
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = ViewRange;
            _camera.targetTexture = _texture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
            _camera.cullingMask = ~((1 << GameLayers.UI) | (1 << GameLayers.Ped) | (1 << GameLayers.Projectile) | (1 << GameLayers.Ragdoll));
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 900f;
            _camera.enabled = false;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
        }

        public void SetQuality(int resolution, int renderEveryNthFrame, float viewRange)
        {
            ViewRange = viewRange;
            RenderEveryNthFrame = Mathf.Max(1, renderEveryNthFrame);
            if (resolution != Resolution && resolution >= 64)
            {
                Resolution = resolution;
                if (_texture != null) { _texture.Release(); Destroy(_texture); }
                CreateCamera();
            }
            if (_camera != null) _camera.orthographicSize = ViewRange;
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            Vector3 player = Services.PlayerPosition;
            var playerTransform = Services.PlayerTransform;

            _camera.transform.position = player + Vector3.up * 320f;
            float heading = playerTransform != null ? playerTransform.eulerAngles.y : 0f;
            _camera.transform.rotation = Quaternion.Euler(90f, RotateWithPlayer ? heading : 0f, 0f);

            _frame++;
            if (_frame % Mathf.Max(1, RenderEveryNthFrame) == 0) _camera.Render();

            UpdateBlips(player, heading);
        }

        private void UpdateBlips(Vector3 player, float heading)
        {
            var landmarks = Services.Landmarks;
            if (landmarks == null || _blipRoot == null) return;

            int used = 0;
            float halfSize = (Size - 8f) * 0.5f;
            float scale = halfSize / ViewRange;
            float rotation = RotateWithPlayer ? -heading * Mathf.Deg2Rad : 0f;
            float cos = Mathf.Cos(rotation), sin = Mathf.Sin(rotation);

            void Place(MapBlip blip)
            {
                Vector3 delta = blip.Position - player;
                if (Mathf.Abs(delta.x) > ViewRange * 1.6f || Mathf.Abs(delta.z) > ViewRange * 1.6f) return;

                float x = delta.x * cos - delta.z * sin;
                float y = delta.x * sin + delta.z * cos;
                Vector2 local = new Vector2(x, y) * scale;
                if (local.magnitude > halfSize) local = local.normalized * halfSize;

                var image = GetBlip(used++);
                image.color = blip.Color;
                var rt = (RectTransform)image.transform;
                rt.anchoredPosition = local;
                rt.sizeDelta = Vector2.one * (blip.Kind == BlipKind.Mission ? 14f : 9f);
                image.gameObject.SetActive(true);
            }

            for (int i = 0; i < landmarks.DynamicBlips.Count && used < 40; i++) Place(landmarks.DynamicBlips[i]);
            for (int i = 0; i < landmarks.StaticBlips.Count && used < 40; i++) Place(landmarks.StaticBlips[i]);

            for (int i = used; i < _blipPool.Count; i++) _blipPool[i].gameObject.SetActive(false);
        }

        private Image GetBlip(int index)
        {
            while (_blipPool.Count <= index)
            {
                var rt = UIBuilder.Anchored("Blip" + _blipPool.Count, _blipRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));
                var image = UIBuilder.Circle(rt, Color.white);
                image.raycastTarget = false;
                _blipPool.Add(image);
            }
            return _blipPool[index];
        }

        private void OnDestroy()
        {
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
        }
    }
}
