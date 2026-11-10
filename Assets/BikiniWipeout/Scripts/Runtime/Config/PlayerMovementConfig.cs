using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Tuning values for <see cref="CharacterMotor"/>. Edit the asset; changes apply live in Play mode.</summary>
    [CreateAssetMenu(menuName = "Bikini Wipeout/Player Movement Config", fileName = "PlayerMovementConfig")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Speed (m/s)")]
        [Min(0f)] public float walkSpeed = 3.2f;
        [Min(0f)] public float runSpeed = 6.5f;

        [Tooltip("Off: walk by default, hold Run to run. On: run by default, hold Run to walk.")]
        public bool runByDefault;

        [Header("Acceleration (m/s²)")]
        [Min(0f)] public float groundAcceleration = 34f;
        [Min(0f)] public float groundDeceleration = 40f;
        [Min(0f)] public float airAcceleration = 14f;

        [Tooltip("How quickly horizontal speed bleeds off in the air when no direction is held.")]
        [Min(0f)] public float airDeceleration = 3f;

        [Header("Turning (degrees/s)")]
        [Min(0f)] public float groundTurnSpeed = 760f;
        [Min(0f)] public float airTurnSpeed = 420f;

        [Header("Jump and gravity")]
        [Min(0f)] public float jumpHeight = 1.4f;

        [Tooltip("Negative. Stronger than real gravity for a snappier arc.")]
        public float gravity = -24f;

        [Tooltip("Extra gravity while falling.")]
        [Min(1f)] public float fallGravityMultiplier = 1.35f;

        [Tooltip("Extra gravity while rising with the jump button released (short hop).")]
        [Min(1f)] public float lowJumpGravityMultiplier = 1.8f;

        [Min(0f)] public float terminalFallSpeed = 28f;

        [Tooltip("Jump is still allowed this long after walking off a ledge.")]
        [Min(0f)] public float coyoteTime = 0.12f;

        [Tooltip("A jump pressed this long before landing still triggers.")]
        [Min(0f)] public float jumpBufferTime = 0.12f;

        [Header("Ground detection")]
        [Tooltip("Downward speed applied while grounded so the capsule stays glued to slopes and platforms.")]
        [Min(0f)] public float groundStickSpeed = 2.5f;

        [Tooltip("How far below the capsule the ground probe reaches.")]
        [Min(0.01f)] public float groundProbeDistance = 0.14f;

        [Tooltip("Falling slower than this on touchdown does not count as a landing (no sound / animation).")]
        [Min(0f)] public float minLandingSpeed = 2.5f;

        [Header("External pushes (shoves, platform momentum)")]
        [Min(0f)] public float externalGroundDamping = 14f;
        [Min(0f)] public float externalAirDamping = 1.5f;

        public float JumpSpeed => Mathf.Sqrt(2f * Mathf.Abs(gravity) * Mathf.Max(0f, jumpHeight));
    }
}
