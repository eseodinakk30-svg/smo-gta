using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SanMonica.Core;

namespace SanMonica.Atmosphere
{
    /// <summary>
    /// Post processing for San Monica: bloom, tonemapping, colour grading and a
    /// vignette, all driven live by the clock and the weather so noon, dusk,
    /// a thunderstorm and being underwater each grade differently.
    /// </summary>
    public class PostProcessRig : MonoBehaviour
    {
        public bool Enabled = true;
        public float BloomScale = 1f;

        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;
        private ColorAdjustments _colour;
        private Vignette _vignette;
        private Tonemapping _tonemapping;
        private UniversalAdditionalCameraData _cameraData;
        private float _underwater;

        public void Initialize(Camera camera)
        {
            if (camera == null) return;

            _cameraData = camera.GetUniversalAdditionalCameraData();
            if (_cameraData != null) _cameraData.renderPostProcessing = true;

            var go = new GameObject("PostProcessing");
            go.transform.SetParent(transform, false);
            go.layer = 0;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "SanMonicaGrade";

            _bloom = _profile.Add<Bloom>(true);
            _bloom.intensity.Override(0.55f);
            _bloom.threshold.Override(1.05f);
            _bloom.scatter.Override(0.62f);
            _bloom.tint.Override(new Color(1f, 0.96f, 0.90f));

            _tonemapping = _profile.Add<Tonemapping>(true);
            _tonemapping.mode.Override(TonemappingMode.Neutral);

            _colour = _profile.Add<ColorAdjustments>(true);
            _colour.postExposure.Override(0f);
            _colour.contrast.Override(6f);
            _colour.saturation.Override(4f);
            _colour.colorFilter.Override(Color.white);

            _vignette = _profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.18f);
            _vignette.smoothness.Override(0.42f);

            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 0f;
            _volume.weight = 1f;
            _volume.profile = _profile;
        }

        public void SetEnabled(bool enabled, float bloomScale)
        {
            Enabled = enabled;
            BloomScale = bloomScale;
            if (_cameraData != null) _cameraData.renderPostProcessing = enabled;
            if (_volume != null) _volume.weight = enabled ? 1f : 0f;
        }

        private void LateUpdate()
        {
            if (!Enabled || _colour == null) return;

            var clock = Services.Clock;
            var weather = Services.Weather;
            float daylight = clock != null ? clock.DaylightAmount : 1f;
            float overcast = weather != null ? weather.Overcast : 0f;
            float wetness = weather != null ? weather.Wetness : 0f;

            bool underwater = Services.Player != null && Services.Player.IsUnderwater;
            _underwater = Mathf.MoveTowards(_underwater, underwater ? 1f : 0f, Time.deltaTime * 3f);

            // Night lifts exposure and cools the image; storms desaturate it.
            float exposure = Mathf.Lerp(0.55f, -0.05f, daylight) - overcast * 0.25f;
            float saturation = Mathf.Lerp(-14f, 6f, daylight) - overcast * 16f + wetness * 4f;
            float contrast = 6f + overcast * 6f;
            Color filter = Color.Lerp(new Color(0.80f, 0.86f, 1f), new Color(1f, 0.98f, 0.94f), daylight);
            if (_underwater > 0.01f)
            {
                filter = Color.Lerp(filter, new Color(0.45f, 0.80f, 0.95f), _underwater);
                saturation -= 10f * _underwater;
                exposure -= 0.35f * _underwater;
            }

            _colour.postExposure.Override(exposure);
            _colour.saturation.Override(saturation);
            _colour.contrast.Override(contrast);
            _colour.colorFilter.Override(filter);

            if (_bloom != null)
            {
                _bloom.intensity.Override(Mathf.Lerp(0.85f, 0.35f, daylight) * BloomScale);
                _bloom.threshold.Override(Mathf.Lerp(0.85f, 1.15f, daylight));
            }

            if (_vignette != null)
                _vignette.intensity.Override(0.16f + _underwater * 0.22f + overcast * 0.06f);
        }
    }
}
