using System;
using System.Linq;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    public class ProceduralMusicTests
    {
        private static int PitchClass(int midi) => ((midi % 12) + 12) % 12;

        [Test]
        public void Score_StaysInDMinor_StartsAndEndsAtHome()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                MusicScore score = ProceduralMusic.Compose(seed);
                Assert.AreEqual(8, score.Chords.Count);
                Assert.AreEqual(2, PitchClass(score.Chords[0][0]), "starts on D minor");
                Assert.AreEqual(2, PitchClass(score.Chords[score.Chords.Count - 1][0]), "ends on D minor");
                foreach (int[] chord in score.Chords)
                    Assert.IsTrue(chord.All(m => ProceduralMusic.Scale.Contains(PitchClass(m))), $"seed {seed}: chord in the scale");
                foreach (MusicNote note in score.Melody)
                {
                    Assert.Contains(PitchClass(note.Midi), ProceduralMusic.Scale, $"seed {seed}: melody in the scale");
                    Assert.That(note.Midi, Is.InRange(62, 81), "a gentle register, nothing shrill");
                    Assert.That(note.Velocity, Is.InRange(0.5f, 0.95f));
                }
            }
        }

        [Test]
        public void Melody_IsSparse_AndLeavesTheChordsRoomToBreathe()
        {
            MusicScore score = ProceduralMusic.Compose(11);
            Assert.That(score.Melody.Count, Is.InRange(score.Chords.Count, score.Chords.Count * 3), "one to three notes per chord");
            for (int i = 1; i < score.Melody.Count; i++)
                Assert.GreaterOrEqual(score.Melody[i].Start - score.Melody[i - 1].Start, 0.74f, "no fast runs");
            foreach (MusicNote note in score.Melody)
            {
                float intoChord = note.Start % score.ChordSeconds;
                Assert.GreaterOrEqual(intoChord, 0.74f, "never on the chord change");
            }
        }

        [Test]
        public void Seeds_GiveDifferentPieces_TheSameSeedTheSamePiece()
        {
            MusicScore a = ProceduralMusic.Compose(3), b = ProceduralMusic.Compose(3), c = ProceduralMusic.Compose(4);
            CollectionAssert.AreEqual(a.Melody.Select(n => n.Midi), b.Melody.Select(n => n.Midi));
            Assert.IsFalse(a.Melody.Select(n => (n.Start, n.Midi)).SequenceEqual(c.Melody.Select(n => (n.Start, n.Midi))));
        }

        [Test]
        public void Render_IsSoft_Clean_AndFadesInAndOut()
        {
            MusicScore score = ProceduralMusic.Compose(5);
            float[] samples = ProceduralMusic.Render(score);
            Assert.AreEqual((int)(score.Seconds * ProceduralMusic.SampleRate), samples.Length);
            Assert.That(score.Seconds, Is.InRange(45f, 60f), "a short piece");
            Assert.IsTrue(samples.All(s => !float.IsNaN(s)));
            Assert.AreEqual(0.5f, samples.Max(Math.Abs), 0.01f, "peak at half scale: subtle, never clipping");
            Assert.AreEqual(0f, samples[0], 1e-6f, "starts in silence");
            Assert.AreEqual(0f, samples[samples.Length - 1], 1e-6f, "ends in silence");
            double rms = Math.Sqrt(samples.Average(s => (double)s * s));
            Assert.That(rms, Is.InRange(0.03, 0.3), "steady, quiet background");
        }

        [Test]
        public void Frequency_FollowsTheMidiStandard()
        {
            Assert.AreEqual(440f, ProceduralMusic.Frequency(69), 1e-3f);
            Assert.AreEqual(293.66f, ProceduralMusic.Frequency(62), 0.01f, "D4");
            Assert.AreEqual(220f, ProceduralMusic.Frequency(57), 1e-3f);
        }
    }
}
