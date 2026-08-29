// Minimal stand-in for the Unity API surface used by San Monica.
// Exists only so the whole game can be type-checked outside the editor.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => MathF.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector2(x / m, y / m) : default; } }
        public void Normalize() { var n = normalized; x = n.x; y = n.y; }
        public static Vector2 zero => default;
        public static Vector2 one => new Vector2(1f, 1f);
        public static Vector2 up => new Vector2(0f, 1f);
        public static Vector2 right => new Vector2(1f, 0f);
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 ClampMagnitude(Vector2 a, float m) { float mag = a.magnitude; return mag > m && mag > 1e-6f ? a * (m / mag) : a; }
        public static Vector2 MoveTowards(Vector2 a, Vector2 b, float d) { var to = b - a; float m = to.magnitude; return m <= d || m < 1e-6f ? b : a + to * (d / m); }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
        public static Vector2 operator *(float b, Vector2 a) => new Vector2(a.x * b, a.y * b);
        public static Vector2 operator /(Vector2 a, float b) => new Vector2(a.x / b, a.y / b);
        public static bool operator ==(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object o) => o is Vector2 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => $"({x:0.##}, {y:0.##})";
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => default;
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
        public override bool Equals(object o) => o is Vector2Int v && this == v;
        public override int GetHashCode() => x * 73856093 ^ y * 19349663;
        public override string ToString() => $"({x}, {y})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0f; }
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector3(x / m, y / m, z / m) : default; } }
        public void Normalize() { var n = normalized; x = n.x; y = n.y; z = n.z; }
        public void Set(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float Angle(Vector3 a, Vector3 b)
        {
            float d = a.magnitude * b.magnitude;
            return d < 1e-6f ? 0f : MathF.Acos(Math.Clamp(Dot(a, b) / d, -1f, 1f)) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis)
            => Angle(a, b) * MathF.Sign(Dot(axis, Cross(a, b)));
        public static Vector3 Cross(Vector3 a, Vector3 b)
            => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => Lerp(a, b, t);
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d)
        {
            var to = b - a; float m = to.magnitude;
            return m <= d || m < 1e-6f ? b : a + to * (d / m);
        }
        public static Vector3 ClampMagnitude(Vector3 a, float m)
        {
            float mag = a.magnitude;
            return mag > m && mag > 1e-6f ? a * (m / mag) : a;
        }
        public static Vector3 ProjectOnPlane(Vector3 a, Vector3 n)
        {
            float sq = n.sqrMagnitude;
            return sq < 1e-6f ? a : a - n * (Dot(a, n) / sq);
        }
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 SmoothDamp(Vector3 a, Vector3 b, ref Vector3 v, float t) => b;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x * b, a.y * b, a.z * b);
        public static Vector3 operator *(float b, Vector3 a) => new Vector3(a.x * b, a.y * b, a.z * b);
        public static Vector3 operator /(Vector3 a, float b) => new Vector3(a.x / b, a.y / b, a.z / b);
        public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public override string ToString() => $"({x:0.##}, {y:0.##}, {z:0.##})";
        public string ToString(string format) => $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)})";
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => default;
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector3 eulerAngles { get => default; set { } }
        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion Euler(Vector3 e) => default;
        public static Quaternion LookRotation(Vector3 forward) => default;
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => default;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a;
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => a;
        public static Quaternion AngleAxis(float angle, Vector3 axis) => default;
        public static Quaternion Inverse(Quaternion q) => q;
        public static float Angle(Quaternion a, Quaternion b) => 0f;
        public static Quaternion operator *(Quaternion a, Quaternion b) => a;
        public static Vector3 operator *(Quaternion a, Vector3 b) => b;
        public static bool operator ==(Quaternion a, Quaternion b) => true;
        public static bool operator !=(Quaternion a, Quaternion b) => false;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
    }

    public struct Matrix4x4
    {
        public static Matrix4x4 identity => default;
        public Vector3 MultiplyPoint3x4(Vector3 p) => p;
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => a;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => default;
        public static Color black => default;
        public static Color clear => default;
        public static Color grey => default;
        public static Color gray => default;
        public static Color red => default;
        public static Color green => default;
        public static Color blue => default;
        public static Color yellow => default;
        public static Color magenta => default;
        public static Color cyan => default;
        public static Color Lerp(Color a, Color b, float t) => a;
        public static Color HSVToRGB(float h, float s, float v) => default;
        public static Color operator *(Color a, float b) => a;
        public static Color operator *(Color a, Color b) => a;
        public static Color operator +(Color a, Color b) => a;
        public static implicit operator Color32(Color c) => default;
        public override string ToString() => "";
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => default;
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public Vector2 center => default;
        public bool Contains(Vector2 p) => false;
    }

    public struct Bounds
    {
        public Vector3 center, size, extents, min, max;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; extents = size; min = center; max = center; }
        public bool Contains(Vector3 p) => false;
        public void Encapsulate(Vector3 p) { }
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; }
        public Vector3 GetPoint(float d) => origin;
    }

    public struct Plane
    {
        public Plane(Vector3 normal, Vector3 point) { }
        public bool Raycast(Ray ray, out float enter) { enter = 0f; return false; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public const float Epsilon = 1e-5f;
        public static float Abs(float v) => MathF.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Sin(float v) => MathF.Sin(v);
        public static float Cos(float v) => MathF.Cos(v);
        public static float Tan(float v) => MathF.Tan(v);
        public static float Asin(float v) => MathF.Asin(v);
        public static float Acos(float v) => MathF.Acos(v);
        public static float Atan(float v) => MathF.Atan(v);
        public static float Atan2(float y, float x) => MathF.Atan2(y, x);
        public static float Sqrt(float v) => MathF.Sqrt(v);
        public static float Pow(float a, float b) => MathF.Pow(a, b);
        public static float Exp(float v) => MathF.Exp(v);
        public static float Log(float v) => MathF.Log(v);
        public static float Floor(float v) => MathF.Floor(v);
        public static float Ceil(float v) => MathF.Ceiling(v);
        public static int FloorToInt(float v) => (int)MathF.Floor(v);
        public static int CeilToInt(float v) => (int)MathF.Ceiling(v);
        public static int RoundToInt(float v) => (int)MathF.Round(v, MidpointRounding.ToEven);
        public static float Round(float v) => MathF.Round(v, MidpointRounding.ToEven);
        public static float Sign(float v) => v >= 0f ? 1f : -1f;
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static float Min(params float[] v) { float m = float.PositiveInfinity; foreach (var f in v) m = MathF.Min(m, f); return m; }
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Max(params float[] v) { float m = float.NegativeInfinity; foreach (var f in v) m = MathF.Max(m, f); return m; }
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpAngle(float a, float b, float t) { float d = Repeat(b - a, 360f); if (d > 180f) d -= 360f; return a + d * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => MathF.Abs(b - a) < 1e-9f ? 0f : Clamp01((v - a) / (b - a));
        public static float MoveTowards(float a, float b, float d) => MathF.Abs(b - a) <= d ? b : a + Sign(b - a) * d;
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = t * t * (3f - 2f * t); return a + (b - a) * t; }
        public static float SmoothDamp(float a, float b, ref float v, float t) => b;
        public static float SmoothDamp(float a, float b, ref float v, float t, float maxSpeed, float dt) => b;
        public static float Repeat(float t, float length) => Clamp(t - MathF.Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - MathF.Abs(t - length); }
        public static float DeltaAngle(float a, float b) { float d = Repeat(b - a, 360f); if (d > 180f) d -= 360f; return d; }
        public static bool Approximately(float a, float b) => MathF.Abs(b - a) < 1e-5f;
        public static int ClosestPowerOfTwo(int v)
        {
            if (v <= 1) return 1;
            int lower = 1; while (lower * 2 < v) lower *= 2;
            int upper = lower * 2;
            return (v - lower) < (upper - v) ? lower : upper;
        }
        public static int NextPowerOfTwo(int v) { int p = 1; while (p < v) p *= 2; return p; }
        public static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;
        public static float PerlinNoise(float x, float y)
        {
            // Value noise is enough: nothing in the game relies on Unity's exact
            // Perlin, and the project has its own noise for anything that does.
            float n = MathF.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return n - MathF.Floor(n);
        }
    }

    public class AnimationCurve
    {
        public AnimationCurve() { }
        public AnimationCurve(params Keyframe[] keys) { }
        public float Evaluate(float t) => 0f;
        public static AnimationCurve EaseInOut(float a, float b, float c, float d) => new AnimationCurve();
        public static AnimationCurve Linear(float a, float b, float c, float d) => new AnimationCurve();
    }

    public struct Keyframe
    {
        public Keyframe(float time, float value) { }
    }
}
