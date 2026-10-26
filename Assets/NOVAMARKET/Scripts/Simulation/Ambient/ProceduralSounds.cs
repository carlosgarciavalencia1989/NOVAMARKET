using System;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// Temporary placeholder sounds synthesised in plain C# (mono samples in -1..1), used until the real CC0
    /// recordings (Kenney packs) are set in the audio profile. Every loop starts and ends on the same value, so it
    /// repeats without clicks.
    /// </summary>
    public static class ProceduralSounds
    {
        public const int SampleRate = 22050;

        /// <summary>Wind in the open: soft hiss whose loudness breathes in and out.</summary>
        public static float[] Wind(float seconds = 8f, int seed = 2)
        {
            var random = new Random(seed);
            float[] samples = Allocate(seconds);
            float band = 0f, band2 = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                band += (Noise(random) - band) * 0.12f;
                band2 += (band - band2) * 0.3f;
                float breath = 0.55f + 0.3f * Cycle(i, samples.Length, 3) + 0.15f * Cycle(i, samples.Length, 7);
                samples[i] = band2 * breath;
            }
            return Finish(samples, 0.45f);
        }

        /// <summary>
        /// Engine at idle: a buzzing fundamental with a few harmonics and some roughness. The scene raises its
        /// pitch with the speed; diesel engines use a lower <paramref name="frequency"/>.
        /// </summary>
        public static float[] Engine(float frequency, float seconds = 1f, float roughness = 0.25f, int seed = 3)
        {
            var random = new Random(seed);
            float[] samples = Allocate(seconds);
            // Whole cycles in the loop, so it repeats cleanly.
            int cycles = Math.Max(1, (int)Math.Round(frequency * samples.Length / (double)SampleRate));
            float rough = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                double phase = 2.0 * Math.PI * cycles * i / samples.Length;
                rough += (Noise(random) - rough) * 0.2f;
                double tone = Math.Sin(phase) + 0.5 * Math.Sin(2 * phase) + 0.3 * Math.Sin(3 * phase + 0.5) + 0.15 * Math.Sin(5 * phase);
                double pulse = 0.7 + 0.3 * Math.Sin(phase * 0.5); // firing beat
                samples[i] = (float)(tone * pulse / 1.95) * (1f - roughness) + rough * roughness * 2f;
            }
            return Finish(samples, 0.6f);
        }

        /// <summary>Metro running: electric whine over the rumble of the wheels on the viaduct.</summary>
        public static float[] Metro(float seconds = 4f, int seed = 4)
        {
            var random = new Random(seed);
            float[] samples = Allocate(seconds);
            int whineCycles = (int)Math.Round(420.0 * seconds);
            float rumble = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                rumble += (Noise(random) - rumble) * 0.05f;
                double whine = Math.Sin(2.0 * Math.PI * whineCycles * i / samples.Length);
                float clack = Cycle(i, samples.Length, 8) > 0.9f ? Noise(random) * 0.3f : 0f; // rail joints
                samples[i] = rumble * 1.6f + (float)whine * 0.18f + clack;
            }
            return Finish(samples, 0.55f);
        }

        /// <summary>Reverse beeper of a truck or forklift: 0.5 s beep at about 1 kHz, 0.5 s silence.</summary>
        public static float[] ReverseBeep(int seed = 5)
        {
            float[] samples = Allocate(1f);
            int half = samples.Length / 2;
            int cycles = (int)Math.Round(1040.0 * half / SampleRate);
            for (int i = 0; i < half; i++)
            {
                float envelope = Math.Min(1f, Math.Min(i, half - i) / 200f); // no clicks at the edges
                samples[i] = (float)Math.Sin(2.0 * Math.PI * cycles * i / half) * envelope;
            }
            return Finish(samples, 0.5f);
        }

        // ------------------------------------------------------------------ helpers

        private static float[] Allocate(float seconds) => new float[Math.Max(1, (int)(seconds * SampleRate))];

        private static float Noise(Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        /// <summary>A slow wave, 0..1, with whole periods over the loop.</summary>
        private static float Cycle(int i, int length, int periods) =>
            (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * periods * i / length));

        /// <summary>Crossfades the last 5 % into the start (seamless loop), removes any offset and normalises the peak.</summary>
        private static float[] Finish(float[] samples, float peak)
        {
            int n = samples.Length, fade = n / 20;
            for (int k = 0; k < fade; k++)
            {
                float t = (float)k / fade;
                samples[k] = samples[k] * t + samples[n - fade + k] * (1f - t);
            }
            Array.Resize(ref samples, n - fade);

            double mean = 0;
            foreach (float s in samples) mean += s;
            mean /= samples.Length;
            float max = 1e-6f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] -= (float)mean;
                max = Math.Max(max, Math.Abs(samples[i]));
            }
            float gain = peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            return samples;
        }
    }
}
