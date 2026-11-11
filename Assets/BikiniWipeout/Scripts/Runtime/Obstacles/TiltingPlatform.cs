using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    public enum TiltMode
    {
        /// <summary>Seesaw: tilts about the local X axis as the character moves along local Z.</summary>
        Seesaw,

        /// <summary>Floating raft: leans towards wherever the character stands.</summary>
        Raft
    }

    /// <summary>
    /// Obstacle 03 (seesaw) and the deck of obstacle 11 (floating arena). A kinematic platform that
    /// tilts under the character's weight. The tilt is driven by a clamped, damped spring
    /// (<see cref="TiltSolver"/>) rather than free rigidbody physics, so it stays inside its limits,
    /// returns to level when unloaded and cannot explode. The motor rides it like any moving platform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TiltingPlatform : MonoBehaviour, IResettable
    {
        [Tooltip("Kinematic Rigidbody of the deck. Its origin is the pivot.")]
        [SerializeField] internal Rigidbody body;

        [SerializeField] internal TiltMode mode = TiltMode.Seesaw;

        [Tooltip("Largest tilt in degrees.")]
        [SerializeField, Range(0f, 30f)] internal float maxTilt = 9f;

        [Tooltip("Distance from the pivot (m) at which the character's weight produces the full tilt.")]
        [SerializeField, Min(0.1f)] internal float halfLength = 4.5f;

        [Tooltip("Spring strength. Higher = the deck reacts faster.")]
        [SerializeField, Min(0f)] internal float stiffness = 14f;

        [Tooltip("Spring damping. Higher = less rocking.")]
        [SerializeField, Min(0f)] internal float damping = 5.5f;

        [Tooltip("Fastest the deck may rotate (degrees per second).")]
        [SerializeField, Min(1f)] internal float maxTiltSpeed = 26f;

        [Tooltip("Gentle rocking when nobody is on it (degrees). 0 = perfectly still.")]
        [SerializeField, Min(0f)] internal float idleSway;

        [SerializeField, Min(0.5f)] internal float idleSwayPeriod = 5f;

        private Quaternion restRotation;
        private Vector3 restPosition;
        private float angleX, angleZ, velocityX, velocityZ;
        private float elapsed;

        /// <summary>Current tilt (degrees) about the local X and Z axes.</summary>
        public Vector2 Tilt => new Vector2(angleX, angleZ);

        public float MaxTilt => maxTilt;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponentInChildren<Rigidbody>();
            }

            if (body != null)
            {
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                restRotation = body.transform.rotation;
                restPosition = body.transform.position;
            }
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            elapsed += dt;
            float targetX = 0f;
            float targetZ = 0f;

            if (PlayerLocator.TryGetActiveMotor(out _, out CharacterMotor motor) && PlayerLocator.IsStandingOn(motor, body.transform))
            {
                // Where the character stands, measured in the level (rest) frame of the deck.
                Vector3 local = Quaternion.Inverse(restRotation) * (motor.transform.position - restPosition);
                // Positive rotation about X lowers the +Z end; negative rotation about Z lowers the +X side.
                targetX = Mathf.Clamp(local.z / halfLength, -1f, 1f) * maxTilt;
                if (mode == TiltMode.Raft)
                {
                    targetZ = -Mathf.Clamp(local.x / halfLength, -1f, 1f) * maxTilt;
                }
            }
            else if (idleSway > 0f)
            {
                float t = elapsed / idleSwayPeriod * Mathf.PI * 2f;
                targetX = Mathf.Sin(t) * idleSway;
                targetZ = mode == TiltMode.Raft ? Mathf.Cos(t * 0.8f) * idleSway : 0f;
            }

            TiltSolver.Step(ref angleX, ref velocityX, targetX, stiffness, damping, maxTilt, maxTiltSpeed, dt);
            TiltSolver.Step(ref angleZ, ref velocityZ, targetZ, stiffness, damping, maxTilt, maxTiltSpeed, dt);
            body.MoveRotation(restRotation * Quaternion.Euler(angleX, 0f, angleZ));
        }

        /// <summary>Returns the deck to level.</summary>
        [ContextMenu("Reset")]
        public void ResetState()
        {
            angleX = angleZ = velocityX = velocityZ = 0f;
            elapsed = 0f;
            if (body != null)
            {
                body.transform.rotation = restRotation;
                body.rotation = restRotation;
            }
        }
    }
}
