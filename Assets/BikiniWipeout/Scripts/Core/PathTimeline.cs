using System;

namespace BikiniWipeout.Core
{
    public enum PathLoopMode
    {
        /// <summary>A -> B -> C -> B -> A ...</summary>
        PingPong,

        /// <summary>A -> B -> C -> A ... (the last segment returns to the first waypoint)</summary>
        Loop
    }

    public readonly struct PathSample
    {
        public PathSample(int fromIndex, int toIndex, float t)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            T = t;
        }

        public int FromIndex { get; }
        public int ToIndex { get; }

        /// <summary>0..1 progress between the two waypoints.</summary>
        public float T { get; }
    }

    /// <summary>
    /// Deterministic waypoint timeline: position is a pure function of elapsed time, so platforms
    /// reset predictably and never drift. Callers supply the distance of each leg and interpolate
    /// their own vectors with the returned sample.
    /// </summary>
    public sealed class PathTimeline
    {
        private readonly int[] legFrom;
        private readonly int[] legTo;
        private readonly float[] legTravelTime;
        private readonly float dwell;
        private readonly float cycleDuration;

        /// <param name="segmentLengths">Distance between waypoint i and i+1 (waypointCount - 1 entries).</param>
        /// <param name="closingLength">Distance from the last waypoint back to the first. Only used by Loop.</param>
        /// <param name="speed">Travel speed in units per second.</param>
        /// <param name="dwellSeconds">Pause at every waypoint.</param>
        public PathTimeline(float[] segmentLengths, float closingLength, float speed, float dwellSeconds, PathLoopMode mode)
        {
            if (segmentLengths == null)
            {
                throw new ArgumentNullException(nameof(segmentLengths));
            }

            WaypointCount = segmentLengths.Length + 1;
            dwell = Math.Max(0f, dwellSeconds);
            float safeSpeed = Math.Max(0.0001f, speed);
            int segments = segmentLengths.Length;

            if (segments == 0)
            {
                legFrom = new int[0];
                legTo = new int[0];
                legTravelTime = new float[0];
                cycleDuration = 0f;
                return;
            }

            int legCount = mode == PathLoopMode.PingPong ? segments * 2 : segments + 1;
            legFrom = new int[legCount];
            legTo = new int[legCount];
            legTravelTime = new float[legCount];

            for (int i = 0; i < segments; i++)
            {
                legFrom[i] = i;
                legTo[i] = i + 1;
                legTravelTime[i] = Math.Max(0f, segmentLengths[i]) / safeSpeed;
            }

            if (mode == PathLoopMode.PingPong)
            {
                for (int i = 0; i < segments; i++)
                {
                    int leg = segments + i;
                    int segment = segments - 1 - i;
                    legFrom[leg] = segment + 1;
                    legTo[leg] = segment;
                    legTravelTime[leg] = legTravelTime[segment];
                }
            }
            else
            {
                legFrom[segments] = segments;
                legTo[segments] = 0;
                legTravelTime[segments] = Math.Max(0f, closingLength) / safeSpeed;
            }

            float total = 0f;
            for (int i = 0; i < legCount; i++)
            {
                total += dwell + legTravelTime[i];
            }

            cycleDuration = total;
        }

        public int WaypointCount { get; }

        /// <summary>Seconds for one full cycle back to the starting state.</summary>
        public float CycleDuration => cycleDuration;

        public PathSample Evaluate(float time)
        {
            if (legFrom.Length == 0 || cycleDuration <= 0f)
            {
                return new PathSample(0, 0, 0f);
            }

            float t = time % cycleDuration;
            if (t < 0f)
            {
                t += cycleDuration;
            }

            for (int i = 0; i < legFrom.Length; i++)
            {
                // Each leg starts by waiting at its origin waypoint, then travels.
                if (t < dwell)
                {
                    return new PathSample(legFrom[i], legTo[i], 0f);
                }

                t -= dwell;
                float travel = legTravelTime[i];
                if (t < travel)
                {
                    return new PathSample(legFrom[i], legTo[i], travel <= 0f ? 1f : t / travel);
                }

                t -= travel;
            }

            // Floating point spill-over at the very end of the cycle.
            return new PathSample(legFrom[0], legTo[0], 0f);
        }

        /// <summary>Smoothstep easing so platforms accelerate and brake instead of snapping.</summary>
        public static float Ease(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }
    }
}
