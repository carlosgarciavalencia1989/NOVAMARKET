using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    public enum AmbientVehicleKind
    {
        Car,
        Taxi,
        Moto,
        Bus,
        Truck
    }

    /// <summary>Size and driving of a kind of vehicle (meters, m/s, m/s²).</summary>
    public readonly struct AmbientVehicleSpec
    {
        public AmbientVehicleSpec(float length, float width, float maxSpeed, float acceleration, float comfortDecel, float standstillGap)
        {
            Length = length;
            Width = width;
            MaxSpeed = maxSpeed;
            Acceleration = acceleration;
            ComfortDecel = comfortDecel;
            StandstillGap = standstillGap;
        }

        public float Length { get; }
        public float Width { get; }
        public float MaxSpeed { get; }
        public float Acceleration { get; }
        public float ComfortDecel { get; }

        /// <summary>Bumper-to-bumper distance kept when stopped behind another vehicle.</summary>
        public float StandstillGap { get; }

        public static AmbientVehicleSpec For(AmbientVehicleKind kind) => kind switch
        {
            AmbientVehicleKind.Moto => new AmbientVehicleSpec(2.0f, 0.8f, 17f, 3.8f, 3.6f, 0.9f),
            AmbientVehicleKind.Taxi => new AmbientVehicleSpec(4.0f, 1.7f, 16.7f, 2.8f, 3.2f, 1.8f),
            AmbientVehicleKind.Bus => new AmbientVehicleSpec(11.5f, 2.5f, 13f, 1.3f, 2.4f, 2.5f),
            AmbientVehicleKind.Truck => new AmbientVehicleSpec(9.5f, 2.5f, 12.5f, 1.2f, 2.4f, 2.5f),
            _ => new AmbientVehicleSpec(4.3f, 1.8f, 16.7f, 2.6f, 3.0f, 2.0f)
        };
    }

    /// <summary>
    /// A car, taxi, moto, bus or truck of the city traffic. It follows a route of lanes and crossing
    /// movements; its pose is computed from the route (front and rear on the route, plus a sideways
    /// offset for motos filtering and for overtaking). It holds no Unity objects: the scene only draws it.
    /// </summary>
    public sealed class AmbientVehicle
    {
        internal readonly List<TrafficLink> Ahead = new List<TrafficLink>();
        internal readonly List<TrafficLink> Behind = new List<TrafficLink>();

        internal AmbientVehicle(int id, AmbientVehicleKind kind, int variant, AmbientVehicleSpec spec, float speedFactor)
        {
            Id = id;
            Kind = kind;
            Variant = variant;
            Spec = spec;
            DesiredSpeed = spec.MaxSpeed * speedFactor;
        }

        public int Id { get; }
        public AmbientVehicleKind Kind { get; }

        /// <summary>Random number the scene uses to pick a colour or model variant.</summary>
        public int Variant { get; }

        public AmbientVehicleSpec Spec { get; }
        public float Length => Spec.Length;
        public float Width => Spec.Width;
        public bool IsMoto => Kind == AmbientVehicleKind.Moto;

        public Vector2 Position { get; private set; }

        /// <summary>Unit direction the front points to (X/Z).</summary>
        public Vector2 Heading { get; private set; } = new Vector2(1f, 0f);

        public float Speed { get; internal set; }

        /// <summary>True while it slows down or stands still (brake lights).</summary>
        public bool IsBraking { get; internal set; }

        /// <summary>Sideways offset from its lane centre, positive to the right (meters).</summary>
        public float Offset { get; internal set; }

        /// <summary>A bus standing at a stop.</summary>
        public bool IsParked { get; internal set; }

        /// <summary>Seconds it has been standing still in a row.</summary>
        public float StoppedSeconds { get; internal set; }

        public Footprint Footprint => new Footprint(Position, Heading, Length, Width);

        /// <summary>The lane or crossing movement the front is on.</summary>
        public TrafficLink Link { get; internal set; }

        /// <summary>Distance of the front from the start of <see cref="Link"/>.</summary>
        public float Front { get; internal set; }

        /// <summary>The crossing movement it has booked (null when none).</summary>
        public TrafficConnector Booked => Held;

        internal float DesiredSpeed { get; }
        internal bool Done { get; set; }
        internal float LastFreeGap { get; set; } = float.MaxValue;
        internal float LastLeaderSpeed { get; set; }
        internal float TargetOffset { get; set; }
        internal TrafficConnector Held { get; set; }
        internal bool StoppedAtSign { get; set; }
        internal int BusStopIndex { get; set; }
        internal float DwellLeft { get; set; }
        internal OvertakeSpan Overtake { get; set; }
        internal float OvertakeEnd { get; set; }

        // What stops it (refreshed every tick).
        internal int BlockerBody { get; set; } = -1;
        internal AmbientVehicle BlockerVehicle { get; set; }
        internal AmbientVehicle PreviousBlocker { get; set; }
        internal float BlockedSeconds { get; set; }
        internal AmbientVehicle PassThrough { get; set; }
        internal float PassThroughLeft { get; set; }
        internal bool BehindParked { get; set; }
        internal float ChainEnd { get; set; }
        internal int SlotCursor { get; set; }

        /// <summary>The lane the vehicle is on, or the lane it leaves while on a crossing movement.</summary>
        internal TrafficLane Lane => Link as TrafficLane ?? (Link as TrafficConnector)?.From;

        /// <summary>The next crossing movement on its route (the one it is on, if on a crossing).</summary>
        internal TrafficConnector NextMovement
        {
            get
            {
                if (Link is TrafficConnector on) return on;
                foreach (TrafficLink link in Ahead)
                    if (link is TrafficConnector connector) return connector;
                return null;
            }
        }

        // ------------------------------------------------------------------ route geometry

        /// <summary>Point and direction at a distance measured from the start of <see cref="Link"/> (may be negative or past it).</summary>
        internal (Vector2 Point, Vector2 Direction) RoutePoint(float distance)
        {
            TrafficLink link = Link;
            if (distance < 0f)
            {
                for (int i = Behind.Count - 1; i >= 0 && distance < 0f; i--)
                {
                    link = Behind[i];
                    distance += link.Length;
                }
                if (distance < 0f) return Extend(link.Path, distance);
            }
            else
            {
                int k = 0;
                while (distance > link.Length && k < Ahead.Count)
                {
                    distance -= link.Length;
                    link = Ahead[k++];
                }
                if (distance > link.Length) return Extend(link.Path, distance);
            }
            return (link.Path.PointAt(distance), link.Path.DirectionAt(distance));
        }

        private static (Vector2, Vector2) Extend(Polyline path, float distance)
        {
            if (distance < 0f)
            {
                Vector2 d0 = path.DirectionAt(0f);
                return (path.Start + d0 * distance, d0);
            }
            Vector2 d1 = path.DirectionAt(path.Length);
            return (path.End + d1 * (distance - path.Length), d1);
        }

        /// <summary>Footprint with the front at <paramref name="front"/> on the route and a sideways offset.</summary>
        internal Footprint PoseAt(float front, float offset)
        {
            (Vector2 f, Vector2 fd) = RoutePoint(front);
            (Vector2 r, Vector2 rd) = RoutePoint(front - Length);
            f += new Vector2(fd.Y, -fd.X) * offset;
            r += new Vector2(rd.Y, -rd.X) * offset;
            Vector2 axis = f - r;
            Vector2 heading = axis.LengthSquared() > 1e-6f ? Vector2.Normalize(axis) : fd;
            return new Footprint((f + r) / 2f, heading, Length, Width);
        }

        internal void UpdatePose()
        {
            Footprint pose = PoseAt(Front, Offset);
            Position = pose.Centre;
            Heading = pose.Forward;
        }

        /// <summary>Meters of planned route in front of the front bumper.</summary>
        internal float PlannedAhead()
        {
            float total = Link.Length - Front;
            foreach (TrafficLink link in Ahead) total += link.Length;
            return total;
        }

        internal void TrimBehind()
        {
            float kept = Front;
            int keep = 0;
            for (int i = Behind.Count - 1; i >= 0 && kept < Length + 2f; i--)
            {
                kept += Behind[i].Length;
                keep++;
            }
            if (Behind.Count > keep) Behind.RemoveRange(0, Behind.Count - keep);
        }

        public override string ToString() => $"{Kind} #{Id}";
    }

    /// <summary>A stretch of a lane kept free for vehicles overtaking in it against its direction.</summary>
    internal sealed class OvertakeSpan
    {
        public OvertakeSpan(TrafficLane lane, TrafficLane ownerLane, float start, float end)
        {
            Lane = lane;
            OwnerLane = ownerLane;
            Start = Math.Max(0f, start);
            End = Math.Min(lane.Length, end);
        }

        /// <summary>The lane being borrowed.</summary>
        public TrafficLane Lane { get; }

        /// <summary>The lane the overtaking vehicle comes from (its opposite).</summary>
        public TrafficLane OwnerLane { get; }

        public float Start { get; }
        public float End { get; }
    }
}
