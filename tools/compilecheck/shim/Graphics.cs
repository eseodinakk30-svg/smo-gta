using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum IndexFormat { UInt16, UInt32 }
    public enum BlendMode { Zero, One, DstColor, SrcColor, OneMinusDstColor, SrcAlpha, OneMinusSrcColor, DstAlpha, OneMinusDstAlpha, SrcAlphaSaturate, OneMinusSrcAlpha }
    public enum RenderQueue { Background = 1000, Geometry = 2000, AlphaTest = 2450, GeometryLast = 2500, Transparent = 3000, Overlay = 4000 }
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
    public enum GraphicsDeviceType { OpenGLES3, Vulkan, Direct3D11, Metal }
    public static class GraphicsSettings
    {
        public static RenderPipelineAsset defaultRenderPipeline { get; set; }
        public static RenderPipelineAsset currentRenderPipeline => defaultRenderPipeline;
    }

    public class RenderPipelineAsset : UnityEngine.ScriptableObject { }
}

namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public float renderScale { get; set; }
        public float shadowDistance { get; set; }
        public int shadowCascadeCount { get; set; }
        public int msaaSampleCount { get; set; }
        public bool supportsHDR { get; set; }
        public bool useSRPBatcher { get; set; }
    }

    public class UniversalRendererData : UnityEngine.ScriptableObject { }
    public class ScriptableRendererData : UnityEngine.ScriptableObject { }
    public static class UniversalRenderPipeline
    {
        public static UniversalRenderPipelineAsset asset { get; set; }
    }
}

namespace UnityEngine
{
    using UnityEngine.Rendering;

    public enum ShadowQuality { Disable, HardOnly, All }
    public enum ShadowResolution { Low, Medium, High, VeryHigh }
    public enum SkinWeights { OneBone, TwoBones, FourBones, Unlimited }
    public enum SkinQuality { Auto, Bone1, Bone2, Bone4 }
    public enum AnisotropicFiltering { Disable, Enable, ForceEnable }
    public enum MotionVectorGenerationMode { Camera, Object, ForceNoMotion }
    public enum MaterialGlobalIlluminationFlags { None, RealtimeEmissive, BakedEmissive, EmissiveIsBlack }
    public enum FogMode { Linear, Exponential, ExponentialSquared }
    public enum TextureFormat { RGBA32, RGB24, ARGB32, Alpha8, DXT1, DXT5 }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum RenderTextureFormat { Default, ARGB32, Depth, RFloat }
    public enum SpriteMeshType { FullRect, Tight }
    public enum CameraClearFlags { Skybox, SolidColor, Depth, Nothing }
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public enum LightRenderMode { Auto, ForcePixel, ForceVertex }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    public static class QualitySettings
    {
        public static string[] names => new[] { "Low", "Medium", "High" };
        public static ShadowQuality shadows { get; set; }
        public static ShadowResolution shadowResolution { get; set; }
        public static SkinWeights skinWeights { get; set; }
        public static bool softParticles { get; set; }
        public static bool billboardsFaceCameraPosition { get; set; }
        public static float lodBias { get; set; }
        public static int maximumLODLevel { get; set; }
        public static int vSyncCount { get; set; }
        public static int particleRaycastBudget { get; set; }
        public static int asyncUploadTimeSlice { get; set; }
        public static int asyncUploadBufferSize { get; set; }
        public static AnisotropicFiltering anisotropicFiltering { get; set; }
        public static UnityEngine.Rendering.RenderPipelineAsset renderPipeline { get; set; }
        public static void SetQualityLevel(int level, bool applyExpensiveChanges) { }
        public static int GetQualityLevel() => 0;
    }

