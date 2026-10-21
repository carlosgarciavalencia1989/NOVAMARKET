using System;

namespace NovaMarket.Simulation.Ambient
{
    public enum SignalAspect
    {
        Green,
        Amber,
        Red
    }

    /// <summary>
    /// Fixed-time traffic lights with two groups: group 0 (the main road) and group 1 (the cross
    /// road). Each group gets green, then amber, then everyone has red for a moment before the other
    /// group starts, so the two groups are never green at the same time.
    /// </summary>
    public sealed class TrafficSignal
    {
        public TrafficSignal(float mainGreen, float crossGreen, float amber = 3f, float allRed = 2f, float offset = 0f)
        {
            if (mainGreen <= 0f || crossGreen <= 0f) throw new ArgumentOutOfRangeException(nameof(mainGreen));
            MainGreen = mainGreen;
            CrossGreen = crossGreen;
            Amber = amber;
            AllRed = allRed;
            Offset = offset;
        }

        public float MainGreen { get; }
        public float CrossGreen { get; }
        public float Amber { get; }
        public float AllRed { get; }

        /// <summary>Seconds added to the clock, so neighbouring lights do not all change together.</summary>
        public float Offset { get; }

        public float Cycle => MainGreen + CrossGreen + 2f * (Amber + AllRed);

        public SignalAspect AspectFor(int group, double time)
        {
            float local = LocalTime(group, time, out float green);
            if (local < green) return SignalAspect.Green;
            return local < green + Amber ? SignalAspect.Amber : SignalAspect.Red;
        }

        /// <summary>Seconds of green the group still has (0 when it is not green).</summary>
        public float GreenLeft(int group, double time)
        {
            float local = LocalTime(group, time, out float green);
            return local < green ? green - local : 0f;
        }

        /// <summary>Seconds since the group's green started (0 … cycle), and its green length.</summary>
        private float LocalTime(int group, double time, out float green)
        {
            float t = (float)((time + Offset) % Cycle);
            if (t < 0f) t += Cycle;
            float start = group == 0 ? 0f : MainGreen + Amber + AllRed;
            green = group == 0 ? MainGreen : CrossGreen;
            float local = t - start;
            return local < 0f ? local + Cycle : local;
        }
    }
}
