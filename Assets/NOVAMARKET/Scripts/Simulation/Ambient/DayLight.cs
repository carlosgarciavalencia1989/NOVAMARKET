using System;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>How the valley is lit at a given hour: the sun (or the moon), the ambient light and the street lamps.</summary>
    public readonly struct DayLightState
    {
        public DayLightState(float elevation, float pitch, float yaw, Vector3 colour, float intensity, float ambientScale, bool lampsOn)
        {
            Elevation = elevation;
            Pitch = pitch;
            Yaw = yaw;
            Colour = colour;
            Intensity = intensity;
            AmbientScale = ambientScale;
            LampsOn = lampsOn;
        }

        /// <summary>Height of the sun above the horizon, degrees (negative at night).</summary>
        public float Elevation { get; }

        /// <summary>Rotation of the directional light: X (pitch) and Y (yaw) Euler angles, degrees.</summary>
        public float Pitch { get; }
        public float Yaw { get; }

        /// <summary>Colour of the light (RGB 0..1) and its intensity.</summary>
        public Vector3 Colour { get; }
        public float Intensity { get; }

        /// <summary>Multiplier of the ambient (sky) light and the fog colour: 1 in the afternoon, dim at night.</summary>
        public float AmbientScale { get; }

        /// <summary>Street lamps and headlights on (dusk and night).</summary>
        public bool LampsOn { get; }

        public bool IsNight => Elevation < 0f;
    }

    /// <summary>
    /// Light of the valley through the day, near the equator: sunrise at 6:00, sunset at 18:00. The default
    /// hour (<see cref="AfternoonHour"/>) reproduces the warm afternoon the scene was lit with (sun at 25°
    /// from the west-southwest, colour 1/0.85/0.70, intensity 2.2). Plain C#; the scene applies it.
    /// </summary>
    public static class DayLight
    {
        public const float Sunrise = 6f;
        public const float Sunset = 18f;
        public const float AfternoonHour = 16.5f;

        /// <summary>The sun's height at <see cref="AfternoonHour"/> and its direction then.</summary>
        public const float AfternoonElevation = 25f;
        public const float AfternoonYaw = 62f;
        public const float AfternoonIntensity = 2.2f;

        /// <summary>Degrees the sun turns around the sky per hour (yaw).</summary>
        public const float YawPerHour = 12f;

        /// <summary>The lamps switch on when the sun is lower than this (about 17:35).</summary>
        public const float LampsOnBelow = 7f;

        private const float MoonIntensity = 0.12f;
        private const float NightAmbient = 0.18f;

        private static readonly Vector3 SunsetColour = new Vector3(1f, 0.5f, 0.3f);
        private static readonly Vector3 AfternoonColour = new Vector3(1f, 0.85f, 0.70f);
        private static readonly Vector3 NoonColour = new Vector3(1f, 0.97f, 0.92f);
        private static readonly Vector3 MoonColour = new Vector3(0.55f, 0.62f, 0.85f);

        /// <summary>Highest sun of the day, chosen so that <see cref="AfternoonHour"/> gives <see cref="AfternoonElevation"/>.</summary>
        public static readonly float NoonElevation =
            AfternoonElevation / (float)Math.Sin(Math.PI * (AfternoonHour - Sunrise) / (Sunset - Sunrise));

        public static float SunElevation(float hour)
        {
            double angle = Math.PI * (Wrap(hour) - Sunrise) / (Sunset - Sunrise);
            return (float)(NoonElevation * Math.Sin(angle));
        }

        public static DayLightState Evaluate(float hour)
        {
            hour = Wrap(hour);
            float elevation = SunElevation(hour);
            float yaw = AfternoonYaw + (hour - AfternoonHour) * YawPerHour;
            bool lamps = elevation < LampsOnBelow;
            float ambient = Lerp(NightAmbient, 1f, Clamp01((elevation + 6f) / (AfternoonElevation + 6f)));

            if (elevation <= 0f)
            {
                // Night: a dim bluish moon from high up, so the valley stays readable.
                return new DayLightState(elevation, 50f, 200f, MoonColour, MoonIntensity, ambient, true);
            }

            double sinRatio = Math.Sin(elevation * Math.PI / 180.0) / Math.Sin(AfternoonElevation * Math.PI / 180.0);
            float intensity = AfternoonIntensity * (float)Math.Pow(Math.Min(1.3, sinRatio), 0.6);
            // Dusk: the sun fades out over its last 3 degrees and the moon takes over.
            float fade = Clamp01(elevation / 3f);
            intensity = Lerp(MoonIntensity, intensity, fade);

            Vector3 colour = elevation < AfternoonElevation
                ? Vector3.Lerp(SunsetColour, AfternoonColour, elevation / AfternoonElevation)
                : Vector3.Lerp(AfternoonColour, NoonColour, Clamp01((elevation - AfternoonElevation) / 35f));
            return new DayLightState(elevation, Math.Max(elevation, 2f), yaw, colour, intensity, ambient, lamps);
        }

        private static float Wrap(float hour)
        {
            hour %= 24f;
            return hour < 0f ? hour + 24f : hour;
        }

        private static float Clamp01(float x) => Math.Max(0f, Math.Min(1f, x));
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
