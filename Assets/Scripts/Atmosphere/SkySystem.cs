using UnityEngine;
using SanMonica.Core;
using SanMonica.Utils;

namespace SanMonica.Atmosphere
{
    /// <summary>
    /// Procedural sky, sun, moon and stars, plus the ambient and fog settings
    /// that make dawn, noon, dusk and night read differently.
    /// </summary>
    public class SkySystem : MonoBehaviour
    {
        public Light Sun { get; private set; }
        public Light Moon { get; private set; }

        [Header("Palette")]
        public Color DayZenith = new Color(0.28f, 0.48f, 0.82f);
        public Color DayHorizon = new Color(0.72f, 0.83f, 0.94f);
        public Color DuskZenith = new Color(0.16f, 0.16f, 0.36f);
        public Color DuskHorizon = new Color(0.92f, 0.48f, 0.24f);
        public Color NightZenith = new Color(0.017f, 0.024f, 0.062f);
        public Color NightHorizon = new Color(0.05f, 0.07f, 0.14f);

        [Header("Fog")]
        public float BaseFogDensity = 0.0016f;
        public float FogDistanceScale = 1f;

        private Material _skyMaterial;
        private Transform _starDome;
        private MeshRenderer _starRenderer;
        private TimeOfDaySystem _clock;
        private WeatherSystem _weather;

        public void Initialize(TimeOfDaySystem clock, WeatherSystem weather)
        {
            _clock = clock;
            _weather = weather;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.72f;
            Sun.intensity = 1.15f;
            Sun.color = new Color(1f, 0.96f, 0.88f);
            RenderSettings.sun = Sun;

            var moonGo = new GameObject("Moon");
            moonGo.transform.SetParent(transform, false);
            Moon = moonGo.AddComponent<Light>();
            Moon.type = LightType.Directional;
            Moon.shadows = LightShadows.None;
            Moon.intensity = 0.20f;
            Moon.color = new Color(0.62f, 0.72f, 1f);

            BuildSkyMaterial();
            BuildStars();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        private void BuildSkyMaterial()
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) shader = Shader.Find("Skybox/Gradient");
            if (shader == null) return;
            _skyMaterial = new Material(shader) { name = "SanMonicaSky" };
            if (_skyMaterial.HasProperty("_SunSize")) _skyMaterial.SetFloat("_SunSize", 0.035f);
            if (_skyMaterial.HasProperty("_AtmosphereThickness")) _skyMaterial.SetFloat("_AtmosphereThickness", 1.05f);
            RenderSettings.skybox = _skyMaterial;
        }

        private void BuildStars()
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(transform, false);
            _starDome = go.transform;

            var mb = new MeshBuilder(1);
            var rng = new Rng(90210);
            for (int i = 0; i < 380; i++)
            {
                Vector3 dir = rng.OnUnitSphere();
                if (dir.y < 0.02f) dir.y = Mathf.Abs(dir.y) + 0.02f;
                Vector3 p = dir * 900f;
                float size = rng.Range(1.6f, 5.5f);
                Vector3 right = Vector3.Cross(dir, Vector3.up).normalized * size;
                Vector3 up = Vector3.Cross(right, dir).normalized * size;
                mb.AddQuad(p - right - up, p + right - up, p + right + up, p - right + up, Vector2.one, 0);
            }

            var mf = go.AddComponent<MeshFilter>();
            _starRenderer = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mb.ToMesh("Stars");
            _starRenderer.sharedMaterial = MaterialLibrary.Unlit(new Color(1f, 1f, 1f, 1f), true);
            _starRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _starRenderer.receiveShadows = false;
        }

        private void LateUpdate()
        {
            if (_clock == null) return;
            float t = _clock.TimeOfDay;
            float daylight = _clock.DaylightAmount;

            // Sun and moon arcs.
            float sunAngle = (t / 24f) * 360f - 90f;
            Sun.transform.rotation = Quaternion.Euler(sunAngle, 165f, 0f);
            Moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, 165f, 0f);

            float overcast = _weather != null ? _weather.Overcast : 0f;
            Sun.intensity = Mathf.Lerp(0f, 1.35f, daylight) * Mathf.Lerp(1f, 0.35f, overcast);
            Sun.enabled = Sun.intensity > 0.02f;
            Moon.intensity = Mathf.Lerp(0.28f, 0f, daylight) * Mathf.Lerp(1f, 0.4f, overcast);
            Moon.enabled = Moon.intensity > 0.01f;

            Sun.color = Color.Lerp(new Color(1f, 0.62f, 0.36f), new Color(1f, 0.97f, 0.92f), Mathf.Clamp01(daylight * 1.6f));

            // Sky colours.
            Color zenith, horizon;
            if (daylight > 0.55f)
            {
                float k = Mathf.InverseLerp(0.55f, 1f, daylight);
                zenith = Color.Lerp(DuskZenith, DayZenith, k);
                horizon = Color.Lerp(DuskHorizon, DayHorizon, k);
            }
            else if (daylight > 0.12f)
            {
                float k = Mathf.InverseLerp(0.12f, 0.55f, daylight);
                zenith = Color.Lerp(NightZenith, DuskZenith, k);
                horizon = Color.Lerp(NightHorizon, DuskHorizon, k);
            }
            else
            {
                zenith = NightZenith;
                horizon = NightHorizon;
            }

            if (overcast > 0.01f)
            {
                Color grey = new Color(0.32f, 0.34f, 0.38f);
                zenith = Color.Lerp(zenith, grey * 0.6f, overcast);
                horizon = Color.Lerp(horizon, grey, overcast);
            }

            RenderSettings.ambientSkyColor = zenith * 1.15f;
            RenderSettings.ambientEquatorColor = horizon * 0.9f;
            RenderSettings.ambientGroundColor = horizon * 0.35f;
            RenderSettings.ambientIntensity = Mathf.Lerp(0.35f, 1f, daylight);

            if (_skyMaterial != null)
            {
                if (_skyMaterial.HasProperty("_SkyTint")) _skyMaterial.SetColor("_SkyTint", zenith);
                if (_skyMaterial.HasProperty("_GroundColor")) _skyMaterial.SetColor("_GroundColor", horizon * 0.5f);
                if (_skyMaterial.HasProperty("_Exposure")) _skyMaterial.SetFloat("_Exposure", Mathf.Lerp(0.35f, 1.25f, daylight));
                if (_skyMaterial.HasProperty("_AtmosphereThickness"))
                    _skyMaterial.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.85f, 2.2f, overcast));
            }

            // Fog follows the weather and the light.
            float visibility = _weather != null ? _weather.VisibilityScale : 1f;
            RenderSettings.fogColor = Color.Lerp(horizon, new Color(0.55f, 0.57f, 0.60f), overcast * 0.6f);
            RenderSettings.fogDensity = BaseFogDensity / Mathf.Max(0.15f, visibility * FogDistanceScale);

            // Stars fade in at night and rotate slowly.
            if (_starDome != null)
            {
                var cam = Services.Camera;
                if (cam != null && cam.Cam != null) _starDome.position = cam.Cam.transform.position;
                _starDome.rotation = Quaternion.Euler(0f, t * 4f, 12f);
                float starAlpha = Mathf.Clamp01(1f - daylight * 2.4f) * (1f - overcast);
                _starRenderer.enabled = starAlpha > 0.02f;
                if (_starRenderer.enabled)
                    _starRenderer.sharedMaterial = MaterialLibrary.Unlit(new Color(1f, 1f, 1f, starAlpha), true);
            }
        }
    }
}
