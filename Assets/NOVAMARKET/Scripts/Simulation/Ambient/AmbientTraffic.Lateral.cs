using System;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// Sideways moves: motos filter between the cars and the kerb up to the front of a queue and spread
    /// across the moto box at a red light; any vehicle stuck behind a parked one (a bus at its stop, a
    /// CEDI truck waiting at the gate) overtakes through the opposite lane when it is free, after
    /// booking that stretch so oncoming traffic waits.
    /// </summary>
    public sealed partial class AmbientTraffic
    {
        /// <summary>Moto positions across a lane: between the lanes, by the kerb, and the inner moto-box places.</summary>
        private static readonly float[] MotoSlots = { -2.1f, 1.9f, -0.7f, 0.7f, 0f };

        private const float MotoSideSpeed = 1.3f;
        private const float CarSideSpeed = 1.6f;

        private void UpdateLateral(AmbientVehicle v, float dt, float lookahead)
        {
            if (v.Overtake != null) ManageOvertake(v);
            else if (v.Link is TrafficLane lane && v.BehindParked && v.StoppedSeconds > 1.5f && v.LastFreeGap < 10f
                     && TryStartOvertake(v, lane))
                return;
            else if (v.IsMoto) ChooseMotoSlot(v, lookahead);
            else v.TargetOffset = 0f;

            if (v.Link is TrafficConnector && v.Overtake == null) v.TargetOffset = v.Offset; // no sideways moves inside a crossing
        }

        /// <summary>
        /// When something slower blocks it, a moto tries one other place across the lane per tick and moves
        /// there if it can go further; once it stands at the front of a red light it moves to an inner place
        /// so the next moto fits; with the road free it returns to the lane centre.
        /// </summary>
        private void ChooseMotoSlot(AmbientVehicle v, float lookahead)
        {
            if (!(v.Link is TrafficLane lane)) return;

            float toEnd = lane.Length - lane.StopLineBack - v.Front; // to the stop line
            bool atLight = lane.HasSignal && v.Held == null && toEnd < 2f && v.Speed < 0.3f;
            if (atLight && Math.Abs(v.TargetOffset) > 1.5f)
            {
                float inner = v.TargetOffset < 0f ? -0.7f : 0.7f;
                if (SideClear(v, inner)) v.TargetOffset = inner;
                return;
            }

            // First in the queue at a crossing without lights: back to the lane centre to reach the line.
            if (!lane.HasSignal && toEnd < 8f && Math.Abs(v.TargetOffset) > 0.1f && NobodyAhead(v, lane) && SideClear(v, 0f))
            {
                v.TargetOffset = 0f;
                return;
            }

            bool blocked = v.LastFreeGap < 12f && v.LastLeaderSpeed < v.DesiredSpeed - 2f;
            if (blocked)
            {
                float slot = MotoSlots[v.SlotCursor++ % MotoSlots.Length];
                float laneHalf = lane.TwoWay ? lane.HalfWidth / 2f : lane.HalfWidth;
                if (Math.Abs(slot - v.TargetOffset) < 0.1f || Math.Abs(slot) > laneHalf - 0.6f) return;
                float there = FreeDistance(v, slot, lookahead, out _, out _);
                if (there > v.LastFreeGap + 3f && SideClear(v, slot)) v.TargetOffset = slot;
            }
            else if (v.LastFreeGap > 30f && Math.Abs(v.TargetOffset) > 0.1f && toEnd > 25f && SideClear(v, 0f))
                v.TargetOffset = 0f;
        }

        private static bool NobodyAhead(AmbientVehicle v, TrafficLane lane)
        {
            foreach (AmbientVehicle other in lane.Occupants)
                if (other != v && other.Front > v.Front) return false;
            return true;
        }

        /// <summary>Moves the body towards its target offset, only as far as the space beside it is free.</summary>
        private void MoveOffset(AmbientVehicle v, float dt)
        {
            float difference = v.TargetOffset - v.Offset;
            if (Math.Abs(difference) < 1e-3f) return;
            float rate = v.Overtake != null ? CarSideSpeed : v.IsMoto ? MotoSideSpeed : CarSideSpeed;
            float step = Math.Sign(difference) * Math.Min(Math.Abs(difference), rate * dt);
            Footprint pose = v.PoseAt(v.Front, v.Offset + step);
            foreach (int index in candidates)
            {
                Footprint theirs = BodyFootprint(index);
                if (Vector2.DistanceSquared(pose.Centre, theirs.Centre) > 400f) continue;
                if (Overlaps(pose, theirs, 0.25f) && Separation(pose, theirs) < Separation(v.Footprint, theirs)) return;
            }
            v.Offset += step;
        }

        // ------------------------------------------------------------------ overtaking a parked vehicle

        private bool TryStartOvertake(AmbientVehicle v, TrafficLane lane)
        {
            TrafficLane opposite = lane.Opposite;
            if (opposite == null) return false;
            float end = ChainEnd(lane, v.ChainEnd) + v.Length + 4f; // front once the rear has cleared the parked queue
            if (end > lane.Length - 16f) return false; // too close to the crossing

            // The same stretch seen from the opposite lane (both lanes have the same length).
            float start = opposite.Length - (end + 3f);
            float finish = opposite.Length - (v.Front - v.Length - 3f);
            if (!OppositeClear(lane, opposite, start, finish)) return false;
            if (!SideClear(v, -1f)) return false;

            var span = new OvertakeSpan(opposite, lane, start, finish);
            laneSpans[opposite.Id].Add(span);
            v.Overtake = span;
            v.OvertakeEnd = end;
            v.TargetOffset = -2f * (lane.HalfWidth / 2f);
            return true;
        }

        /// <summary>
        /// Extends the end of a parked queue past every standing body that touches the lane just after it
        /// (e.g. a second truck at the gate, standing across the lane), so the overtake passes them all.
        /// </summary>
        private float ChainEnd(TrafficLane lane, float end)
        {
            const float joined = 8f;
            float laneHalf = lane.TwoWay ? lane.HalfWidth / 2f : lane.HalfWidth;
            for (bool extended = true; extended;)
            {
                extended = false;
                foreach (SharedRoadVehicle external in externals)
                {
                    if (external.Speed > 0.2f) continue;
                    Footprint body = external.Footprint;
                    (float rear, float front) = ProjectedRange(lane, body);
                    lane.Project(body.Centre, out float lateral);
                    float sideways = body.HalfLength * Math.Abs(Vector2.Dot(body.Forward, lane.Right))
                                     + body.HalfWidth * Math.Abs(Vector2.Dot(body.Right, lane.Right));
                    if (Math.Abs(lateral) - sideways > laneHalf || rear > end + joined || front <= end) continue;
                    end = front;
                    extended = true;
                }
                foreach (AmbientVehicle other in lane.Occupants)
                {
                    if (!other.IsParked || other.Front - other.Length > end + joined || other.Front <= end) continue;
                    end = other.Front;
                    extended = true;
                }
            }
            return end;
        }

        /// <summary>Nobody in the stretch of the opposite lane, nobody close enough to need it, nobody turning into it.</summary>
        private bool OppositeClear(TrafficLane lane, TrafficLane opposite, float start, float finish)
        {
            foreach (OvertakeSpan span in laneSpans[opposite.Id])
                if (span.OwnerLane != lane) return false;
            if (laneSpans[lane.Id].Count > 0) return false; // someone coming the other way is borrowing ours

            foreach (AmbientVehicle other in opposite.Occupants)
            {
                float stopping = other.Speed * other.Speed / (2f * other.Spec.ComfortDecel) + 6f;
                if (other.Front > start - stopping && other.Front - other.Length < finish) return false;
            }
            foreach (ExternalOnLane external in laneExternals[opposite.Id])
                if (external.Front > start - 30f && external.Rear < finish) return false;
            if (start < 35f && opposite.Occupants.Count > 0) return false;
            foreach (TrafficJunction junction in Network.Junctions)
            {
                if (start >= 35f || junction != lane.EndJunction) continue;
                foreach (AmbientVehicle holder in junction.Holders)
                    if (holder.Held.To == opposite) return false;
            }

            // Bodies standing across the borrowed stretch (e.g. a truck turning in).
            var area = new Footprint(opposite.PointAt((start + finish) / 2f), opposite.Direction,
                Math.Max(1f, finish - start), 3f);
            foreach (AmbientVehicle other in vehicles)
                if (Vector2.DistanceSquared(other.Position, area.Centre) < 90f * 90f
                    && other.Link != lane && Overlaps(area, other.Footprint, 0.3f))
                    return false;
            foreach (SharedRoadVehicle external in externals)
                if (Overlaps(area, external.Footprint, 0.3f)) return false;
            return true;
        }

        private void ManageOvertake(AmbientVehicle v)
        {
            if (v.Link != v.Overtake.OwnerLane && v.Offset > -0.5f)
            {
                EndOvertake(v);
                return;
            }
            bool passed = v.Front >= v.OvertakeEnd;
            if (passed && v.TargetOffset < -0.1f)
            {
                if (SideClear(v, 0f)) v.TargetOffset = 0f;
                else if (v.Speed < 0.5f) ExtendOvertake(v); // more standing in the lane than it saw: borrow a bit more
            }
            if (v.TargetOffset > -0.1f && Math.Abs(v.Offset) < 0.3f) EndOvertake(v);
            else if (!passed && v.Offset > -0.3f && v.StoppedSeconds > 5f) // could not pull out: give up
            {
                v.TargetOffset = 0f;
                EndOvertake(v);
            }
        }

        private void ExtendOvertake(AmbientVehicle v)
        {
            OvertakeSpan old = v.Overtake;
            TrafficLane lane = old.OwnerLane, opposite = old.Lane;
            float end = v.OvertakeEnd + 8f;
            if (end > lane.Length - 16f) return;
            float start = opposite.Length - (end + 3f);
            if (!OppositeClear(lane, opposite, start, old.Start)) return;
            laneSpans[opposite.Id].Remove(old);
            v.Overtake = new OvertakeSpan(opposite, lane, start, old.End);
            laneSpans[opposite.Id].Add(v.Overtake);
            v.OvertakeEnd = end;
        }

        private void EndOvertake(AmbientVehicle v)
        {
            if (v.Overtake == null) return;
            laneSpans[v.Overtake.Lane.Id].Remove(v.Overtake);
            v.Overtake = null;
        }
    }
}
