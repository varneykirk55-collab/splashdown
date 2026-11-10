using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Maps character events to audio events: footsteps (by distance travelled), jump, landing,
    /// obstacle impacts and ragdoll body collisions. With real animations, footsteps can instead be
    /// driven by animation events calling <see cref="PlayFootstep"/> (set <see cref="distanceFootsteps"/> off).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAudio : MonoBehaviour
    {
        [SerializeField] internal CharacterMotor motor;
        [SerializeField] internal KnockdownController knockdown;
        [SerializeField] internal RagdollController ragdoll;

        [Header("Footsteps")]
        [SerializeField] internal bool distanceFootsteps = true;
        [SerializeField, Min(0.1f)] internal float walkStepDistance = 1.1f;
        [SerializeField, Min(0.1f)] internal float runStepDistance = 1.75f;

        [Header("Volume scaling")]
        [Tooltip("Landing speed (m/s) that plays the landing sound at full volume.")]
        [SerializeField, Min(0.1f)] internal float fullVolumeLandingSpeed = 12f;

        [Tooltip("Bone impact speed (m/s) that plays the body-collision sound at full volume.")]
        [SerializeField, Min(0.1f)] internal float fullVolumeCollisionSpeed = 9f;

        private float distanceSinceStep;
        private bool subscribed;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (knockdown == null) knockdown = GetComponent<KnockdownController>();
            if (ragdoll == null) ragdoll = GetComponent<RagdollController>();
        }

        private void Start()
        {
            if (motor != null)
            {
                motor.Jumped += OnJumped;
                motor.Landed += OnLanded;
            }

            if (knockdown != null)
            {
                knockdown.KnockedDown += OnKnockedDown;
                knockdown.Shoved += OnShoved;
            }

            if (ragdoll != null)
            {
                ragdoll.BoneCollided += OnBoneCollided;
            }

            subscribed = true;
        }

        private void OnDestroy()
        {
            if (!subscribed)
            {
                return;
            }

            if (motor != null)
            {
                motor.Jumped -= OnJumped;
                motor.Landed -= OnLanded;
            }

            if (knockdown != null)
            {
                knockdown.KnockedDown -= OnKnockedDown;
                knockdown.Shoved -= OnShoved;
            }

            if (ragdoll != null)
            {
                ragdoll.BoneCollided -= OnBoneCollided;
            }
        }

        private void Update()
        {
            if (!distanceFootsteps || motor == null || motor.IsSuspended || !motor.IsGrounded)
            {
                distanceSinceStep = 0f;
                return;
            }

            float speed = motor.PlanarSpeed;
            if (speed < 0.5f)
            {
                distanceSinceStep = 0f;
                return;
            }

            distanceSinceStep += speed * Time.deltaTime;
            PlayerMovementConfig config = motor.Config;
            float runThreshold = config != null ? (config.walkSpeed + config.runSpeed) * 0.5f : 4.5f;
            float stepDistance = speed < runThreshold ? walkStepDistance : runStepDistance;
            if (distanceSinceStep >= stepDistance)
            {
                distanceSinceStep = 0f;
                PlayFootstep();
            }
        }

        /// <summary>Public so animation events on imported clips can call it.</summary>
        public void PlayFootstep()
        {
            AudioManager.Play(AudioEventId.Footstep, transform.position);
        }

        private void OnJumped()
        {
            AudioManager.Play(AudioEventId.Jump, transform.position);
        }

        private void OnLanded(float impactSpeed)
        {
            float volume = Mathf.Clamp01(0.35f + impactSpeed / fullVolumeLandingSpeed);
            AudioManager.Play(AudioEventId.Land, transform.position, volume);
        }

        private void OnKnockedDown(ImpactData impact)
        {
            AudioManager.Play(AudioEventId.ObstacleImpact, impact.Point);
        }

        private void OnShoved(ImpactData impact)
        {
            AudioManager.Play(AudioEventId.ObstacleImpact, impact.Point, 0.45f);
        }

        private void OnBoneCollided(float speed, Vector3 point)
        {
            float volume = Mathf.Clamp01(0.25f + speed / fullVolumeCollisionSpeed);
            AudioManager.Play(AudioEventId.RagdollCollision, point, volume);
        }
    }
}
