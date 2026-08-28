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
        public float magnitude => 0f;
        public float sqrMagnitude => 0f;
        public Vector2 normalized => this;
        public void Normalize() { }
        public static Vector2 zero => default;
        public static Vector2 one => default;
        public static Vector2 up => default;
        public static Vector2 right => default;
        public static float Distance(Vector2 a, Vector2 b) => 0f;
        public static float Dot(Vector2 a, Vector2 b) => 0f;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a;
        public static Vector2 ClampMagnitude(Vector2 a, float m) => a;
        public static Vector2 MoveTowards(Vector2 a, Vector2 b, float d) => a;
        public static Vector2 operator +(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a) => a;
        public static Vector2 operator *(Vector2 a, float b) => a;
        public static Vector2 operator *(float b, Vector2 a) => a;
        public static Vector2 operator /(Vector2 a, float b) => a;
        public static bool operator ==(Vector2 a, Vector2 b) => true;
        public static bool operator !=(Vector2 a, Vector2 b) => false;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
        public override string ToString() => "";
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => default;
        public static bool operator ==(Vector2Int a, Vector2Int b) => true;
        public static bool operator !=(Vector2Int a, Vector2Int b) => false;
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => a;
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => a;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
        public override string ToString() => "";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0f; }
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude => 0f;
        public float sqrMagnitude => 0f;
        public Vector3 normalized => this;
        public void Normalize() { }
        public void Set(float a, float b, float c) { }
        public static Vector3 zero => default;
        public static Vector3 one => default;
        public static Vector3 up => default;
        public static Vector3 down => default;
        public static Vector3 left => default;
        public static Vector3 right => default;
        public static Vector3 forward => default;
        public static Vector3 back => default;
        public static float Distance(Vector3 a, Vector3 b) => 0f;
        public static float Dot(Vector3 a, Vector3 b) => 0f;
        public static float Angle(Vector3 a, Vector3 b) => 0f;
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis) => 0f;
        public static Vector3 Cross(Vector3 a, Vector3 b) => a;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) => a;
        public static Vector3 ClampMagnitude(Vector3 a, float m) => a;
        public static Vector3 ProjectOnPlane(Vector3 a, Vector3 n) => a;
        public static Vector3 Scale(Vector3 a, Vector3 b) => a;
        public static Vector3 SmoothDamp(Vector3 a, Vector3 b, ref Vector3 v, float t) => a;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a) => a;
        public static Vector3 operator *(Vector3 a, float b) => a;
        public static Vector3 operator *(float b, Vector3 a) => a;
        public static Vector3 operator /(Vector3 a, float b) => a;
        public static bool operator ==(Vector3 a, Vector3 b) => true;
        public static bool operator !=(Vector3 a, Vector3 b) => false;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
        public override string ToString() => "";
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
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public const float Epsilon = 1e-5f;
        public static float Abs(float v) => 0f;
        public static int Abs(int v) => 0;
        public static float Sin(float v) => 0f;
        public static float Cos(float v) => 0f;
        public static float Tan(float v) => 0f;
        public static float Asin(float v) => 0f;
        public static float Acos(float v) => 0f;
        public static float Atan(float v) => 0f;
        public static float Atan2(float y, float x) => 0f;
        public static float Sqrt(float v) => 0f;
        public static float Pow(float a, float b) => 0f;
        public static float Exp(float v) => 0f;
        public static float Log(float v) => 0f;
        public static float Floor(float v) => 0f;
        public static float Ceil(float v) => 0f;
        public static int FloorToInt(float v) => 0;
        public static int CeilToInt(float v) => 0;
        public static int RoundToInt(float v) => 0;
        public static float Round(float v) => 0f;
        public static float Sign(float v) => 0f;
        public static float Min(float a, float b) => 0f;
        public static float Min(params float[] v) => 0f;
        public static int Min(int a, int b) => 0;
        public static float Max(float a, float b) => 0f;
        public static float Max(params float[] v) => 0f;
        public static int Max(int a, int b) => 0;
        public static float Clamp(float v, float a, float b) => 0f;
        public static int Clamp(int v, int a, int b) => 0;
        public static float Clamp01(float v) => 0f;
        public static float Lerp(float a, float b, float t) => 0f;
        public static float LerpAngle(float a, float b, float t) => 0f;
        public static float LerpUnclamped(float a, float b, float t) => 0f;
        public static float InverseLerp(float a, float b, float v) => 0f;
        public static float MoveTowards(float a, float b, float d) => 0f;
        public static float SmoothStep(float a, float b, float t) => 0f;
        public static float SmoothDamp(float a, float b, ref float v, float t) => 0f;
        public static float SmoothDamp(float a, float b, ref float v, float t, float maxSpeed, float dt) => 0f;
        public static float Repeat(float t, float length) => 0f;
        public static float PingPong(float t, float length) => 0f;
        public static float DeltaAngle(float a, float b) => 0f;
        public static int ClosestPowerOfTwo(int v) => 0;
        public static bool Approximately(float a, float b) => false;
        public static float PerlinNoise(float x, float y) => 0f;
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
