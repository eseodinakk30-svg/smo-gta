using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.All)] public class HeaderAttribute : PropertyAttribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.All)] public class TooltipAttribute : PropertyAttribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.All)] public class RangeAttribute : PropertyAttribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.All)] public class TextAreaAttribute : PropertyAttribute { public TextAreaAttribute() { } public TextAreaAttribute(int a, int b) { } }
    [AttributeUsage(AttributeTargets.All)] public class SpaceAttribute : PropertyAttribute { public SpaceAttribute() { } public SpaceAttribute(float h) { } }
    public class PropertyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class CreateAssetMenuAttribute : Attribute { public string menuName; public string fileName; public int order; }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } public RequireComponent(Type a, Type b) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class ContextMenu : Attribute { public ContextMenu(string n) { } }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, BeforeSplashScreen, SubsystemRegistration, AfterAssembliesLoaded }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    public class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() => 0;
        public override string ToString() => name;
        public static void Destroy(Object o) { }
        public static void Destroy(Object o, float delay) { }
        public static void DestroyImmediate(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T Instantiate<T>(T original) where T : Object => original;
        public static T Instantiate<T>(T original, Vector3 p, Quaternion r) where T : Object => original;
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static T FindObjectOfType<T>() where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => new T[0];
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
    }

    public enum FindObjectsSortMode { None, InstanceID }
    public enum HideFlags { None, HideInHierarchy, DontSave }

    public class Component : Object
    {
        public GameObject gameObject { get; set; }
        public Transform transform { get; set; }
        public string tag { get; set; }
        public T GetComponent<T>() => default;
        public Component GetComponent(Type t) => null;
        public T GetComponentInParent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T[] GetComponents<T>() => new T[0];
        public T[] GetComponentsInChildren<T>() => new T[0];
        public bool TryGetComponent<T>(out T value) { value = default; return false; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
        public bool isActiveAndEnabled => true;
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public void StopCoroutine(Coroutine c) { }
        public void StopCoroutine(IEnumerator c) { }
        public void StopAllCoroutines() { }
        public void Invoke(string name, float delay) { }
        public void InvokeRepeating(string name, float delay, float repeat) { }
        public void CancelInvoke() { }
        public void CancelInvoke(string name) { }
        public bool IsInvoking(string name) => false;
        public static void print(object o) { }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => Activator.CreateInstance<T>();
        public static ScriptableObject CreateInstance(Type t) => null;
    }

    public class Coroutine { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public class WaitForSecondsRealtime : YieldInstruction { public WaitForSecondsRealtime(float s) { } }
    public class WaitForEndOfFrame : YieldInstruction { }
    public class WaitForFixedUpdate : YieldInstruction { }
    public class WaitUntil : YieldInstruction { public WaitUntil(Func<bool> f) { } }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform { get; set; }
        public int layer { get; set; }
        public string tag { get; set; }
        public bool isStatic { get; set; }
        public bool activeSelf => true;
        public bool activeInHierarchy => true;
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component => Activator.CreateInstance<T>();
        public Component AddComponent(Type t) => null;
        public T GetComponent<T>() => default;
        public T GetComponentInParent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T[] GetComponents<T>() => new T[0];
        public T[] GetComponentsInChildren<T>() => new T[0];
        public bool TryGetComponent<T>(out T value) { value = default; return false; }
        public static GameObject Find(string name) => null;
        public static GameObject CreatePrimitive(PrimitiveType t) => null;
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 lossyScale => default;
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 forward { get; set; }
        public Vector3 right { get; set; }
        public Vector3 up { get; set; }
        public Transform parent { get; set; }
        public Transform root => this;
        public int childCount => 0;
        public Matrix4x4 worldToLocalMatrix => default;
        public Matrix4x4 localToWorldMatrix => default;
        public void SetParent(Transform p) { }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public Transform GetChild(int index) => null;
        public Transform Find(string name) => null;
        public void SetAsLastSibling() { }
        public void SetAsFirstSibling() { }
        public void SetSiblingIndex(int i) { }
        public void Translate(Vector3 v) { }
        public void Rotate(Vector3 axis, float angle) { }
        public void Rotate(Vector3 axis, float angle, Space space) { }
        public void LookAt(Vector3 target) { }
        public void LookAt(Transform target) { }
        public Vector3 TransformPoint(Vector3 p) => p;
        public Vector3 InverseTransformPoint(Vector3 p) => p;
        public Vector3 TransformDirection(Vector3 d) => d;
        public Vector3 InverseTransformDirection(Vector3 d) => d;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
        public void SetLocalPositionAndRotation(Vector3 p, Quaternion r) { }
        public bool IsChildOf(Transform other) => false;
        public IEnumerator GetEnumerator() => null;
    }

    public enum Space { World, Self }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Rect rect => default;
    }

    public static class Time
    {
        public static float time => 0f;
        public static float deltaTime => 0f;
        public static float fixedDeltaTime { get; set; }
        public static float unscaledDeltaTime => 0f;
        public static float unscaledTime => 0f;
        public static float timeScale { get; set; }
        public static float maximumDeltaTime { get; set; }
        public static int frameCount => 0;
        public static float realtimeSinceStartup => 0f;
    }

    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
        public static void LogException(Exception e) { }
        public static void DrawLine(Vector3 a, Vector3 b) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c) { }
        public static void DrawRay(Vector3 a, Vector3 b, Color c) { }
    }

    public static class Application
    {
        public static bool isMobilePlatform { get; set; }
        public static bool isEditor => false;
        public static bool isPlaying => true;
        public static bool isBatchMode => false;
        public static bool runInBackground { get; set; }
        public static int targetFrameRate { get; set; }
        public static string persistentDataPath => "";
        public static string dataPath => "";
        public static RuntimePlatform platform => RuntimePlatform.Android;
        public static void Quit() { }
    }

    public enum RuntimePlatform { Android, IPhonePlayer, WindowsPlayer, OSXPlayer, LinuxPlayer, WindowsEditor }

    public static class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
        public static float dpi => 300f;
        public static SleepTimeout sleepTimeout { get; set; }
        public static bool fullScreen { get; set; }
        public static void SetResolution(int w, int h, bool fs) { }
        public static Rect safeArea => default;
    }

    public struct SleepTimeout
    {
        public static SleepTimeout NeverSleep => default;
        public static SleepTimeout SystemSetting => default;
    }

    public static class SystemInfo
    {
        public static int systemMemorySize => 4096;
        public static int graphicsMemorySize => 2048;
        public static int processorCount => 8;
        public static int graphicsShaderLevel => 45;
        public static string deviceModel => "";
        public static string graphicsDeviceName => "";
        public static bool supportsInstancing => true;
    }

    public static class PlayerPrefs
    {
        public static void SetInt(string k, int v) { }
        public static int GetInt(string k, int d = 0) => d;
        public static void SetFloat(string k, float v) { }
        public static float GetFloat(string k, float d = 0f) => d;
        public static void SetString(string k, string v) { }
        public static string GetString(string k, string d = "") => d;
        public static void Save() { }
        public static bool HasKey(string k) => false;
    }

    public static class JsonUtility
    {
        public static string ToJson(object o) => "";
        public static string ToJson(object o, bool pretty) => "";
        public static T FromJson<T>(string json) => default;
        public static void FromJsonOverwrite(string json, object target) { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
        public static void UnloadUnusedAssets() { }
    }

    public static class Random
    {
        public static float value => 0f;
        public static Vector3 insideUnitSphere => default;
        public static Vector2 insideUnitCircle => default;
        public static Vector3 onUnitSphere => default;
        public static Quaternion rotation => default;
        public static float Range(float a, float b) => a;
        public static int Range(int a, int b) => a;
        public static void InitState(int seed) { }
    }
}
