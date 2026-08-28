using UnityEngine;

namespace SanMonica.Core
{
    /// <summary>
    /// Deterministic, allocation free pseudo random generator (xorshift128).
    /// Every piece of procedural world content derives from a seed so that the
    /// same world is produced on every device and after every save/load.
    /// </summary>
    public struct Rng
    {
        private uint _x, _y, _z, _w;

        public Rng(int seed)
        {
            uint s = (uint)seed;
            if (s == 0) s = 0x9E3779B9u;
            _x = s;
            _y = s * 1812433253u + 1u;
            _z = _y * 1812433253u + 1u;
            _w = _z * 1812433253u + 1u;
            for (int i = 0; i < 8; i++) NextUInt();
        }

        public static Rng FromCoords(int seed, int x, int y, int salt = 0)
        {
            unchecked
            {
                int h = seed;
                h = h * 73856093 ^ x * 19349663 ^ y * 83492791 ^ salt * 668265261;
                return new Rng(h);
            }
        }

        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float Value => (NextUInt() & 0xFFFFFF) / 16777216f;

        public float Range(float min, float max) => min + Value * (max - min);

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability) => Value < probability;

        public T Pick<T>(T[] items) => items[Range(0, items.Length)];

        public T Pick<T>(System.Collections.Generic.IList<T> items) => items[Range(0, items.Count)];

        public Vector2 InsideUnitCircle()
        {
            float a = Value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        public Vector3 OnUnitSphere()
        {
            float z = Range(-1f, 1f);
            float a = Value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
        }

        /// <summary>Gaussian-ish value centred on 0 with roughly unit deviation.</summary>
        public float Gaussian() => (Value + Value + Value - 1.5f) * 1.1547f;
    }
}
