using System;

namespace BikiniWipeout.Core
{
    /// <summary>
    /// How far the character can jump, derived from the same parameters the motor uses.
    /// Lets course layouts be validated against the character's real abilities.
    /// </summary>
    public readonly struct JumpEnvelope
    {
        public JumpEnvelope(float walkSpeed, float runSpeed, float jumpHeight, float gravity, float fallGravityMultiplier)
        {
            WalkSpeed = Math.Max(0f, walkSpeed);
            RunSpeed = Math.Max(0f, runSpeed);
            JumpHeight = Math.Max(0f, jumpHeight);
            Gravity = Math.Abs(gravity);
            FallGravityMultiplier = Math.Max(1f, fallGravityMultiplier);
        }

        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        public float JumpHeight { get; }
        public float Gravity { get; }
        public float FallGravityMultiplier { get; }

        /// <summary>The Foundation 1 defaults (PlayerMovementConfig).</summary>
        public static JumpEnvelope Default => new JumpEnvelope(3.2f, 6.5f, 1.4f, 24f, 1.35f);

        /// <summary>
        /// Seconds in the air for a full jump (button held) that lands <paramref name="rise"/> metres
        /// above (positive) or below (negative) the take-off height. Returns 0 if the rise is unreachable.
        /// </summary>
        public float AirTime(float rise)
        {
            if (Gravity <= 0f || rise >= JumpHeight)
            {
                return 0f;
            }

            double up = Math.Sqrt(2.0 * JumpHeight / Gravity);
            double down = Math.Sqrt(2.0 * (JumpHeight - rise) / (Gravity * FallGravityMultiplier));
            return (float)(up + down);
        }

        /// <summary>Horizontal distance covered by a jump at <paramref name="speed"/> landing <paramref name="rise"/> higher.</summary>
        public float Reach(float speed, float rise)
        {
            return Math.Max(0f, speed) * AirTime(rise);
        }

        public float RunReach(float rise)
        {
            return Reach(RunSpeed, rise);
        }

        public float WalkReach(float rise)
        {
            return Reach(WalkSpeed, rise);
        }
    }
}
