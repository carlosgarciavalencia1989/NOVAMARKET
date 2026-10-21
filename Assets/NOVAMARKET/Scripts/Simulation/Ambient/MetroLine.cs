using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    public enum MetroDirection
    {
        Eastbound,
        Westbound
    }

    public enum MetroTrainState
    {
        Running,

        /// <summary>Standing at a platform with the doors open.</summary>
        AtStation,

        /// <summary>At the end of the line, about to return on the other track.</summary>
        TurningBack
    }

    /// <summary>A metro train: where it is on its track, how fast it goes and what it does.</summary>
    public sealed class MetroTrain
    {
        internal MetroTrain(int id, float length)
        {
            Id = id;
            Length = length;
        }

        public int Id { get; }

        /// <summary>Bumper to bumper (all cars and the gaps between them).</summary>
        public float Length { get; }

        public MetroDirection Direction { get; internal set; }
        public MetroTrainState State { get; internal set; }

        /// <summary>Distance of the front from the start of its track (the west end for eastbound trains).</summary>
        public float Front { get; internal set; }

        public float Speed { get; internal set; }

        /// <summary>The doors are open (standing at a platform).</summary>
        public bool DoorsOpen => State == MetroTrainState.AtStation;

        /// <summary>Centre of the train (world X/Z).</summary>
        public Vector2 Position { get; internal set; }

        /// <summary>Unit direction the front points to (world X/Z).</summary>
        public Vector2 Heading => Direction == MetroDirection.Eastbound ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);

        /// <summary>Index (west to east) of the station it stands at, or -1.</summary>
        public int StationIndex { get; internal set; } = -1;

        internal float Timer { get; set; }
        internal int NextStop { get; set; }

        public float Rear => Front - Length;

        public override string ToString() => $"Metro #{Id}";
    }

    /// <summary>
    /// The metro of the valley: trains run along the elevated line, brake to stop centred on each platform,
    /// stand there with the doors open, keep their distance from the train ahead and, at each end of the line,
    /// wait and return on the other track. Plain C#: call <see cref="Tick"/>; the scene only draws the trains.
    /// </summary>
    public sealed class MetroLine
    {
        private const float StopTolerance = 0.3f;

        private readonly List<MetroTrain> trains = new List<MetroTrain>();

        public MetroLine(MetroLineDefinition definition, MetroSettings settings = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Settings = settings ?? new MetroSettings();
            PlaceTrains();
        }

        public MetroLineDefinition Definition { get; }
        public MetroSettings Settings { get; }
        public IReadOnlyList<MetroTrain> Trains => trains;

        /// <summary>A train stopped at a platform and opened its doors (train, station index).</summary>
        public event Action<MetroTrain, int> Arrived;

        /// <summary>A train closed its doors and left a platform.</summary>
        public event Action<MetroTrain, int> Departed;

        /// <summary>Distance from the start of a track (in its direction) to the point at world <paramref name="x"/>.</summary>
        public float TrackDistance(MetroDirection direction, float x) =>
            direction == MetroDirection.Eastbound ? x - Definition.WestEnd : Definition.EastEnd - x;

        /// <summary>Spreads the trains evenly around the loop (out on one track, back on the other).</summary>
        private void PlaceTrains()
        {
            float track = Definition.TrackLength, length = Settings.TrainLength;
            float loop = 2f * track;
            for (int i = 0; i < Settings.TrainCount; i++)
            {
                var train = new MetroTrain(i + 1, length);
                float at = loop * i / Settings.TrainCount;
                train.Direction = at < track ? MetroDirection.Eastbound : MetroDirection.Westbound;
                float front = (at < track ? at : at - track) + length;
                train.Front = Math.Min(track, front);
                train.NextStop = FirstStopAhead(train);
                UpdatePosition(train);
                trains.Add(train);
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            foreach (MetroTrain train in trains) Drive(train, deltaTime);
        }

        private void Drive(MetroTrain train, float dt)
        {
            switch (train.State)
            {
                case MetroTrainState.AtStation:
                    train.Timer -= dt;
                    if (train.Timer > 0f) return;
                    int station = train.StationIndex;
                    train.State = MetroTrainState.Running;
                    train.StationIndex = -1;
                    train.NextStop++;
                    Departed?.Invoke(train, station);
                    return;

                case MetroTrainState.TurningBack:
                    train.Timer -= dt;
                    if (train.Timer > 0f || !IsTrackStartClear(Other(train.Direction))) return;
                    train.Direction = Other(train.Direction);
                    train.Front = train.Length; // the whole train is now at the start of the other track
                    train.State = MetroTrainState.Running;
                    train.NextStop = FirstStopAhead(train);
                    UpdatePosition(train);
                    return;
            }

            float track = Definition.TrackLength;
            float stopAt = train.NextStop < StopCount ? StopFront(train.Direction, train.NextStop) : track;
            float limit = stopAt - train.Front;

            float ahead = GapToTrainAhead(train);
            float room = Math.Min(limit, ahead - Settings.MinSeparation);
            float target = Math.Min(Settings.CruiseSpeed, (float)Math.Sqrt(2f * Settings.Deceleration * Math.Max(0f, room)));
            train.Speed = target > train.Speed ? Math.Min(target, train.Speed + Settings.Acceleration * dt) : target;
            train.Front = Math.Min(train.Front + train.Speed * dt, train.Front + Math.Max(0f, room));
            UpdatePosition(train);

            if (stopAt - train.Front > StopTolerance || train.Speed > 0.5f) return;
            train.Front = stopAt;
            train.Speed = 0f;
            UpdatePosition(train);
            if (train.NextStop < StopCount)
            {
                train.State = MetroTrainState.AtStation;
                train.Timer = Settings.DwellSeconds;
                train.StationIndex = StationIndexOfStop(train.Direction, train.NextStop);
                Arrived?.Invoke(train, train.StationIndex);
            }
            else
            {
                train.State = MetroTrainState.TurningBack;
                train.Timer = Settings.TurnbackSeconds;
            }
        }

        // ------------------------------------------------------------------ stops

        private int StopCount => Definition.StationX.Count;

        /// <summary>Station of the k-th stop in a direction (eastbound goes west to east).</summary>
        private int StationIndexOfStop(MetroDirection direction, int stop) =>
            direction == MetroDirection.Eastbound ? stop : StopCount - 1 - stop;

        /// <summary>Front position for the train to stand centred on the platform of the k-th stop.</summary>
        private float StopFront(MetroDirection direction, int stop) =>
            TrackDistance(direction, Definition.StationX[StationIndexOfStop(direction, stop)]) + Settings.TrainLength / 2f;

        private int FirstStopAhead(MetroTrain train)
        {
            int stop = 0;
            while (stop < StopCount && StopFront(train.Direction, stop) < train.Front - StopTolerance) stop++;
            return stop;
        }

        // ------------------------------------------------------------------ spacing

        /// <summary>Distance from the front to the rear of the next train on the same track (float.MaxValue when none).</summary>
        private float GapToTrainAhead(MetroTrain train)
        {
            float best = float.MaxValue;
            foreach (MetroTrain other in trains)
            {
                if (other == train || other.Direction != train.Direction || other.Rear < train.Front) continue;
                best = Math.Min(best, other.Rear - train.Front);
            }
            return best;
        }

        /// <summary>No train near the start of a track (a returning train needs its length plus the separation).</summary>
        private bool IsTrackStartClear(MetroDirection direction)
        {
            foreach (MetroTrain other in trains)
                if (other.Direction == direction && other.State != MetroTrainState.TurningBack
                    && other.Rear < Settings.TrainLength + Settings.MinSeparation)
                    return false;
            return true;
        }

        private static MetroDirection Other(MetroDirection direction) =>
            direction == MetroDirection.Eastbound ? MetroDirection.Westbound : MetroDirection.Eastbound;

        private void UpdatePosition(MetroTrain train)
        {
            float centre = train.Front - train.Length / 2f;
            bool east = train.Direction == MetroDirection.Eastbound;
            float x = east ? Definition.WestEnd + centre : Definition.EastEnd - centre;
            train.Position = new Vector2(x, east ? Definition.EastboundZ : Definition.WestboundZ);
        }
    }
}
