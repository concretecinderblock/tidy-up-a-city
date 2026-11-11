using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Generates placeholder feedback sounds at startup. Replaced by FMOD events once FMOD is integrated.
    /// </summary>
    public static class ToneSynth
    {
        private const int SampleRate = 44100;

        /// <summary>Soft bell: fundamental plus two quiet harmonics, fast attack, exponential decay.</summary>
        public static AudioClip Bell(string name, float frequency, float duration, float decay)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Attack(t, 0.004f) * Mathf.Exp(-decay * t);
                float w = 2f * Mathf.PI * frequency * t;
                data[i] = envelope * (Mathf.Sin(w) + 0.3f * Mathf.Sin(2f * w) + 0.12f * Mathf.Sin(3f * w)) * 0.6f;
            }
            return Create(name, data);
        }

        /// <summary>Gentle low "nope": a short downward glide, rounded off.</summary>
        public static AudioClip SoftReject(string name, float startFrequency, float endFrequency, float duration)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float k = t / duration;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, k);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float envelope = Attack(t, 0.01f) * Mathf.Sin(Mathf.PI * k);
                data[i] = envelope * Mathf.Sin(phase) * 0.45f;
            }
            return Create(name, data);
        }

        /// <summary>
        /// A soft brush stroke for scrubbing: low-passed noise with a gentle swell and fade. Seeded, so the
        /// clip is the same every run.
        /// </summary>
        public static AudioClip Brush(string name, float duration, float brightness)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            var random = new System.Random(7);
            float low = 0f, lower = 0f;
            float cutoff = Mathf.Clamp01(brightness); // 0 = dull, 1 = bright
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += (noise - low) * Mathf.Lerp(0.05f, 0.35f, cutoff);   // two one-pole filters: soft "shhh"
                lower += (low - lower) * Mathf.Lerp(0.08f, 0.5f, cutoff);
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));   // swell in, fade out
                data[i] = lower * envelope * 0.9f;
            }
            return Create(name, data);
        }

        private static float Attack(float t, float attackTime) => t >= attackTime ? 1f : t / attackTime;

        private static AudioClip Create(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
