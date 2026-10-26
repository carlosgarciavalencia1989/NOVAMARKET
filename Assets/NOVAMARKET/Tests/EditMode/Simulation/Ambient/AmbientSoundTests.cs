using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    public class AmbientSoundTests
    {
        private static IEnumerable<(string Name, float[] Samples)> Loops()
        {
            yield return ("wind", ProceduralSounds.Wind());
            yield return ("car", ProceduralSounds.Engine(55f));
            yield return ("diesel", ProceduralSounds.Engine(32f, roughness: 0.35f));
            yield return ("metro", ProceduralSounds.Metro());
            yield return ("beep", ProceduralSounds.ReverseBeep());
        }

        [Test]
        public void PlaceholderLoops_AreCentred_NotClipped_AndRepeatWithoutClicks()
        {
            foreach ((string name, float[] s) in Loops())
            {
                Assert.Greater(s.Length, ProceduralSounds.SampleRate / 2, name);
                Assert.IsTrue(s.All(x => !float.IsNaN(x) && Math.Abs(x) <= 0.7f), $"{name}: peak under 0.7");
                Assert.Greater(s.Max(Math.Abs), 0.3f, $"{name}: audible");
                Assert.AreEqual(0.0, s.Average(x => (double)x), 0.01, $"{name}: no offset");

                // The jump from the last sample back to the first is no bigger than the usual step inside the loop.
                double typicalStep = 0;
                for (int i = 1; i < s.Length; i++) typicalStep = Math.Max(typicalStep, Math.Abs(s[i] - s[i - 1]));
                Assert.LessOrEqual(Math.Abs(s[0] - s[s.Length - 1]), typicalStep + 1e-4, $"{name}: seamless loop");
            }
        }

        [Test]
        public void ReverseBeep_BeepsHalfASecond_ThenSilence()
        {
            float[] beep = ProceduralSounds.ReverseBeep();
            int quarter = beep.Length / 4;
            Assert.Greater(beep.Skip(quarter / 2).Take(quarter).Max(Math.Abs), 0.4f, "beeping");
            Assert.Less(beep.Skip(beep.Length - quarter).Max(Math.Abs), 0.01f, "silent");
        }

        [Test]
        public void Engine_LoopsAreDeterministic()
        {
            CollectionAssert.AreEqual(ProceduralSounds.Engine(55f), ProceduralSounds.Engine(55f));
            CollectionAssert.AreNotEqual(ProceduralSounds.Engine(55f), ProceduralSounds.Engine(32f));
        }

        [Test]
        public void Street_IsHeardOnlyCloseToTheGround()
        {
            Assert.AreEqual(1f, AmbientSoundMix.NearGround(27f), 1e-4f, "camera zoomed all the way in");
            Assert.AreEqual(0f, AmbientSoundMix.NearGround(145f), 1e-4f, "strategy view: only the music");
            float middle = AmbientSoundMix.NearGround(60f);
            Assert.That(middle, Is.InRange(0.1f, 0.9f), "fades in while zooming");
            Assert.Greater(AmbientSoundMix.NearGround(50f), middle);

            Assert.Greater(AmbientSoundMix.WindVolume(1f), AmbientSoundMix.WindVolume(0f), "gusts");
            Assert.That(AmbientSoundMix.WindVolume(3f), Is.InRange(0f, 1f));
        }

        [Test]
        public void Engines_RiseWithSpeed_DieselsLess()
        {
            Assert.AreEqual(1f, AmbientSoundMix.EnginePitch(AmbientVehicleKind.Car, 0f), 1e-4f);
            Assert.Greater(AmbientSoundMix.EnginePitch(AmbientVehicleKind.Car, 12f), AmbientSoundMix.EnginePitch(AmbientVehicleKind.Car, 4f));
            float busTop = AmbientSoundMix.EnginePitch(AmbientVehicleKind.Bus, 50f);
            float motoTop = AmbientSoundMix.EnginePitch(AmbientVehicleKind.Moto, 50f);
            Assert.AreEqual(1.5f, busTop, 1e-4f);
            Assert.AreEqual(2f, motoTop, 1e-4f);
            Assert.Greater(AmbientSoundMix.EngineVolume(AmbientVehicleKind.Truck, 5f), AmbientSoundMix.EngineVolume(AmbientVehicleKind.Car, 5f));
            Assert.Greater(AmbientSoundMix.EngineVolume(AmbientVehicleKind.Car, 12f), AmbientSoundMix.EngineVolume(AmbientVehicleKind.Car, 0f));
        }

        [Test]
        public void Nearest_PicksTheClosestWithinRange_NearestFirst()
        {
            var positions = new List<Vector2>
            {
                new Vector2(50f, 0f), new Vector2(10f, 0f), new Vector2(500f, 0f), new Vector2(0f, 20f), new Vector2(-30f, 0f)
            };
            CollectionAssert.AreEqual(new[] { 1, 3, 4 }, AmbientSoundMix.Nearest(positions, Vector2.Zero, 3, 200f));
            CollectionAssert.AreEqual(new[] { 1, 3, 4, 0 }, AmbientSoundMix.Nearest(positions, Vector2.Zero, 10, 200f), "500 m is out of range");
            Assert.AreEqual(0, AmbientSoundMix.Nearest(positions, Vector2.Zero, 0, 200f).Count);
        }
    }
}
