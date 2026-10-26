using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>A note of the melody: when it starts (s), its MIDI pitch (60 = middle C) and how hard it is plucked (0..1).</summary>
    public readonly struct MusicNote
    {
        public MusicNote(float start, int midi, float velocity)
        {
            Start = start;
            Midi = midi;
            Velocity = velocity;
        }

        public float Start { get; }
        public int Midi { get; }
        public float Velocity { get; }
    }

    /// <summary>A short calm piece: a chord progression (one chord every <see cref="ChordSeconds"/>) and a sparse melody.</summary>
    public sealed class MusicScore
    {
        public MusicScore(IReadOnlyList<int[]> chords, float chordSeconds, IReadOnlyList<MusicNote> melody)
        {
            Chords = chords;
            ChordSeconds = chordSeconds;
            Melody = melody;
        }

        /// <summary>Each chord as MIDI pitches, root first.</summary>
        public IReadOnlyList<int[]> Chords { get; }

        public float ChordSeconds { get; }
        public IReadOnlyList<MusicNote> Melody { get; }

        /// <summary>Length of the piece including the last chord's fade out, seconds.</summary>
        public float Seconds => Chords.Count * ChordSeconds + ProceduralMusic.ReleaseSeconds;
    }

    /// <summary>
    /// Temporary background music composed by the game, in the calm, sparse mood of historical strategy games:
    /// a soft string pad and a low drone in D minor with a few plucked notes (harp / lute) over it. Plain C#: the
    /// score is chosen from a seed and synthesised to mono samples; real CC0 tracks can replace it in the audio
    /// profile.
    /// </summary>
    public static class ProceduralMusic
    {
        public const int SampleRate = ProceduralSounds.SampleRate;
        public const float AttackSeconds = 2f;
        public const float ReleaseSeconds = 3f;

        /// <summary>Pitch classes of D natural minor (D E F G A B♭ C).</summary>
        public static readonly int[] Scale = { 2, 4, 5, 7, 9, 10, 0 };

        // Chords as pitch classes (root first): i, VI, III, VII, iv, v.
        private static readonly int[][] ChordShapes =
        {
            new[] { 2, 5, 9 },   // Dm
            new[] { 10, 2, 5 },  // B♭
            new[] { 5, 9, 0 },   // F
            new[] { 0, 4, 7 },   // C
            new[] { 7, 10, 2 },  // Gm
            new[] { 9, 0, 4 }    // Am
        };

        // Progressions that start and end at home (Dm), as indices into ChordShapes.
        private static readonly int[][] Progressions =
        {
            new[] { 0, 1, 2, 3, 0, 4, 5, 0 },
            new[] { 0, 3, 1, 2, 4, 5, 3, 0 },
            new[] { 0, 4, 1, 5, 2, 3, 1, 0 }
        };

        public static MusicScore Compose(int seed, float chordSeconds = 6f)
        {
            var random = new Random(seed);
            int[] progression = Progressions[random.Next(Progressions.Length)];
            var chords = new List<int[]>();
            var melody = new List<MusicNote>();
            const float beat = 0.75f;
            int beatsPerChord = Math.Max(1, (int)(chordSeconds / beat));

            int previous = 69; // A4
            for (int c = 0; c < progression.Length; c++)
            {
                int[] shape = ChordShapes[progression[c]];
                chords.Add(Voice(shape));

                // Two or three notes per chord, on free beats, never in the first beat (let the chord breathe).
                int count = 1 + random.Next(3);
                var used = new HashSet<int>();
                for (int n = 0; n < count; n++)
                {
                    int slot = 1 + random.Next(beatsPerChord - 1);
                    if (!used.Add(slot)) continue;
                    bool chordTone = random.NextDouble() < 0.75;
                    int pitchClass = chordTone ? shape[random.Next(shape.Length)] : Scale[random.Next(Scale.Length)];
                    int midi = Nearest(pitchClass, previous, 62, 81);
                    previous = midi;
                    float velocity = 0.55f + 0.35f * (float)random.NextDouble();
                    melody.Add(new MusicNote(c * chordSeconds + slot * beat, midi, velocity));
                }
            }
            melody.Sort((a, b) => a.Start.CompareTo(b.Start));
            return new MusicScore(chords, chordSeconds, melody);
        }

        /// <summary>Synthesises the piece: string pad, low drone, plucked melody and a soft echo; peak about 0.5.</summary>
        public static float[] Render(MusicScore score)
        {
            int length = (int)(score.Seconds * SampleRate);
            var mix = new float[length];
            for (int c = 0; c < score.Chords.Count; c++)
            {
                float start = c * score.ChordSeconds;
                int[] chord = score.Chords[c];
                for (int k = 0; k < chord.Length; k++)
                    AddPad(mix, start, score.ChordSeconds, Frequency(chord[k]), 0.09f, k * 0.37f);
                AddPad(mix, start, score.ChordSeconds, Frequency(chord[0] - 12), 0.11f, 0f); // drone an octave down
            }
            foreach (MusicNote note in score.Melody)
                AddPluck(mix, note.Start, Frequency(note.Midi), 0.32f * note.Velocity);

            AddEcho(mix, 0.43f, 0.28f);
            SoftenAndNormalise(mix, 0.5f);
            return mix;
        }

        public static float Frequency(int midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);

        // ------------------------------------------------------------------ voices

        /// <summary>Pad voicing in the middle register (D3..D4 area): root, then the other tones above it.</summary>
        private static int[] Voice(int[] shape)
        {
            var notes = new int[shape.Length];
            int root = 48 + shape[0];
            if (root < 50) root += 12;
            notes[0] = root;
            for (int i = 1; i < shape.Length; i++)
            {
                int midi = 48 + shape[i];
                while (midi <= notes[i - 1]) midi += 12;
                notes[i] = midi;
            }
            return notes;
        }

        private static int Nearest(int pitchClass, int around, int low, int high)
        {
            int best = low, bestDistance = int.MaxValue;
            for (int midi = low; midi <= high; midi++)
            {
                if (((midi % 12) + 12) % 12 != pitchClass) continue;
                int distance = Math.Abs(midi - around);
                if (distance < bestDistance)
                {
                    best = midi;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>Slow-swelling string pad: three slightly detuned soft oscillators, attack and release overlapping the next chord.</summary>
        private static void AddPad(float[] mix, float start, float hold, float frequency, float amplitude, float phase)
        {
            int from = (int)(start * SampleRate);
            int to = Math.Min(mix.Length, (int)((start + hold + ReleaseSeconds) * SampleRate));
            double[] detune = { 1.0, 1.0035, 0.9968 };
            for (int i = from; i < to; i++)
            {
                float t = (i - from) / (float)SampleRate;
                float envelope = Math.Min(1f, t / AttackSeconds);
                if (t > hold) envelope *= Math.Max(0f, 1f - (t - hold) / ReleaseSeconds);
                double sum = 0;
                foreach (double d in detune)
                {
                    double p = 2.0 * Math.PI * frequency * d * t + phase;
                    sum += Math.Sin(p) + 0.18 * Math.Sin(2 * p) + 0.08 * Math.Sin(3 * p);
                }
                mix[i] += (float)(sum / 3.0) * amplitude * Smooth(envelope);
            }
        }

        /// <summary>Plucked string (Karplus-Strong): a burst of noise in a short delay line that fades into a soft tone.</summary>
        private static void AddPluck(float[] mix, float start, float frequency, float amplitude)
        {
            int period = Math.Max(2, (int)Math.Round(SampleRate / frequency));
            var line = new float[period];
            var random = new Random((int)(start * 1000) ^ period);
            for (int i = 0; i < period; i++) line[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            int from = (int)(start * SampleRate);
            int to = Math.Min(mix.Length, from + (int)(3.5f * SampleRate));
            int index = 0;
            float smooth = 0f;
            for (int i = from; i < to; i++)
            {
                int next = (index + 1) % period;
                float value = line[index];
                line[index] = 0.4985f * (line[index] + line[next]); // averaging = string damping
                index = next;
                smooth += (value - smooth) * 0.45f; // a mellower, harp-like timbre
                mix[i] += smooth * amplitude;
            }
        }

        private static void AddEcho(float[] mix, float delaySeconds, float feedback)
        {
            int delay = (int)(delaySeconds * SampleRate);
            for (int i = delay; i < mix.Length; i++) mix[i] += mix[i - delay] * feedback;
        }

        private static void SoftenAndNormalise(float[] mix, float peak)
        {
            float low = 0f, max = 1e-6f;
            for (int i = 0; i < mix.Length; i++)
            {
                low += (mix[i] - low) * 0.35f; // gentle low-pass: nothing harsh
                mix[i] = low;
                max = Math.Max(max, Math.Abs(low));
            }
            float gain = peak / max;
            int fade = Math.Min(mix.Length, SampleRate / 2);
            for (int i = 0; i < mix.Length; i++)
            {
                float edge = Math.Min(1f, Math.Min(i, mix.Length - 1 - i) / (float)fade); // no clicks at start or end
                mix[i] *= gain * edge;
            }
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
