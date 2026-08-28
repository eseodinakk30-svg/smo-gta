// Stubs for the slice of the UnityEditor API the project's editor scripts use.
//
// The editor scripts configure the whole project - URP assets, quality tiers,
// player settings, the boot scene - so a typo in them fails the build just as
// hard as one in gameplay code, and it used to take a twelve minute CI run to
// find out. These declarations exist purely so the compile check can type check
// Assets/Scripts/Editor as well. Signatures follow Unity 2022.3 LTS; where 2022
// renamed something (ApiCompatibilityLevel.NET_Standard, NET_Unity_4_8) only the
// current name is declared, so leftovers from a newer or older API are caught.
namespace UnityEngine
{
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }

    public enum UIOrientation
    {
        Portrait,
        PortraitUpsideDown,
        LandscapeRight,
        LandscapeLeft,
        AutoRotation,
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get; set; }
        public string path { get; set; }
        public bool isLoaded { get; set; }
        public bool IsValid() => true;
    }

    public static class SceneManager
    {
        public static Scene GetActiveScene() => new Scene();
        public static int sceneCount => 1;
    }
}

namespace UnityEditor
{

    using UnityEngine;
    using UnityEngine.Rendering;

    public enum BuildTarget { NoTarget, Android, StandaloneLinux64, StandaloneWindows64, iOS }
    public enum BuildTargetGroup { Unknown, Standalone, Android, iOS }
    public enum ScriptingImplementation { Mono2x, IL2CPP, CoreCLR }
    public enum ManagedStrippingLevel { Disabled, Low, Medium, High }
    public enum Il2CppCompilerConfiguration { Debug, Release, Master }
    public enum ApiCompatibilityLevel { NET_Standard = 6, NET_Unity_4_8 = 3 }
    public enum MobileTextureSubtarget { Generic, ETC, ETC2, ASTC }
    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, All = 3 }
    public enum AndroidSdkVersions
    {
        AndroidApiLevelAuto = 0,
        AndroidApiLevel24 = 24,
        AndroidApiLevel29 = 29,
        AndroidApiLevel33 = 33,
        AndroidApiLevel34 = 34,
    }
    public enum SerializationMode { Mixed, ForceBinary, ForceText }
    public enum BuildOptions { None = 0, Development = 1, AllowDebugging = 2 }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MenuItem : System.Attribute
    {
        public MenuItem(string path) { }
        public MenuItem(string path, bool validate) { }
        public MenuItem(string path, bool validate, int priority) { }
        public int priority { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class InitializeOnLoadAttribute : System.Attribute { }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static ColorSpace colorSpace { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static bool useAnimatedAutorotation { get; set; }
        public static bool gpuSkinning { get; set; }
        public static bool stripEngineCode { get; set; }
        public static bool runInBackground { get; set; }
        public static bool resizableWindow { get; set; }

        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget target, string identifier) { }
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget target, ScriptingImplementation backend) { }
        public static void SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget target, ManagedStrippingLevel level) { }
        public static void SetApiCompatibilityLevel(BuildTargetGroup group, ApiCompatibilityLevel level) { }
        public static void SetIl2CppCompilerConfiguration(BuildTargetGroup group, Il2CppCompilerConfiguration config) { }
        public static void SetMobileMTRendering(BuildTargetGroup group, bool enable) { }
        public static void SetGraphicsAPIs(BuildTarget target, GraphicsDeviceType[] apis) { }
        public static void SetUseDefaultGraphicsAPIs(BuildTarget target, bool useDefault) { }

        public static class Android
        {
            public static int bundleVersionCode { get; set; }
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static bool androidIsGame { get; set; }
            public static bool forceInternetPermission { get; set; }
            public static bool forceSDCardPermission { get; set; }
            public static bool startInFullscreen { get; set; }
            public static bool renderOutsideSafeArea { get; set; }
            public static bool optimizedFramePacing { get; set; }
            public static bool useCustomKeystore { get; set; }
            public static string keystoreName { get; set; }
            public static string keystorePass { get; set; }
            public static string keyaliasName { get; set; }
            public static string keyaliasPass { get; set; }
        }
    }

    public static class EditorUserBuildSettings
    {
        public static bool buildAppBundle { get; set; }
        public static MobileTextureSubtarget androidBuildSubtarget { get; set; }
        public static bool SwitchActiveBuildTarget(BuildTargetGroup group, BuildTarget target) => true;
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; } = new EditorBuildSettingsScene[0];
    }

    public static class EditorSettings
    {
        public static SerializationMode serializationMode { get; set; }
    }

    public static class EditorApplication
    {
        public static System.Action delayCall { get; set; }
        public static bool isPlaying { get; set; }
        public static bool isPlayingOrWillChangePlaymode { get; set; }
        public static void Exit(int code) { }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static void SetDirty(Object target) { }
        public static void CopySerialized(Object source, Object destination) { }
    }

    public static class AssetDatabase
    {
        public static bool IsValidFolder(string path) => true;
        public static string CreateFolder(string parent, string name) => "";
        public static void CreateAsset(Object asset, string path) { }
        public static void ImportAsset(string path) { }
        public static void Refresh() { }
        public static void SaveAssets() { }
        public static T LoadAssetAtPath<T>(string path) where T : Object => null;
        public static Object[] LoadAllAssetsAtPath(string path) => new Object[0];
    }

    public enum SerializedPropertyType { Generic, Integer, Boolean, Float, String, Color, Enum, ObjectReference }

    public class SerializedProperty
    {
        public int arraySize { get; set; }
        public int intValue { get; set; }
        public bool boolValue { get; set; }
        public float floatValue { get; set; }
        public string stringValue { get; set; }
        public int enumValueIndex { get; set; }
        public Object objectReferenceValue { get; set; }
        public SerializedPropertyType propertyType { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int index) => this;
        public SerializedProperty FindPropertyRelative(string path) => this;
        public void InsertArrayElementAtIndex(int index) { }
        public void DeleteArrayElementAtIndex(int index) { }
    }

    public class SerializedObject
    {
        public SerializedObject(Object target) { }
        public SerializedObject(Object[] targets) { }
        public SerializedProperty FindProperty(string path) => new SerializedProperty();
        public void ApplyModifiedPropertiesWithoutUndo() { }
        public void ApplyModifiedProperties() { }
        public void Update() { }
    }

    public static class BuildPipeline
    {
        public static BuildTargetGroup GetBuildTargetGroup(BuildTarget target) => BuildTargetGroup.Android;
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options)
            => new UnityEditor.Build.Reporting.BuildReport();
    }

    public struct BuildPlayerOptions
    {
        public string[] scenes { get; set; }
        public string locationPathName { get; set; }
        public BuildTarget target { get; set; }
        public BuildTargetGroup targetGroup { get; set; }
        public BuildOptions options { get; set; }
    }
}

namespace UnityEditor.Build
{
    public struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Android = new NamedBuildTarget();
        public static readonly NamedBuildTarget Standalone = new NamedBuildTarget();
        public static readonly NamedBuildTarget iOS = new NamedBuildTarget();
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown, Succeeded, Failed, Cancelled }

    public class BuildSummary
    {
        public BuildResult result;
        public string outputPath;
        public ulong totalSize;
        public System.TimeSpan totalTime;
        public int totalErrors;
    }

    public class BuildReport
    {
        public BuildSummary summary = new BuildSummary();
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;

    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => new Scene();
        public static bool SaveScene(Scene scene, string path) => true;
        public static bool SaveScene(Scene scene) => true;
    }
}