    public static class RenderSettings
    {
        public static bool fog { get; set; }
        public static FogMode fogMode { get; set; }
        public static Color fogColor { get; set; }
        public static float fogDensity { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
        public static Material skybox { get; set; }
        public static Light sun { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Color ambientEquatorColor { get; set; }
        public static Color ambientGroundColor { get; set; }
        public static Color ambientLight { get; set; }
        public static float ambientIntensity { get; set; }
        public static UnityEngine.Rendering.AmbientMode ambientMode { get; set; }
    }

    public class Texture : Object
    {
        public int width { get; set; }
        public int height { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public FilterMode filterMode { get; set; }
        public int anisoLevel { get; set; }
    }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { }
        public Texture2D(int w, int h, TextureFormat f, bool mipChain) { }
        public Texture2D(int w, int h, TextureFormat f, bool mipChain, bool linear) { }
        public void SetPixels32(Color32[] px) { }
        public void SetPixels(Color[] px) { }
        public Color32[] GetPixels32() => new Color32[0];
        public void SetPixel(int x, int y, Color c) { }
        public Color GetPixel(int x, int y) => default;
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        public void Compress(bool highQuality) { }
        public static Texture2D whiteTexture => null;
    }

    public class RenderTexture : Texture
    {
        public RenderTexture(int w, int h, int depth) { }
        public RenderTexture(int w, int h, int depth, RenderTextureFormat f) { }
        public int antiAliasing { get; set; }
        public bool Create() => true;
        public void Release() { }
    }

    public class Sprite : Object
    {
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot) => null;
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot, float ppu) => null;
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot, float ppu, uint extrude, SpriteMeshType meshType, Vector4 border) => null;
    }

    public class Shader : Object
    {
        public static Shader Find(string name) => null;
    }

    public class Material : Object
    {
        public Material(Shader s) { }
        public Material(Material m) { }
        public Shader shader { get; set; }
        public Color color { get; set; }
        public bool enableInstancing { get; set; }
        public int renderQueue { get; set; }
        public MaterialGlobalIlluminationFlags globalIlluminationFlags { get; set; }
        public void SetColor(string n, Color c) { }
        public Color GetColor(string n) => default;
        public void SetFloat(string n, float v) { }
        public float GetFloat(string n) => 0f;
        public void SetInt(string n, int v) { }
        public void SetTexture(string n, Texture t) { }
        public Texture GetTexture(string n) => null;
        public void SetTextureScale(string n, Vector2 s) { }
        public void SetVector(string n, Vector4 v) { }
        public bool HasProperty(string n) => true;
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
    }

    public class Mesh : Object
    {
        public UnityEngine.Rendering.IndexFormat indexFormat { get; set; }
        public int subMeshCount { get; set; }
        public Vector3[] vertices { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public Color32[] colors32 { get; set; }
        public int[] triangles { get; set; }
        public BoneWeight[] boneWeights { get; set; }
        public Matrix4x4[] bindposes { get; set; }
        public Bounds bounds { get; set; }
        public int vertexCount => 0;
        public void SetVertices(List<Vector3> v) { }
        public void SetNormals(List<Vector3> v) { }
        public void SetUVs(int channel, List<Vector2> v) { }
        public void SetColors(List<Color32> v) { }
        public void SetTriangles(List<int> t, int submesh) { }
        public void RecalculateNormals() { }
        public void RecalculateTangents() { }
        public void RecalculateBounds() { }
        public void UploadMeshData(bool markNoLongerReadable) { }
        public void MarkDynamic() { }
        public void Clear() { }
    }

    public struct BoneWeight
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
    }

    public class Renderer : Component
    {
        public bool enabled { get; set; }
        public Material material { get; set; }
        public Material[] materials { get; set; }
        public Material sharedMaterial { get; set; }
        public Material[] sharedMaterials { get; set; }
        public Bounds bounds => default;
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
        public bool allowOcclusionWhenDynamic { get; set; }
        public MotionVectorGenerationMode motionVectorGenerationMode { get; set; }
    }

    public class MeshRenderer : Renderer { }

    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh { get; set; }
        public Transform[] bones { get; set; }
        public Transform rootBone { get; set; }
        public SkinQuality quality { get; set; }
        public bool updateWhenOffscreen { get; set; }
        public Bounds localBounds { get; set; }
    }

    public class MeshFilter : Component
    {
        public Mesh mesh { get; set; }
        public Mesh sharedMesh { get; set; }
    }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public int cullingMask { get; set; }
        public RenderTexture targetTexture { get; set; }
        public bool allowHDR { get; set; }
        public bool allowMSAA { get; set; }
        public int depth { get; set; }
        public void Render() { }
        public Ray ViewportPointToRay(Vector3 p) => default;
        public Ray ScreenPointToRay(Vector3 p) => default;
        public Vector3 WorldToScreenPoint(Vector3 p) => p;
        public Vector3 WorldToViewportPoint(Vector3 p) => p;
    }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
        public float spotAngle { get; set; }
        public LightShadows shadows { get; set; }
        public float shadowStrength { get; set; }
        public LightRenderMode renderMode { get; set; }
    }

    public class Font : Object
    {
        public static string[] GetOSInstalledFontNames() => new string[0];
        public static Font CreateDynamicFontFromOSFont(string name, int size) => null;
    }
}

namespace UnityEngine.Rendering
{
    public class VolumeProfile : UnityEngine.ScriptableObject
    {
        public T Add<T>(bool overrides) where T : VolumeComponent => UnityEngine.ScriptableObject.CreateInstance<T>();
    }

    public class VolumeComponent : UnityEngine.ScriptableObject { }

    public class Volume : UnityEngine.Component
    {
        public bool isGlobal { get; set; }
        public float priority { get; set; }
        public float weight { get; set; }
        public VolumeProfile profile { get; set; }
    }

    public class VolumeParameter<T> { public void Override(T value) { } public T value { get; set; } }
    public class FloatParameter : VolumeParameter<float> { }
    public class ColorParameter : VolumeParameter<UnityEngine.Color> { }
}

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }

    public class Bloom : VolumeComponent
    {
        public VolumeParameter<float> intensity = new VolumeParameter<float>();
        public VolumeParameter<float> threshold = new VolumeParameter<float>();
        public VolumeParameter<float> scatter = new VolumeParameter<float>();
        public VolumeParameter<UnityEngine.Color> tint = new VolumeParameter<UnityEngine.Color>();
    }

    public class Tonemapping : VolumeComponent
    {
        public VolumeParameter<TonemappingMode> mode = new VolumeParameter<TonemappingMode>();
    }

    public class ColorAdjustments : VolumeComponent
    {
        public VolumeParameter<float> postExposure = new VolumeParameter<float>();
        public VolumeParameter<float> contrast = new VolumeParameter<float>();
        public VolumeParameter<float> saturation = new VolumeParameter<float>();
        public VolumeParameter<UnityEngine.Color> colorFilter = new VolumeParameter<UnityEngine.Color>();
    }

    public class Vignette : VolumeComponent
    {
        public VolumeParameter<float> intensity = new VolumeParameter<float>();
        public VolumeParameter<float> smoothness = new VolumeParameter<float>();
    }

    public class UniversalAdditionalCameraData : UnityEngine.Component
    {
        public bool renderPostProcessing { get; set; }
        public bool renderShadows { get; set; }
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this UnityEngine.Camera camera) => null;
    }
}
