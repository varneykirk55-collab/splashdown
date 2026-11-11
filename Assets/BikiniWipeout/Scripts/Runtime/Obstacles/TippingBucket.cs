using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Obstacle 06: a suspended bucket that fills, wobbles as a warning, then dumps its water on the
    /// path. Anyone underneath is knocked down in <see cref="pushDirection"/> through the normal
    /// impact pipeline (<see cref="IImpactReceiver"/>), so thresholds, ragdoll and recovery are shared.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TippingBucket : MonoBehaviour, IResettable
    {
        [Header("Timing (seconds)")]
        [SerializeField, Min(1f)] internal float period = 4.5f;
        [SerializeField, Min(0f)] internal float warningTime = 1f;
        [SerializeField, Min(0.1f)] internal float activeTime = 0.9f;
        [SerializeField] internal float cycleOffset;

        [Header("Knockback")]
        [Tooltip("Direction the water throws the character, in this object's local space.")]
        [SerializeField] internal Vector3 pushDirection = Vector3.right;

        [Tooltip("Speed of the water when it lands (m/s). Above the knockdown threshold by default.")]
        [SerializeField, Min(0f)] internal float waterSpeed = 7f;

        [SerializeField, Min(0f)] internal float forceMultiplier = 1.1f;

        [Tooltip("Centre and half size of the splash zone under the bucket, in local space.")]
        [SerializeField] internal Vector3 zoneCenter = new Vector3(0f, 1.4f, 0f);
        [SerializeField] internal Vector3 zoneHalfExtents = new Vector3(2f, 1.6f, 1.3f);

        [Tooltip("Acceleration applied to ragdoll bones in the splash zone (m/s^2).")]
        [SerializeField, Min(0f)] internal float ragdollAcceleration = 22f;

        [Header("Visuals and audio (optional)")]
        [Tooltip("The bucket itself. It tips towards the push direction around this transform's origin.")]
        [SerializeField] internal Transform bucket;
        [SerializeField, Range(0f, 150f)] internal float tipAngle = 115f;
        [SerializeField] internal ParticleSystem waterEffect;
        [SerializeField] internal AudioEventId warningEvent = AudioEventId.BucketWarning;
        [SerializeField] internal AudioEventId dumpEvent = AudioEventId.BucketDump;

        private Quaternion bucketRest = Quaternion.identity;
        private float elapsed;
        private HazardPhase phase = HazardPhase.Idle;
        private bool hitThisDump;
        private bool hasDumped;

        public HazardPhase Phase => phase;

        /// <summary>How many times the bucket has knocked down or shoved the character.</summary>
        public int HitCount { get; private set; }

        private HazardCycle Cycle => new HazardCycle(period, warningTime, activeTime, cycleOffset);

        private Vector3 WorldPush
        {
            get
            {
                Vector3 world = Vector3.ProjectOnPlane(transform.TransformDirection(pushDirection), Vector3.up);
                return world.sqrMagnitude > 0.0001f ? world.normalized : transform.right;
            }
        }

        private void Awake()
        {
            if (bucket != null)
            {
                bucketRest = bucket.localRotation;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            HazardPhase next = Cycle.Evaluate(elapsed, out float progress);
            if (next != phase)
            {
                phase = next;
                if (phase == HazardPhase.Warning)
                {
                    AudioManager.Play(warningEvent, transform.position);
                }
                else if (phase == HazardPhase.Active)
                {
                    hitThisDump = false;
                    hasDumped = true;
                    AudioManager.Play(dumpEvent, transform.position);
                    if (waterEffect != null)
                    {
                        waterEffect.Play(true);
                    }
                }
            }

            AnimateBucket(progress);

            // The water needs a moment to fall from the bucket to the deck.
            if (phase == HazardPhase.Active && progress > 0.15f && !hitThisDump)
            {
                TryHitPlayer();
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

            Vector3 push = (WorldPush + Vector3.down * 0.4f).normalized * ragdollAcceleration;
            for (int i = 0; i < ragdoll.Bodies.Count; i++)
            {
                Rigidbody bone = ragdoll.Bodies[i];
                if (bone != null && !bone.isKinematic && InZone(bone.worldCenterOfMass))
                {
                    bone.AddForce(push, ForceMode.Acceleration);
                }
            }
        }

        [ContextMenu("Reset")]
        public void ResetState()
        {
            elapsed = 0f;
            phase = HazardPhase.Idle;
            hitThisDump = false;
            hasDumped = false;
            if (bucket != null)
            {
                bucket.localRotation = bucketRest;
            }
        }

        /// <summary>True if a world point is inside the splash zone.</summary>
        public bool InZone(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint) - zoneCenter;
            return Mathf.Abs(local.x) <= zoneHalfExtents.x && Mathf.Abs(local.y) <= zoneHalfExtents.y
                && Mathf.Abs(local.z) <= zoneHalfExtents.z;
        }

        private void TryHitPlayer()
        {
            if (!PlayerLocator.TryGetActiveMotor(out PlayerCharacter player, out CharacterMotor motor))
            {
                return;
            }

            Vector3 body = motor.transform.position + Vector3.up * 0.9f;
            if (!InZone(body))
            {
                return;
            }

            Vector3 push = WorldPush;
            hitThisDump = true;
            IImpactReceiver receiver = player.Knockdown;
            if (receiver == null)
            {
                motor.AddExternalVelocity(push * waterSpeed);
                HitCount++;
                return;
            }

            var impact = new ImpactData
            {
                Point = receiver.ImpactCenter - push * 0.3f + Vector3.up * 0.4f,
                Normal = push,
                // Relative to the character, so the hit is the same whether it runs with or against the water.
                SourceVelocity = motor.Velocity + push * waterSpeed,
                ForceMultiplier = forceMultiplier,
                Kind = ImpactSourceKind.Generic,
                Source = gameObject
            };

            if (receiver.ReceiveImpact(in impact))
            {
                HitCount++;
            }
        }

        private void AnimateBucket(float progress)
        {
            if (bucket == null)
            {
                return;
            }

            float angle;
            switch (phase)
            {
                case HazardPhase.Warning:
                    // A growing wobble: the visible cue that the bucket is about to go.
                    angle = Mathf.Sin(progress * Mathf.PI * 7f) * 9f * progress;
                    break;
                case HazardPhase.Active:
                    angle = tipAngle * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 3.5f));
                    break;
                default:
                    // Swing back upright early in the idle phase while it "refills".
                    angle = hasDumped ? tipAngle * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 4f))) : 0f;
                    break;
            }

            Vector3 localPush = Vector3.ProjectOnPlane(pushDirection, Vector3.up);
            Vector3 axis = localPush.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, localPush.normalized) : Vector3.forward;
            bucket.localRotation = bucketRest * Quaternion.AngleAxis(angle, Quaternion.Inverse(bucketRest) * axis);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(zoneCenter, zoneHalfExtents * 2f);
            Gizmos.DrawLine(zoneCenter, zoneCenter + pushDirection.normalized * 2f);
        }
    }
}
