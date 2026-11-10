using System;

namespace BikiniWipeout.Core
{
    /// <summary>Small engine-free helpers used by the character motor.</summary>
    public static class MovementMath
    {
        /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
        public static float Approach(float current, float target, float maxDelta)
        {
            if (maxDelta <= 0f)
            {
                return current;
            }

            float diff = target - current;
            if (Math.Abs(diff) <= maxDelta)
            {
                return target;
            }

            return current + Math.Sign(diff) * maxDelta;
        }

        /// <summary>Initial upward speed needed to reach <paramref name="height"/> under <paramref name="gravity"/>.</summary>
        public static float JumpSpeedForHeight(float height, float gravity)
        {
            if (height <= 0f || gravity == 0f)
            {
                return 0f;
            }

            return (float)Math.Sqrt(2.0 * Math.Abs(gravity) * height);
        }
    }

    /// <summary>
    /// Remembers a momentary condition for a short grace period. Used for jump buffering
    /// (pressed slightly before landing) and coyote time (pressed slightly after leaving a ledge).
    /// </summary>
    public struct GraceTimer
    {
        private float remaining;

        public bool IsActive => remaining > 0f;

        public void Start(float duration)
        {
            remaining = Math.Max(0f, duration);
        }

        public void Tick(float deltaTime)
        {
            if (remaining > 0f)
            {
                remaining = Math.Max(0f, remaining - deltaTime);
            }
        }

        public void Clear()
        {
            remaining = 0f;
        }
    }
}
