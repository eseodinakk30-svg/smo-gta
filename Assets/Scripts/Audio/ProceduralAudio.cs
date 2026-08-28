using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Audio
{
    /// <summary>
    /// Every sound in San Monica is synthesised here at runtime - engines,
    /// gunfire, footsteps, sirens, rain, thunder and interface tones. Nothing is
    /// sampled, so the whole soundscape is original and ships as code.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 22050;

        // ---------------- primitives ----------------
        private static float Noise(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xFFFFFF) / 8388608f - 1f;
        }

        private static void Normalise(float[] data, float peak = 0.92f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-5f) return;
            float scale = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }

        private static AudioClip Make(string name, float[] data, bool loop)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>One pole low pass, used to shape noise into wind, rain and impacts.</summary>
        private static void LowPass(float[] data, float cutoff)
        {
            float a = Mathf.Clamp01(cutoff);
            float prev = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                prev += a * (data[i] - prev);
                data[i] = prev;
            }
        }

        private static void HighPass(float[] data, float cutoff)
        {
            float a = Mathf.Clamp01(cutoff);
            float prev = 0f, prevOut = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                prevOut = a * (prevOut + x - prev);
                prev = x;
                data[i] = prevOut;
            }
        }

        // ---------------- weapons ----------------
        public static AudioClip Gunshot(float pitch, float body, float tail, int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * (0.18f + tail * 0.55f));
            var data = new float[length];
            uint state = (uint)(seed * 2654435761u + 17u);

            float bodyFreq = 140f * pitch;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Exp(-t * 46f);
                float bodyEnv = Mathf.Exp(-t * (16f / Mathf.Max(0.2f, body)));
                float tailEnv = Mathf.Exp(-t * (3.2f / Mathf.Max(0.15f, tail)));

                float crack = Noise(ref state) * attack * 1.15f;
                float thump = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * bodyEnv * body * 0.85f;
                float rumble = Noise(ref state) * tailEnv * tail * 0.35f;
                data[i] = crack + thump + rumble;
            }
            LowPass(data, 0.55f);
            Normalise(data);
            return Make("Gunshot", data, false);
        }

        public static AudioClip EngineLoop(float baseHz, float harshness, int cylinders, int seed)
        {
            // One full cycle at the base frequency so the loop is seamless.
            float cycles = 8f;
            int length = Mathf.RoundToInt(SampleRate * cycles / Mathf.Max(20f, baseHz));
            var data = new float[length];
            uint state = (uint)(seed * 40503u + 7u);

            for (int i = 0; i < length; i++)
            {
                float phase = (i / (float)length) * cycles * Mathf.PI * 2f;
                float value = 0f;
                for (int h = 1; h <= 9; h++)
                {
                    float amplitude = 1f / (h * (1f + harshness * 0.6f));
                    if (h % 2 == 0) amplitude *= 0.55f + harshness * 0.4f;
                    value += Mathf.Sin(phase * h + h * 0.35f) * amplitude;
                }
                // Firing pulses give the engine its character.
                float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(phase * cylinders * 0.5f)), 6f) * harshness;
                value = value * 0.55f + pulse * 0.5f + Noise(ref state) * 0.06f * harshness;
                data[i] = value;
            }
            LowPass(data, 0.35f);
            Normalise(data, 0.75f);
            return Make("EngineLoop", data, true);
        }

        public static AudioClip Siren(int seed)
        {
            int length = SampleRate * 2;
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float sweep = Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                float freq = 700f + sweep * 420f;
                float value = Mathf.Sin(2f * Mathf.PI * freq * t);
                value += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.25f;
                data[i] = value * 0.6f;
            }
            Normalise(data, 0.8f);
            return Make("Siren", data, true);
        }

        public static AudioClip Horn(float baseHz)
        {
            int length = Mathf.RoundToInt(SampleRate * 0.55f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t * 40f) * Mathf.Exp(-t * 2.2f);
                float value = Mathf.Sin(2f * Mathf.PI * baseHz * t) + Mathf.Sin(2f * Mathf.PI * baseHz * 1.26f * t) * 0.8f;
                value += Mathf.Sin(2f * Mathf.PI * baseHz * 2f * t) * 0.3f;
                data[i] = value * env;
            }
            Normalise(data, 0.85f);
            return Make("Horn", data, false);
        }

        public static AudioClip Footstep(int seed, bool hard)
        {
            int length = Mathf.RoundToInt(SampleRate * 0.16f);
            var data = new float[length];
            uint state = (uint)(seed * 22695477u + 3u);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (hard ? 42f : 60f));
                data[i] = Noise(ref state) * env;
                if (hard) data[i] += Mathf.Sin(2f * Mathf.PI * 90f * t) * env * 0.5f;
            }
            LowPass(data, hard ? 0.30f : 0.18f);
            Normalise(data, 0.55f);
            return Make("Footstep", data, false);
        }

        public static AudioClip Impact(float weight, int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * (0.22f + weight * 0.4f));
            var data = new float[length];
            uint state = (uint)(seed * 69069u + 11u);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (18f - weight * 10f));
                float metal = Mathf.Sin(2f * Mathf.PI * (220f - weight * 90f) * t) * env;
                data[i] = (Noise(ref state) * 0.7f + metal * 0.6f) * env;
            }
            LowPass(data, 0.4f);
            Normalise(data, 0.85f);
            return Make("Impact", data, false);
        }

        public static AudioClip Explosion(int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * 2.2f);
            var data = new float[length];
            uint state = (uint)(seed * 1103515245u + 12345u);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Exp(-t * 8f);
                float body = Mathf.Exp(-t * 1.6f);
                float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 24f, Mathf.Clamp01(t)) * t) * body;
                data[i] = Noise(ref state) * (attack * 0.9f + body * 0.4f) + sub * 0.8f;
            }
            LowPass(data, 0.22f);
            Normalise(data);
            return Make("Explosion", data, false);
        }

        public static AudioClip Rain(int seed)
        {
            int length = SampleRate * 4;
            var data = new float[length];
            uint state = (uint)(seed * 214013u + 2531011u);
            for (int i = 0; i < length; i++) data[i] = Noise(ref state);
            HighPass(data, 0.42f);
            LowPass(data, 0.62f);
            // Crossfade the ends so the loop is seamless.
            int fade = SampleRate / 4;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = Mathf.Lerp(data[length - fade + i], data[i], k);
            }
            Normalise(data, 0.6f);
            return Make("Rain", data, true);
        }

        public static AudioClip Wind(int seed)
        {
            int length = SampleRate * 5;
            var data = new float[length];
            uint state = (uint)(seed * 8253729u + 2396403u);
            float prev = 0f;
            for (int i = 0; i < length; i++)
            {
                float n = Noise(ref state);
                prev += 0.012f * (n - prev);
                float t = i / (float)SampleRate;
                float gust = 0.6f + 0.4f * Mathf.Sin(t * 0.35f) * Mathf.Sin(t * 0.11f + 1.2f);
                data[i] = prev * gust;
            }
            int fade = SampleRate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = Mathf.Lerp(data[length - fade + i], data[i], k);
            }
            Normalise(data, 0.5f);
            return Make("Wind", data, true);
        }

        public static AudioClip Thunder(int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * 3.4f);
            var data = new float[length];
            uint state = (uint)(seed * 1664525u + 1013904223u);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float roll = Mathf.Exp(-t * 0.9f) * (0.7f + 0.3f * Mathf.Sin(t * 5.5f));
                data[i] = Noise(ref state) * roll;
            }
            LowPass(data, 0.12f);
            Normalise(data, 0.9f);
            return Make("Thunder", data, false);
        }

        public static AudioClip CityAmbience(int seed, float density)
        {
            int length = SampleRate * 6;
            var data = new float[length];
            uint state = (uint)(seed * 22695477u + 1u);
            float prev = 0f;
            for (int i = 0; i < length; i++)
            {
                float n = Noise(ref state);
                prev += 0.02f * (n - prev);
                float t = i / (float)SampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.10f + Mathf.Sin(2f * Mathf.PI * 118f * t) * 0.05f;
                data[i] = prev * (0.5f + density * 0.5f) + hum * density;
            }
            int fade = SampleRate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = Mathf.Lerp(data[length - fade + i], data[i], k);
            }
            Normalise(data, 0.45f);
            return Make("CityAmbience", data, true);
        }

        public static AudioClip UiTone(float frequency, float duration, float bend, bool square)
        {
            int length = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float f = frequency * Mathf.Pow(2f, bend * t);
                float phase = 2f * Mathf.PI * f * t;
                float value = square ? Mathf.Sign(Mathf.Sin(phase)) * 0.5f : Mathf.Sin(phase);
                float env = Mathf.Min(1f, t * 90f) * Mathf.Exp(-t * 6.5f);
                data[i] = value * env;
            }
            Normalise(data, 0.55f);
            return Make("UiTone", data, false);
        }

        public static AudioClip Splash(int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * 0.8f);
            var data = new float[length];
            uint state = (uint)(seed * 4093u + 5u);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 6f);
                data[i] = Noise(ref state) * env;
            }
            HighPass(data, 0.3f);
            LowPass(data, 0.55f);
            Normalise(data, 0.7f);
            return Make("Splash", data, false);
        }

        public static AudioClip Skid(int seed)
        {
            int length = SampleRate * 2;
            var data = new float[length];
            uint state = (uint)(seed * 6364136u + 9u);
            float prev = 0f;
            for (int i = 0; i < length; i++)
            {
                float n = Noise(ref state);
                prev += 0.09f * (n - prev);
                float t = i / (float)SampleRate;
                data[i] = prev * (0.8f + 0.2f * Mathf.Sin(t * 22f));
            }
            HighPass(data, 0.25f);
            int fade = SampleRate / 4;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = Mathf.Lerp(data[length - fade + i], data[i], k);
            }
            Normalise(data, 0.6f);
            return Make("Skid", data, true);
        }

        public static AudioClip Scream(int seed)
        {
            int length = Mathf.RoundToInt(SampleRate * 1.1f);
            var data = new float[length];
            uint state = (uint)(seed * 12345u + 77u);
            float baseFreq = 320f + (seed % 7) * 30f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t * 22f) * Mathf.Exp(-t * 2.6f);
                float vibrato = Mathf.Sin(2f * Mathf.PI * 6.5f * t) * 22f;
                float f = baseFreq + vibrato + t * 60f;
                float value = Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f
                            + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f
                            + Mathf.Sin(2f * Mathf.PI * f * 3f * t) * 0.12f
                            + Noise(ref state) * 0.10f;
                data[i] = value * env;
            }
            Normalise(data, 0.6f);
            return Make("Scream", data, false);
        }
    }
}
