using System;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// Driving: each vehicle looks ahead along its route, finds the first body it would come too close to
    /// and the first rule that stops it (lights, "PARE", giving way, a full exit, a "do not block" box, a
    /// bus stop, a lane kept for an overtake) and picks a safe speed. It never moves past either point,
    /// so bodies never touch.
    /// </summary>
    public sealed partial class AmbientTraffic
    {
        /// <summary>Space kept sideways and lengthways around every body (meters).</summary>
        public const float BodyGap = 0.35f;

        /// <summary>A driver who sees amber stops if this braking (m/s²) is enough; otherwise crosses.</summary>
        private const float AmberDecel = 4f;

        /// <summary>Last seconds of each green reserved for left turners (they wait for a gap in the oncoming flow).</summary>
        private const float LeftTurnSeconds = 8f;

        private const float MaxDecel = 7f;
        private const float ReactionSeconds = 0.5f;
        private const float MutualWaitSeconds = 5f;

        private float[] weights = new float[4];

        private void Drive(AmbientVehicle v, float dt)
        {
            float decel = v.Spec.ComfortDecel;
            float lookahead = v.Speed * v.Speed / (2f * decel) + 10f;
            EnsureAhead(v, lookahead + v.Length + 40f);
            GatherCandidates(v, lookahead);

            UpdateLateral(v, dt, lookahead);

            float obstacle = FreeDistance(v, v.TargetOffset, lookahead, out int blocker, out float leaderSpeed);
            TrackBlocker(v, blocker, obstacle, dt);
            if (v.PassThrough != null)
                obstacle = FreeDistance(v, v.TargetOffset, lookahead, out blocker, out leaderSpeed);
            v.LastFreeGap = obstacle;
            v.LastLeaderSpeed = leaderSpeed;

            float rule = RuleLimit(v, lookahead, obstacle, dt);

            // Safe speed: free speed, the speed that still stops before the obstacle (Gipps-like, with the
            // leader's own speed), and the speed that stops at the rule's line.
            float free = Math.Min(v.DesiredSpeed, v.Link.SpeedLimit * (v.IsMoto ? 1.1f : 1f));
            free = Math.Min(free, CurveAhead(v, decel));
            float target = free;
            if (obstacle < lookahead)
            {
                float room = obstacle - (v.Spec.StandstillGap - BodyGap) - v.Speed * ReactionSeconds;
                target = Math.Min(target, (float)Math.Sqrt(Math.Max(0f, 2f * decel * room + leaderSpeed * leaderSpeed)));
            }
            if (rule < float.MaxValue)
                target = Math.Min(target, rule < 0.05f ? 0f : (float)Math.Sqrt(2f * decel * rule));

            float speed = target > v.Speed
                ? Math.Min(target, v.Speed + v.Spec.Acceleration * dt)
                : Math.Max(target, v.Speed - MaxDecel * dt);
            float move = Math.Max(0f, speed) * dt;
            float hard = Math.Min(obstacle, rule);
            if (move > hard)
            {
                move = Math.Max(0f, hard);
                speed = move / dt;
            }
            v.IsBraking = speed < v.Speed - 0.05f || speed < 0.3f;
            v.Speed = Math.Max(0f, speed);
            v.StoppedSeconds = v.Speed < 0.2f ? v.StoppedSeconds + dt : 0f;

            MoveOffset(v, dt);
            Advance(v, SafeMove(v, move));
            v.UpdatePose();
        }

        /// <summary>
        /// Last check of the exact final pose (the look-ahead samples every meter, and on a curve a body
        /// beside another can come closer in between): halves the move until it does not touch anyone.
        /// </summary>
        private float SafeMove(AmbientVehicle v, float move)
        {
            if (move <= 0f) return 0f;
            Footprint now = v.Footprint;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (FirstTouch(v, v.PoseAt(v.Front + move, v.Offset), now) < 0) return move;
                move *= 0.5f;
            }
            return 0f;
        }

        private int FirstTouch(AmbientVehicle v, Footprint pose, Footprint now)
        {
            foreach (int index in candidates)
            {
                Footprint theirs = BodyFootprint(index);
                if (Vector2.DistanceSquared(pose.Centre, theirs.Centre) > 900f) continue;
                float gap = bodies[index].Vehicle != null && bodies[index].Vehicle == v.PassThrough ? 0.01f : 0.1f;
                if (Overlaps(pose, theirs, gap) && Separation(pose, theirs) < Separation(now, theirs) - 1e-4f) return index;
            }
            return -1;
        }

        /// <summary>Slows down in time for a sharp movement ahead.</summary>
        private static float CurveAhead(AmbientVehicle v, float decel)
        {
            float distance = v.Link.Length - v.Front;
            float limit = float.MaxValue;
            foreach (TrafficLink link in v.Ahead)
            {
                if (distance > 60f) break;
                limit = Math.Min(limit, (float)Math.Sqrt(link.SpeedLimit * link.SpeedLimit + 2f * decel * Math.Max(0f, distance)));
                distance += link.Length;
            }
            return limit;
        }

        // ------------------------------------------------------------------ route

        private void EnsureAhead(AmbientVehicle v, float needed)
        {
            while (v.PlannedAhead() < needed)
            {
                TrafficLink last = v.Ahead.Count > 0 ? v.Ahead[v.Ahead.Count - 1] : v.Link;
                TrafficLink next = ChooseNext(v, last);
                if (next == null) return;
                v.Ahead.Add(next);
            }
        }

        /// <summary>Mostly straight on; buses and trucks prefer the big roads; U-turns only at dead ends.</summary>
        private TrafficLink ChooseNext(AmbientVehicle v, TrafficLink link)
        {
            if (link.Next.Count == 0) return null;
            if (link is TrafficConnector || link.Next.Count == 1) return link.Next[0];

            bool heavy = v.Kind == AmbientVehicleKind.Bus || v.Kind == AmbientVehicleKind.Truck;
            float total = 0f;
            if (weights.Length < link.Next.Count) weights = new float[link.Next.Count];
            for (int i = 0; i < link.Next.Count; i++)
            {
                var movement = (TrafficConnector)link.Next[i];
                float weight = movement.Turn switch
                {
                    TurnKind.Straight => 4f,
                    TurnKind.UTurn => 0.1f,
                    _ => 2f
                };
                if (movement.To.Class == RoadClass.Highway) weight *= heavy ? 3f : 1.3f;
                else if (movement.To.Class == RoadClass.Avenue) weight *= heavy ? 2f : 1.2f;
                weights[i] = weight;
                total += weight;
            }
            float pick = (float)random.NextDouble() * total;
            for (int i = 0; i < link.Next.Count; i++)
            {
                pick -= weights[i];
                if (pick <= 0f) return link.Next[i];
            }
            return link.Next[link.Next.Count - 1];
        }

        private void Advance(AmbientVehicle v, float move)
        {
            v.Front += move;
            while (v.Front > v.Link.Length)
            {
                if (v.Ahead.Count == 0) EnsureAhead(v, 1f);
                if (v.Ahead.Count == 0)
                {
                    v.Done = true; // the road leaves the map here
                    return;
                }
                v.Front -= v.Link.Length;
                v.Link.Occupants.Remove(v);
                v.Behind.Add(v.Link);
                v.Link = v.Ahead[0];
                v.Ahead.RemoveAt(0);
                v.Link.Occupants.Add(v);
                if (v.Link is TrafficConnector movement) EnteredJunction?.Invoke(v, movement);
                else
                {
                    v.BusStopIndex = 0;
                    v.StoppedAtSign = false;
                }
            }
            v.TrimBehind();

            TrafficConnector held = v.Held;
            if (held != null && v.Link != held && v.Link != held.From && (v.Link != held.To || v.Front - v.Length > 0.3f))
                Release(v);
        }

        // ------------------------------------------------------------------ obstacles

        private void GatherCandidates(AmbientVehicle v, float lookahead)
        {
            QueryBodies(v.Position, lookahead + v.Length + 14f, candidates);
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                Body body = bodies[candidates[i]];
                if (body.Vehicle == v) candidates.RemoveAt(i);
            }
        }

        /// <summary>
        /// How far the front can still move with the given sideways offset before the body comes within
        /// <see cref="BodyGap"/> of another one (sampled every meter, then every two). Bodies it is already
        /// moving away from never block it.
        /// </summary>
        private float FreeDistance(AmbientVehicle v, float offset, float lookahead, out int blocker, out float leaderSpeed)
        {
            blocker = -1;
            leaderSpeed = 0f;
            if (candidates.Count == 0) return lookahead;
            Footprint now = v.Footprint;
            float x = 0f;
            while (x < lookahead)
            {
                float next = Math.Min(lookahead, x + (x < 12f ? 1f : 2f));
                Footprint probe = v.PoseAt(v.Front + next, next <= 2f ? v.Offset : offset);
                int hit = FirstHit(v, probe, now);
                if (hit >= 0)
                {
                    blocker = hit;
                    Footprint theirs = BodyFootprint(hit);
                    leaderSpeed = Math.Max(0f, BodySpeed(hit) * Vector2.Dot(theirs.Forward, v.Heading));
                    return x;
                }
                x = next;
            }
            return lookahead;
        }

        private int FirstHit(AmbientVehicle v, Footprint probe, Footprint now)
        {
            float reach = Radius2(probe) + BodyGap;
            foreach (int index in candidates)
            {
                Footprint theirs = BodyFootprint(index);
                float limit = reach + Radius2(theirs);
                if (Vector2.DistanceSquared(probe.Centre, theirs.Centre) > limit * limit) continue;
                bool passThrough = bodies[index].Vehicle != null && bodies[index].Vehicle == v.PassThrough;
                float gap = passThrough ? 0.02f : BodyGap;
                if (!Overlaps(probe, theirs, gap)) continue;
                if (Separation(probe, theirs) < Separation(now, theirs) - 1e-3f) return index;
            }
            return -1;
        }

        /// <summary>True when the body can stand at a sideways offset (and every step on the way there) right now.</summary>
        private bool SideClear(AmbientVehicle v, float offset)
        {
            float from = v.Offset;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(offset - from) / 0.5f));
            for (int i = 1; i <= steps; i++)
            {
                Footprint pose = v.PoseAt(v.Front, from + (offset - from) * i / steps);
                foreach (int index in candidates)
                {
                    Footprint theirs = BodyFootprint(index);
                    if (Vector2.DistanceSquared(pose.Centre, theirs.Centre) > 400f) continue;
                    if (Overlaps(pose, theirs, 0.3f)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Remembers who blocks the vehicle and whether that is a parked vehicle (or a queue behind one),
        /// which may be overtaken. Two vehicles that wait for each other for a while: the older one squeezes past.
        /// </summary>
        private void TrackBlocker(AmbientVehicle v, int blocker, float obstacle, float dt)
        {
            AmbientVehicle other = blocker >= 0 ? bodies[blocker].Vehicle : null;
            v.BlockerBody = blocker;
            v.BlockerVehicle = other;

            v.BehindParked = false;
            if (blocker >= 0 && obstacle < 10f && v.Link is TrafficLane lane)
            {
                if (other == null)
                {
                    SharedRoadVehicle external = externals[bodies[blocker].External];
                    if (external.IsParked)
                    {
                        v.BehindParked = true;
                        v.ChainEnd = ProjectedRange(lane, external.Footprint).Front;
                    }
                }
                else if (other.Link == lane && other.IsParked)
                {
                    v.BehindParked = true;
                    v.ChainEnd = other.Front;
                }
                else if (other.Link == lane && other.BehindParked && other.Speed < 0.5f && other.Overtake == null)
                {
                    v.BehindParked = true;
                    v.ChainEnd = other.ChainEnd;
                }
            }

            v.BlockedSeconds = other != null && other == v.PreviousBlocker && v.Speed < 0.2f ? v.BlockedSeconds + dt : 0f;
            v.PreviousBlocker = other;
            if (v.PassThroughLeft > 0f && (v.PassThroughLeft -= dt) <= 0f) v.PassThrough = null;
            if (other != null && v.BlockedSeconds > MutualWaitSeconds && other.BlockerVehicle == v && v.Id < other.Id)
            {
                v.PassThrough = other;
                v.PassThroughLeft = 4f;
                v.BlockedSeconds = 0f;
            }
        }

        // ------------------------------------------------------------------ rules

        /// <summary>Distance the front may still move because of the traffic rules (float.MaxValue when none applies).</summary>
        private float RuleLimit(AmbientVehicle v, float lookahead, float obstacle, float dt)
        {
            float limit = float.MaxValue;
            JunctionLimit(v, lookahead, ref limit);

            if (v.Link is TrafficLane lane)
            {
                // "Do not block": do not stop inside the box; wait before it until there is room after it. Nobody
                // enters it while a CEDI truck is moving through it (turning in or pulling out of the gate);
                // vehicles overtaking in the other lane pass by.
                bool overtaking = v.Overtake != null && v.Offset < -3f;
                foreach ((float start, float end) in lane.KeepClear)
                {
                    if (overtaking || v.Front > start - 0.05f || start - v.Front > lookahead + 5f) continue;
                    float stopsAt = v.Front + Math.Min(obstacle, limit);
                    if (obstacle < lookahead && stopsAt < end + v.Length + 1f || IsSharedVehicleMovingIn(lane, start, end))
                        limit = Math.Min(limit, Math.Max(0f, start - v.Front));
                }

                // A stretch borrowed by someone overtaking against this lane's direction.
                foreach (OvertakeSpan span in laneSpans[lane.Id])
                    if (v.Front < span.Start + 0.1f)
                        limit = Math.Min(limit, Math.Max(0f, span.Start - v.Front - 1f));

                if (v.Kind == AmbientVehicleKind.Bus) BusStopLimit(v, lane, dt, ref limit);
                if (v.IsMoto && Math.Max(Math.Abs(v.Offset), Math.Abs(v.TargetOffset)) > 0.8f) MotoBesideLimit(v, lane, ref limit);
            }

            // An overtaking vehicle stays inside the stretch it borrowed.
            if (v.Overtake != null && v.Offset < -0.5f)
                limit = Math.Min(limit, Math.Max(0f, v.OvertakeEnd + 3f - v.Front));
            return limit;
        }

        /// <summary>
        /// Near a crossing a filtering moto never stands beside a car, bus or truck (which may turn across it):
        /// it gets completely in front of it (the moto box at lights) or waits behind its rear.
        /// </summary>
        private static void MotoBesideLimit(AmbientVehicle v, TrafficLane lane, ref float limit)
        {
            const float zone = 15f, margin = 0.5f;
            float line = lane.Length - lane.StopLineBack - 0.5f;
            if (lane.Length - v.Front > zone + 10f || v.Held?.From == lane) return; // far away, or already going
            float allowed = Math.Min(line, v.Front + limit);
            foreach (AmbientVehicle other in lane.Occupants)
            {
                if (other.IsMoto || other == v || lane.Length - other.Front > zone) continue;
                float rear = other.Front - other.Length;
                if (other.Front <= v.Front - v.Length + 0.6f) continue; // the moto is already in front of it
                bool fitsInFront = other.Front + margin + v.Length <= allowed;
                if (!fitsInFront) allowed = Math.Min(allowed, rear - margin);
            }
            // Someone already turning whose tail is still in the lane: stay behind the tail.
            if (lane.EndJunction != null)
                foreach (AmbientVehicle holder in lane.EndJunction.Holders)
                    if (!holder.IsMoto && holder.Link is TrafficConnector on && on.From == lane && holder.Front < holder.Length)
                        allowed = Math.Min(allowed, lane.Length + holder.Front - holder.Length - margin);
            limit = Math.Min(limit, Math.Max(0f, allowed - v.Front));
        }

        /// <summary>A vehicle of the other simulation (a CEDI truck), not waiting, touches this stretch of the lane.</summary>
        private bool IsSharedVehicleMovingIn(TrafficLane lane, float start, float end)
        {
            if (externals.Count == 0) return false;
            float laneWidth = lane.TwoWay ? lane.HalfWidth : 2f * lane.HalfWidth;
            var area = new Footprint(lane.PointAt((start + end) / 2f), lane.Direction, end - start, laneWidth);
            foreach (SharedRoadVehicle external in externals)
                if (!external.IsParked && Overlaps(area, external.Footprint, 0f)) return true;
            return false;
        }

        private void BusStopLimit(AmbientVehicle v, TrafficLane lane, float dt, ref float limit)
        {
            if (v.BusStopIndex >= lane.BusStops.Count) return;
            float stop = lane.BusStops[v.BusStopIndex];
            if (v.DwellLeft > 0f)
            {
                limit = 0f;
                v.DwellLeft -= dt;
                if (v.DwellLeft > 0f) return;
                v.IsParked = false;
                v.BusStopIndex++;
                return;
            }
            float distance = stop - v.Front;
            if (Math.Abs(distance) < 1f && v.Speed < 0.3f)
            {
                v.DwellLeft = Settings.BusDwellSeconds;
                v.IsParked = true;
                limit = 0f;
                return;
            }
            if (distance < -1f) v.BusStopIndex++; // missed it (stopped short behind another bus)
            else limit = Math.Min(limit, Math.Max(0f, distance));
        }

        /// <summary>
        /// The next crossing movement on the route: the vehicle takes it (books it) when it is close, first in
        /// its lane and allowed in; otherwise it stops at the line (cars behind the moto box at lights).
        /// </summary>
        private void JunctionLimit(AmbientVehicle v, float lookahead, ref float limit)
        {
            // Booked on green but still before the line when it turns red (or, going straight or right, when the
            // left turners' last seconds begin): stop if there is room to.
            TrafficConnector booked = v.Held;
            if (booked != null && v.Link == booked.From && booked.Junction.Signal != null)
            {
                float toEnd = booked.From.Length - booked.From.StopLineBack - v.Front - 0.3f;
                float toStop = toEnd - (v.IsMoto ? 0f : CarStopBack);
                bool red = AspectFor(booked.From) == SignalAspect.Red && v.Speed * v.Speed / (2f * MaxDecel) < toEnd;
                bool leftTime = booked.Priority == 0 && InLeftTurnTime(booked.From)
                                && v.Speed * v.Speed / (2f * AmberDecel) < toStop;
                if (red || leftTime) Release(v);
            }

            float distance = v.Link.Length - v.Front;
            TrafficConnector movement = null;
            if (v.Link is TrafficLane && v.Ahead.Count > 0 && v.Ahead[0] is TrafficConnector first && first != v.Held)
                movement = first;
            else
            {
                for (int i = 0; i < v.Ahead.Count && distance <= lookahead + 15f; i++)
                {
                    if (v.Ahead[i] is TrafficConnector connector && connector != v.Held)
                    {
                        movement = connector;
                        break;
                    }
                    distance += v.Ahead[i].Length;
                }
            }
            if (movement == null || distance > lookahead + 15f) return;

            TrafficLane approach = movement.From;
            // Cars stop behind the moto box at lights; without lights a moto filtering at the side waits with
            // the cars too (a long vehicle turning in sweeps over that corner).
            bool besideLane = v.IsMoto && Math.Abs(v.Offset) > 1.2f;
            float stopBack = approach.HasSignal ? (v.IsMoto ? 0.5f : CarStopBack) : besideLane ? CarStopBack : 0.5f;
            stopBack += approach.StopLineBack; // before the zebra crossing
            float toLine = distance - stopBack;
            bool onApproach = v.Link == approach;

            if (onApproach && approach.HasStopSign && !v.StoppedAtSign && toLine < 1.5f && v.Speed < 0.3f)
                v.StoppedAtSign = true;

            bool close = toLine <= Math.Max(12f, v.Speed * v.Speed / (2f * v.Spec.ComfortDecel) + 8f);
            if (close && onApproach && IsFirstInLane(v, approach) && MayEnter(v, movement, toLine))
            {
                Acquire(v, movement);
                return;
            }
            limit = Math.Min(limit, Math.Max(0f, toLine));
        }

        /// <summary>No vehicle ahead on the lane that has not booked its movement yet (a moto level with a car counts as ahead).</summary>
        private static bool IsFirstInLane(AmbientVehicle v, TrafficLane lane)
        {
            foreach (AmbientVehicle other in lane.Occupants)
                if (other != v && other.Held?.From != lane && other.BlockerVehicle != v
                    && (other.Front > v.Front + 0.3f || other.IsMoto && !v.IsMoto && other.Front > v.Front - 0.3f))
                    return false; // someone ahead, or a moto level with this car, goes first (unless it waits for this one)
            return true;
        }

        private bool MayEnter(AmbientVehicle v, TrafficConnector movement, float toLine)
        {
            TrafficJunction junction = movement.Junction;
            if (junction.Signal != null)
            {
                SignalAspect aspect = AspectFor(movement.From);
                if (aspect == SignalAspect.Red) return false;
                // On amber: go only if it cannot stop any more, or if it already stands at the line (a left
                // turner that waited for the oncoming flow clears the crossing then).
                bool canStop = v.Speed * v.Speed / (2f * AmberDecel) < toLine - 0.5f;
                bool waitingAtLine = v.Speed < 0.5f && toLine < 1f && movement.Priority > 0;
                if (aspect == SignalAspect.Amber && canStop && !waitingAtLine) return false;
                // The last seconds of green belong to the left turners: straight on and right wait if they can.
                if (movement.Priority == 0 && InLeftTurnTime(movement.From) && (canStop || v.Speed < 0.5f)) return false;
            }
            else if (movement.From.HasStopSign && !v.StoppedAtSign) return false;

            foreach (AmbientVehicle holder in junction.Holders)
            {
                if (holder == v) continue;
                if (movement.conflicts.Contains(holder.Held)) return false;
                if (holder.Held.From != movement.From || !IsStillBeside(holder, movement.From, v.Length + 4f)) continue;
                // From the same lane, two vehicles enter side by side only when both are motos going straight, and
                // never beside a car, bus or truck, which may swing across them. A moto level with a car at the
                // line goes first instead.
                bool sameWay = holder.Held == movement && holder.IsMoto && v.IsMoto && movement.Turn == TurnKind.Straight;
                bool motoLevelAhead = holder.Link == movement.From && v.Front >= holder.Front - 0.3f;
                if (!sameWay && !motoLevelAhead && holder.BlockerVehicle != v) return false; // (it waits for this one)
            }

            foreach (TrafficConnector other in movement.conflicts)
                if (other.Priority < movement.Priority && AspectFor(other.From) != SignalAspect.Red && IsApproaching(other, v))
                    return false;

            return HasExitSpace(v, movement);
        }

        private bool InLeftTurnTime(TrafficLane lane)
        {
            TrafficSignal signal = lane.EndJunction?.Signal;
            if (signal == null) return false;
            float left = signal.GreenLeft(lane.SignalGroup, Time);
            return left > 0f && left < LeftTurnSeconds;
        }

        /// <summary>True while the vehicle's rear is less than <paramref name="clearance"/> past the end of <paramref name="lane"/>.</summary>
        private static bool IsStillBeside(AmbientVehicle vehicle, TrafficLane lane, float clearance)
        {
            if (vehicle.Link == lane) return true;
            if (vehicle.Link is TrafficConnector connector && connector.From == lane)
                return vehicle.Front - vehicle.Length < clearance;
            return false;
        }

        /// <summary>Someone about to take <paramref name="movement"/> (a few seconds away), who has priority.</summary>
        private bool IsApproaching(TrafficConnector movement, AmbientVehicle me)
        {
            TrafficLane lane = movement.From;
            foreach (AmbientVehicle other in lane.Occupants)
            {
                if (other == me || other.NextMovement != movement) continue;
                // One standing in a queue behind someone else cannot come now (e.g. behind a left turner).
                if (other.Speed < 1f && !IsFirstInLane(other, lane)) continue;
                float distance = lane.Length - other.Front;
                // On amber (or in the left turners' last seconds of green), one that stands or can still stop will stop.
                bool willStop = other.Speed < 0.5f || other.Speed * other.Speed / (2f * AmberDecel) < distance - 0.5f;
                if (willStop && (AspectFor(lane) == SignalAspect.Amber || movement.Priority == 0 && InLeftTurnTime(lane))) continue;
                if (distance < 6f + other.Speed * 3.5f) return true;
            }
            if (movement.Turn == TurnKind.Straight)
                foreach (ExternalOnLane external in laneExternals[lane.Id])
                    if (lane.Length - external.Front < 6f + external.Speed * 3.5f) return true;
            return false;
        }

        /// <summary>"Do not block the crossing": enter only when the exit lane has room for the whole body.</summary>
        private bool HasExitSpace(AmbientVehicle v, TrafficConnector movement)
        {
            TrafficLane to = movement.To;
            float free = to.Length;
            foreach (AmbientVehicle other in to.Occupants)
                if (other.Speed <= 4f) free = Math.Min(free, other.Front - other.Length);
            foreach (ExternalOnLane external in laneExternals[to.Id])
                if (external.Speed <= 4f) free = Math.Min(free, external.Rear);

            float needed = v.Length + 2f;
            foreach (AmbientVehicle holder in movement.Junction.Holders)
                if (holder != v && holder.Held.To == to && holder.Link != to) needed += holder.Length + 2f;
            foreach (OvertakeSpan span in laneSpans[to.Id])
                if (span.Start < needed + 15f) return false;
            return free >= needed;
        }

        private static void Acquire(AmbientVehicle v, TrafficConnector movement)
        {
            Release(v);
            v.Held = movement;
            movement.Junction.Holders.Add(v);
            v.StoppedAtSign = false;
        }

        private static void Release(AmbientVehicle v)
        {
            if (v.Held == null) return;
            v.Held.Junction.Holders.Remove(v);
            v.Held = null;
        }
    }
}
