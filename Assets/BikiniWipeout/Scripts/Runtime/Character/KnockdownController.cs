using System;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Knockdown and recovery logic for a character. Receives impacts from any <see cref="ImpactSource"/>,
    /// decides between ignore / shove / knockdown, and coordinates the motor and the ragdoll:
    ///
    ///   impact -> ragdoll on, steering off -> body settles -> safe spot found -> get up -> steering on (+ immunity)
    ///
    /// The motor and the ragdoll do not know about each other; this class is the only link.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockdownController : MonoBehaviour, IImpactReceiver
    {
        [SerializeField] internal KnockdownConfig config;
        [SerializeField] internal CharacterMotor motor;
        [SerializeField] internal RagdollController ragdoll;

        [Tooltip("Height above the feet that obstacles aim at while the character is standing.")]
        [SerializeField, Min(0f)] internal float impactCenterHeight = 0.95f;

        private KnockdownStateMachine machine;
        private KnockdownPhase previousPhase = KnockdownPhase.Normal;
        private int failedRecoveryAttempts;

        public KnockdownPhase Phase => machine != null ? machine.Phase : KnockdownPhase.Normal;

        /// <summary>True from the moment of impact until control is handed back.</summary>
        public bool IsDown => Phase != KnockdownPhase.Normal;

        public bool IsRagdolled => machine != null && machine.IsSimulating;
        public bool RecoveryFaceUp { get; private set; }
        public bool IsImmune => machine != null && machine.IsImmune;
        public float TimeInPhase => machine != null ? machine.TimeInPhase : 0f;
        public KnockdownConfig Config => config;

        public Vector3 ImpactCenter
        {
            get
            {
                if (ragdoll != null && ragdoll.IsActive)
                {
                    return ragdoll.RootBonePosition;
                }

                return transform.position + Vector3.up * impactCenterHeight;
            }
        }

        /// <summary>Raised when an obstacle knocks the character down.</summary>
        public event Action<ImpactData> KnockedDown;

        /// <summary>Raised for impacts that only pushed the character.</summary>
        public event Action<ImpactData> Shoved;

        /// <summary>Raised when the character starts getting up.</summary>
        public event Action RecoveryStarted;

        /// <summary>Raised when control is handed back to the player.</summary>
        public event Action Recovered;

        /// <summary>
        /// Raised when no safe place to stand could be found after several attempts.
        /// The respawner answers by returning the character to the last checkpoint.
        /// </summary>
        public event Action RescueRequested;

        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<KnockdownConfig>();
                Debug.LogWarning("[KnockdownController] No KnockdownConfig assigned; using default values.", this);
            }

            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            if (ragdoll == null)
            {
                ragdoll = GetComponent<RagdollController>();
            }

            machine = new KnockdownStateMachine(config.timings);
            machine.PhaseChanged += OnPhaseChanged;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            // Re-read every frame so the config asset can be tuned live in Play mode.
            var timings = config.timings;
            if (ragdoll != null) timings.RecoveryDuration = Mathf.Max(timings.RecoveryDuration, ragdoll.Config.poseBlendTime);
            machine.SetTimings(timings);

            bool settled = ragdoll != null && ragdoll.IsActive
                && ragdoll.IsSettled(config.settleSpeed, config.settleAngularSpeed)
                && HasNearbySupport(ragdoll.RootBonePosition);
            machine.Tick(dt, settled);

            if (machine.IsSimulating && machine.WantsRecovery)
            {
                TryRecover();
            }
        }

        public bool ReceiveImpact(in ImpactData impact)
        {
            if (!isActiveAndEnabled || machine == null || machine.Phase != KnockdownPhase.Normal)
            {
                // While ragdolled the bones collide with obstacles physically; nothing more to decide.
                return false;
            }

            if (motor != null && motor.IsSuspended)
            {
                return false;
            }

            Vector3 ownVelocity = motor != null ? motor.Velocity : Vector3.zero;
            float closingSpeed = Mathf.Max(0f, Vector3.Dot(impact.SourceVelocity - ownVelocity, impact.Normal));
            ImpactResult result = ImpactEvaluator.Evaluate(closingSpeed, impact.ForceMultiplier, config.impact);
            if (result.Outcome == ImpactOutcome.None)
            {
                return false;
            }

            Vector3 pushDirection = FlatDirection(impact);

            if (result.Outcome == ImpactOutcome.Shove || !machine.CanBeKnockedDown)
            {
                // Too weak, or the character is immune: push it out of the way instead.
                if (motor != null)
                {
                    motor.AddExternalVelocity(pushDirection * (result.LaunchSpeed * config.shoveScale));
                }

                Shoved?.Invoke(impact);
                return true;
            }

            Vector3 impactDirection = impact.Normal.sqrMagnitude > 0.0001f ? impact.Normal.normalized : pushDirection;
            float horizontalWeight = Vector3.ProjectOnPlane(impactDirection, Vector3.up).magnitude;
            Vector3 launch = impactDirection * result.LaunchSpeed
                + Vector3.up * (result.LaunchSpeed * config.upwardLaunchRatio * horizontalWeight);
            if (!StartKnockdown(launch, impact.Point))
            {
                return false;
            }

            KnockedDown?.Invoke(impact);
            return true;
        }

        /// <summary>
        /// Knocks the character down directly (scripted events, tests). <paramref name="launchVelocity"/>
        /// is added to the character's current velocity. Returns false if a knockdown is not possible now.
        /// </summary>
        public bool ForceKnockdown(Vector3 launchVelocity, Vector3 worldPoint)
        {
            if (machine == null || !StartKnockdown(launchVelocity, worldPoint))
            {
                return false;
            }

            KnockedDown?.Invoke(new ImpactData
            {
                Point = worldPoint,
                Normal = launchVelocity.sqrMagnitude > 0.0001f ? launchVelocity.normalized : Vector3.up,
                SourceVelocity = launchVelocity,
                ForceMultiplier = 1f,
                Kind = ImpactSourceKind.Generic,
                Source = null
            });
            return true;
        }

        /// <summary>Cancels any knockdown immediately and grants respawn immunity. Used by respawns.</summary>
        public void ResetForRespawn()
        {
            failedRecoveryAttempts = 0;
            if (ragdoll != null)
            {
                ragdoll.Deactivate(false);
            }

            if (machine != null)
            {
                machine.ForceReset(config.respawnImmunity);
            }

            if (motor != null)
            {
                motor.SetControlLock(ControlLock.Knockdown, false);
            }
        }

        private bool StartKnockdown(Vector3 launchVelocity, Vector3 worldPoint)
        {
            if (ragdoll == null || ragdoll.Bodies.Count == 0 || (motor != null && motor.IsSuspended) || !machine.TryKnockdown())
            {
                return false;
            }

            failedRecoveryAttempts = 0;
            Vector3 inherited = Vector3.zero;
            if (motor != null)
            {
                inherited = motor.Velocity;
                motor.SetControlLock(ControlLock.Knockdown, true);
                motor.SetSuspended(true);
            }

            if (ragdoll != null)
            {
                ragdoll.Activate(inherited);
                ragdoll.ApplyLaunch(launchVelocity, worldPoint);
            }

            return true;
        }

        private void TryRecover()
        {
            Vector3 bodyPosition = ragdoll != null ? ragdoll.RootBonePosition : transform.position;

            if (HasNearbySupport(bodyPosition) && TryFindStandPoint(bodyPosition, out Vector3 standPoint))
            {
                RecoveryFaceUp = ragdoll != null && ragdoll.RootBody != null && ragdoll.RootBody.transform.forward.y > 0f;
                Quaternion facing = ComputeGetUpFacing();

                Action relocate = () =>
                {
                    if (motor != null)
                    {
                        motor.Teleport(standPoint, facing);
                        motor.SetSuspended(false);
                    }
                    else
                    {
                        transform.SetPositionAndRotation(standPoint, facing);
                    }
                };

                if (ragdoll != null)
                {
                    ragdoll.Deactivate(true, relocate);
                }
                else
                {
                    relocate();
                }

                machine.BeginRecovery();
                return;
            }

            failedRecoveryAttempts++;
            if (failedRecoveryAttempts >= config.maxRecoveryAttempts && RescueRequested != null)
            {
                failedRecoveryAttempts = 0;
                machine.DeferRecovery(config.recoveryRetryDelay);
                RescueRequested.Invoke();
                return;
            }

            machine.DeferRecovery(config.recoveryRetryDelay);
        }

        private void OnPhaseChanged(KnockdownPhase phase)
        {
            KnockdownPhase from = previousPhase;
            previousPhase = phase;

            if (phase == KnockdownPhase.Recovering)
            {
                RecoveryStarted?.Invoke();
            }
            else if (phase == KnockdownPhase.Normal && from == KnockdownPhase.Recovering)
            {
                if (motor != null)
                {
                    motor.SetControlLock(ControlLock.Knockdown, false);
                }

                Recovered?.Invoke();
            }
        }

        private Vector3 FlatDirection(in ImpactData impact)
        {
            Vector3 flat = Vector3.ProjectOnPlane(impact.Normal, Vector3.up);
            if (flat.sqrMagnitude < 0.01f)
            {
                flat = Vector3.ProjectOnPlane(impact.SourceVelocity, Vector3.up);
            }

            if (flat.sqrMagnitude < 0.01f)
            {
                flat = -transform.forward;
            }

            return flat.normalized;
        }

        private Quaternion ComputeGetUpFacing()
        {
            Vector3 forward = transform.forward;
            if (ragdoll != null && ragdoll.RootBody != null)
            {
                Transform hips = ragdoll.RootBody.transform;
                Vector3 flat = Vector3.ProjectOnPlane(hips.forward, Vector3.up);
                if (flat.sqrMagnitude < 0.2f)
                {
                    // Lying flat: face along the body. On the back -> towards the feet, face down -> towards the head.
                    flat = Vector3.ProjectOnPlane(hips.up, Vector3.up);
                    if (hips.forward.y > 0f)
                    {
                        flat = -flat;
                    }
                }

                if (flat.sqrMagnitude > 0.0001f)
                {
                    forward = flat;
                }
            }

            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
        }

        /// <summary>Finds solid, obstacle-free ground at or near <paramref name="origin"/> where the capsule fits.</summary>
        private bool TryFindStandPoint(Vector3 origin, out Vector3 point)
        {
            if (IsStandable(origin, out point))
            {
                return true;
            }

            for (int ring = 1; ring <= 2; ring++)
            {
                float radius = config.safeSearchRadius * ring * 0.5f;
                if (radius <= 0f)
                {
                    break;
                }

                for (int i = 0; i < 8; i++)
                {
                    Vector3 offset = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * radius;
                    if (IsStandable(origin + offset, out point))
                    {
                        return true;
                    }
                }
            }

            point = origin;
            return false;
        }

        private bool HasNearbySupport(Vector3 position)
        {
            return Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit,
                config.recoverySupportDistance + 0.1f, GameLayers.SafeGroundMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= 0.7f;
        }

        private bool IsStandable(Vector3 candidate, out Vector3 point)
        {
            point = candidate;
            Vector3 start = candidate + Vector3.up * config.groundProbeHeight;
            float distance = config.groundProbeHeight + config.groundProbeDepth;

            if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, distance,
                    GameLayers.SafeGroundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.normal.y < 0.7f || hit.point.y > candidate.y + 0.1f)
            {
                return false;
            }

            float radius = motor != null ? motor.CapsuleRadius : 0.3f;
            float height = motor != null ? motor.CapsuleHeight : 1.7f;
            const float lift = 0.06f;

            Vector3 center = motor != null && motor.Controller != null
                ? motor.Controller.center : Vector3.up * height * 0.5f;
            center += hit.point + Vector3.up * lift;
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 bottom = center - Vector3.up * halfSegment;
            Vector3 top = center + Vector3.up * halfSegment;
            float skin = motor != null && motor.Controller != null ? motor.Controller.skinWidth : 0.03f;
            if (Physics.CheckCapsule(bottom, top, radius + skin, GameLayers.SolidMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            point = hit.point + Vector3.up * lift;
            return true;
        }
    }
}
