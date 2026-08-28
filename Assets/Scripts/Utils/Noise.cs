using UnityEngine;

namespace SanMonica.Utils
{
    /// <summary>Deterministic gradient / value noise used by terrain, textures and weather.</summary>
    public static class Noise
    {
        private static readonly int[] Perm = new int[512];

        static Noise()
        {
            var rng = new SanMonica.Core.Rng(1337);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) Perm[i] = p[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>Classic 2D perlin noise in the range [-1,1].</summary>
        public static float Perlin(float x, float y)
        {
            int xi = Mathf.FloorToInt(x) & 255;
            int yi = Mathf.FloorToInt(y) & 255;
            float xf = x - Mathf.Floor(x);
            float yf = y - Mathf.Floor(y);
            float u = Fade(xf), v = Fade(yf);

            int aa = Perm[Perm[xi] + yi];
            int ab = Perm[Perm[xi] + yi + 1];
            int ba = Perm[Perm[xi + 1] + yi];
            int bb = Perm[Perm[xi + 1] + yi + 1];

            float x1 = Mathf.Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = Mathf.Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Mathf.Lerp(x1, x2, v);
        }

        /// <summary>Fractal brownian motion, range roughly [-1,1].</summary>
        public static float Fbm(float x, float y, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(x * freq, y * freq) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Ridged noise, good for mountain silhouettes. Range [0,1].</summary>
        public static float Ridged(float x, float y, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin(x * freq, y * freq));
                sum += n * n * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Cheap hash based white noise in [0,1].</summary>
        public static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Worley / cellular noise F1 distance normalised to [0,1].</summary>
        public static float Worley(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            float best = 8f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                float px = ox + Hash(xi + ox, yi + oy);
                float py = oy + Hash(xi + ox + 31.7f, yi + oy + 17.3f);
                float dx = px - fx, dy = py - fy;
                float d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            return Mathf.Clamp01(Mathf.Sqrt(best));
        }
    }
}
