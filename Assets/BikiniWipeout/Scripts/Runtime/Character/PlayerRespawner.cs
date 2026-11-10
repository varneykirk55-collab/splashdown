using System;
using System.Collections;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Implemented by anything a hazard volume (water) can claim.</summary>
    public interface IHazardVictim
    {
        /// <summary>Returns true if the hazard took effect (false while already respawning or in cooldown).</summary>
        bool OnHazardEntered(Vector3 worldPoint);
    }

    /// <summary>
    /// Returns the character to the active checkpoint after falling into a hazard: hides it, waits a
    /// moment, resets ragdoll + velocity + physics state, and guards against respawn loops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRespawner : MonoBehaviour, IHazardVictim
    {
        [SerializeField] internal PlayerCharacter character;
        [SerializeField] internal CheckpointManager checkpoints;

        [Tooltip("Seconds between hitting the water and reappearing at the checkpoint.")]
        [SerializeField, Min(0f)] internal float respawnDelay = 0.9f;

        [Tooltip("Hazards are ignored for this long after a respawn.")]
        [SerializeField, Min(0f)] internal float hazardCooldown = 1.0f;

        [Tooltip("Dying this soon after a respawn counts towards loop detection.")]
        [SerializeField, Min(0f)] internal float loopWindow = 2.5f;

        [Tooltip("Consecutive quick deaths before falling back to the previous checkpoint.")]
        [SerializeField, Min(1)] internal int loopThreshold = 3;

        [Tooltip("World height below which the character is respawned even if no hazard volume was touched.")]
        [SerializeField] internal float killHeight = -15f;

        private RespawnGuard guard;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        public Vector3 RespawnFocus { get; private set; }

        public bool IsRespawning => guard != null && guard.IsRespawnPending;

        /// <summary>Raised when the character falls into a hazard (counts as a fall). Argument: world point.</summary>
        public event Action<Vector3> FellInHazard;

        /// <summary>Raised when the character has been placed back on the course.</summary>
        public event Action Respawned;

        private void Awake()
        {
            if (character == null)
            {
                character = GetComponent<PlayerCharacter>();
            }

            guard = new RespawnGuard(hazardCooldown, loopWindow, loopThreshold);
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void Start()
        {
            // Subscribed in Start so the character facade has finished resolving its components.
            if (character != null && character.Knockdown != null)
            {
                character.Knockdown.RescueRequested += OnRescueRequested;
            }
        }

        private void OnDestroy()
        {
            if (character != null && character.Knockdown != null)
            {
                character.Knockdown.RescueRequested -= OnRescueRequested;
            }
        }

        private void Update()
        {
            if (character != null && character.FocusPosition.y < killHeight)
            {
                BeginRespawn(character.FocusPosition, true);
            }
        }

        public bool OnHazardEntered(Vector3 worldPoint)
        {
            return BeginRespawn(worldPoint, true);
        }

        /// <summary>Sends the character back to the active checkpoint without counting a fall.</summary>
        public bool RespawnAtCheckpoint()
        {
            return BeginRespawn(transform.position, false);
        }

        private void OnRescueRequested()
        {
            RespawnAtCheckpoint();
        }

        private bool BeginRespawn(Vector3 worldPoint, bool countsAsFall)
        {
            if (!isActiveAndEnabled || character == null)
            {
                return false;
            }

            Vector3 lastFocus = character.FocusPosition;
            int activeIndex = checkpoints != null ? checkpoints.CurrentIndex : 0;
            int respawnIndex = guard.BeginRespawn(Time.timeAsDouble, activeIndex);
            if (respawnIndex < 0)
            {
                return false;
            }

            RespawnFocus = lastFocus;
            // Freeze and hide the character where it went under.
            if (character.Knockdown != null)
            {
                character.Knockdown.ResetForRespawn();
            }

            if (character.Motor != null)
            {
                character.Motor.SetControlLock(ControlLock.Respawn, true);
                character.Motor.SetSuspended(true);
            }

            character.SetVisible(false);

            if (countsAsFall)
            {
                FellInHazard?.Invoke(worldPoint);
            }

            StartCoroutine(RespawnAfterDelay(respawnIndex));
            return true;
        }

        private IEnumerator RespawnAfterDelay(int checkpointIndex)
        {
            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }

            CompleteRespawn(checkpointIndex);
        }

        private void CompleteRespawn(int checkpointIndex)
        {
            GetSpawnPose(checkpointIndex, out Vector3 position, out Quaternion rotation);

            if (character.Knockdown != null)
            {
                // Again, so the immunity window starts when the character actually reappears.
                character.Knockdown.ResetForRespawn();
            }

            if (character.Motor != null)
            {
                character.Motor.Teleport(position, rotation);
                character.Motor.SetSuspended(false);
                character.Motor.SetControlLock(ControlLock.Respawn, false);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            character.SetVisible(true);
            guard.CompleteRespawn(Time.timeAsDouble);
            Respawned?.Invoke();
        }

        private void GetSpawnPose(int checkpointIndex, out Vector3 position, out Quaternion rotation)
        {
            position = initialPosition;
            rotation = initialRotation;

            Checkpoint checkpoint = checkpoints != null ? checkpoints.GetCheckpoint(checkpointIndex) : null;
            if (checkpoint != null)
            {
                position = checkpoint.SpawnPosition;
                rotation = checkpoint.SpawnRotation;
            }

            // Snap onto the ground so the character never spawns floating or intersecting the platform.
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 8f,
                    GameLayers.SafeGroundMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point + Vector3.up * 0.02f;
            }
            else
            {
                Debug.LogWarning($"[PlayerRespawner] No ground found under spawn point of checkpoint {checkpointIndex}.", this);
            }
        }
    }
}
