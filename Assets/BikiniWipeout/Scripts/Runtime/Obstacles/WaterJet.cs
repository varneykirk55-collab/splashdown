using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    public enum WaterJetMode
    {
        /// <summary>Fires along the object's up axis and throws the character into the air.</summary>
        Geyser,

        /// <summary>Fires along the object's forward axis and pushes the character sideways.</summary>
        Push
    }

    /// <summary>
    /// Obstacle 05: a timed water jet. It bubbles as a warning, then fires for a short time. A geyser
    /// launches whoever stands over it; a side jet pushes across the path. The timing is a pure
    /// function of elapsed time, so the pattern can be learned and resets exactly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterJet : MonoBehaviour, IResettable
    {
        [SerializeField] internal WaterJetMode mode = WaterJetMode.Geyser;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.5f)] internal float period = 3.2f;
        [SerializeField, Min(0f)] internal float warningTime = 0.7f;
        [SerializeField, Min(0.05f)] internal float activeTime = 1.1f;

        [Tooltip("Shifts the cycle. Use it to stagger a row of jets.")]
        [SerializeField] internal float cycleOffset;

        [Header("Reach")]
        [Tooltip("Radius of the water column (m).")]
        [SerializeField, Min(0.1f)] internal float radius = 1f;

        [Tooltip("Length of the water column from the nozzle (m).")]
        [SerializeField, Min(0.1f)] internal float range = 4.5f;

        [Header("Geyser")]
        [Tooltip("Upward speed given to a character caught over the jet (m/s).")]
        [SerializeField, Min(0f)] internal float launchSpeed = 11f;

        [Tooltip("Sideways speed added away from the centre of the jet (m/s).")]
        [SerializeField, Min(0f)] internal float sideKick = 3.5f;

        [Header("Push")]
        [Tooltip("How quickly the jet accelerates a character along its direction (m/s^2).")]
        [SerializeField, Min(0f)] internal float pushAcceleration = 45f;

        [Tooltip("The jet stops accelerating a character that already moves this fast along it (m/s).")]
        [SerializeField, Min(0f)] internal float maxPushSpeed = 5.5f;

        [Header("Ragdoll")]
        [Tooltip("Acceleration applied to ragdoll bones inside the column (m/s^2), so wipeouts get juggled.")]
        [SerializeField, Min(0f)] internal float ragdollAcceleration = 28f;

        [Header("Effects (optional)")]
        [SerializeField] internal ParticleSystem warningEffect;
        [SerializeField] internal ParticleSystem activeEffect;
        [SerializeField] internal AudioEventId audioEvent = AudioEventId.WaterJet;

        private float elapsed;
        private HazardPhase phase = HazardPhase.Idle;
        private float nextLaunchTime;

        public HazardPhase Phase => phase;
        public bool IsFiring => phase == HazardPhase.Active;

        /// <summary>How many times this jet has launched or started pushing the character.</summary>
        public int HitCount { get; private set; }

        private HazardCycle Cycle => new HazardCycle(period, warningTime, activeTime, cycleOffset);

        private Vector3 Axis => mode == WaterJetMode.Geyser ? transform.up : transform.forward;

        private void OnEnable()
        {
            ApplyPhase(Cycle.Evaluate(elapsed, out _), true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            ApplyPhase(Cycle.Evaluate(elapsed, out _), false);
            if (phase != HazardPhase.Active || !PlayerLocator.TryGetActiveMotor(out _, out CharacterMotor motor))
            {
                return;
            }

            Vector3 feet = motor.transform.position;
            if (mode == WaterJetMode.Geyser)
            {
                if (Time.time < nextLaunchTime || !InColumn(feet + Vector3.up * 0.3f))
                {
                    return;
                }

                Vector3 away = Vector3.ProjectOnPlane(feet - transform.position, Vector3.up);
                if (away.sqrMagnitude < 0.01f)
                {
                    away = Vector3.ProjectOnPlane(transform.right, Vector3.up);
                }

                motor.Launch(launchSpeed);
                motor.AddExternalVelocity(away.normalized * sideKick);
                nextLaunchTime = Time.time + 0.6f;
                HitCount++;
                return;
            }

            if (!InColumn(feet + Vector3.up * 0.9f))
            {
                return;
            }

            Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction.Normalize();
            if (Time.time >= nextLaunchTime)
            {
                HitCount++;
            }

            nextLaunchTime = Time.time + 0.5f;
            if (Vector3.Dot(motor.Velocity, direction) < maxPushSpeed)
            {
                motor.AddExternalVelocity(direction * (pushAcceleration * Time.deltaTime));
            }
        }

        private void FixedUpdate()
        {
            if (phase != HazardPhase.Active || ragdollAcceleration <= 0f)
            {
                return;
            }

            RagdollController ragdoll = PlayerLocator.ActiveRagdoll(PlayerLocator.Current);
            if (ragdoll == null)
            {
                return;
            }

            Vector3 axis = Axis;
            for (int i = 0; i < ragdoll.Bodies.Count; i++)
            {
                Rigidbody bone = ragdoll.Bodies[i];
                if (bone != null && !bone.isKinematic && InColumn(bone.worldCenterOfMass))
                {
                    bone.AddForce(axis * ragdollAcceleration, ForceMode.Acceleration);
                }
            }
        }

        /// <summary>Returns the jet to the start of its cycle.</summary>
        [ContextMenu("Reset")]
        public void ResetState()
        {
            elapsed = 0f;
            nextLaunchTime = 0f;
            ApplyPhase(Cycle.Evaluate(0f, out _), true);
        }

        /// <summary>True if a world point is inside the water column.</summary>
        public bool InColumn(Vector3 worldPoint)
        {
            Vector3 offset = worldPoint - transform.position;
            float along = Vector3.Dot(offset, Axis);
            if (along < -0.3f || along > range)
            {
                return false;
            }

            return (offset - Axis * along).sqrMagnitude <= radius * radius;
        }

        private void ApplyPhase(HazardPhase next, bool force)
        {
            if (next == phase && !force)
            {
                return;
            }

            phase = next;
            SetPlaying(warningEffect, phase == HazardPhase.Warning);
            SetPlaying(activeEffect, phase == HazardPhase.Active);
            if (phase == HazardPhase.Active && !force)
            {
                AudioManager.Play(audioEvent, transform.position);
            }
        }

        private static void SetPlaying(ParticleSystem effect, bool playing)
        {
            if (effect == null)
            {
                return;
            }

            if (playing && !effect.isEmitting)
            {
                effect.Play(true);
            }
            else if (!playing && effect.isEmitting)
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Vector3 axis = mode == WaterJetMode.Geyser ? transform.up : transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + axis * range);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawWireSphere(transform.position + axis * range, radius);
        }
    }
}
