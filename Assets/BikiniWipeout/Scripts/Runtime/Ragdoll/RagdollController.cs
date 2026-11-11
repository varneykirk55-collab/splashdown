using System;
using System.Collections.Generic;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Rigidbody ragdoll: switches a bone hierarchy between "kinematic, animation-driven" and
    /// "simulated", launches it, and blends back to the animated pose afterwards.
    ///
    /// It is deliberately independent of player movement and of obstacles. It discovers bones by
    /// looking for Rigidbodies under <see cref="ragdollRoot"/>, so any rig set up with Unity's
    /// ragdoll wizard (or the placeholder mannequin) works without code changes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class RagdollController : MonoBehaviour
    {
        [SerializeField] internal RagdollConfig config;

        [Tooltip("Root of the bone hierarchy (the visual model). All Rigidbodies below it become ragdoll bones.")]
        [SerializeField] internal Transform ragdollRoot;

        [Tooltip("The pelvis / hips body. Used for settle detection and as the camera focus while ragdolled.")]
        [SerializeField] internal Rigidbody rootBody;

        [Tooltip("Optional. Disabled while the ragdoll is simulated.")]
        [SerializeField] internal Animator animator;

        [Tooltip("Other pose-writing components to switch off while simulated (e.g. the placeholder procedural animator).")]
        [SerializeField] internal Behaviour[] disableWhileSimulated;

        private Rigidbody[] bodies;
        private Collider[] colliders;
        private Transform[] poseTransforms;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Vector3[] blendFromPositions;
        private Quaternion[] blendFromRotations;
        private Vector3[] capturedWorldPositions;
        private Quaternion[] capturedWorldRotations;
        private float blendElapsed;
        private float lastCollisionEventTime = float.NegativeInfinity;
        private bool initialized;

        /// <summary>True while physics owns the bones.</summary>
        public bool IsActive { get; private set; }

        /// <summary>True while blending from the ragdoll pose back to the animated pose.</summary>
        public bool IsBlending { get; private set; }

        public Rigidbody RootBody => rootBody;
        public IReadOnlyList<Rigidbody> Bodies => bodies;
        public RagdollConfig Config => config;

        public Vector3 RootBonePosition => rootBody != null ? rootBody.transform.position : transform.position;
        public bool IsSettled(float linearSpeed, float angularSpeed)
        {
            if (!IsActive || bodies.Length == 0) return false;
            foreach (Rigidbody body in bodies)
                if (body.linearVelocity.sqrMagnitude > linearSpeed * linearSpeed
                    || body.angularVelocity.sqrMagnitude > angularSpeed * angularSpeed) return false;
            return true;
        }

        public Vector3 RootBoneVelocity => IsActive && rootBody != null ? rootBody.linearVelocity : Vector3.zero;

        /// <summary>Raised when a bone hits something hard enough: (impact speed, world point).</summary>
        public event Action<float, Vector3> BoneCollided;

        public event Action Activated;
        public event Action Deactivated;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Collects bones and stores the rest pose. Safe to call more than once.</summary>
        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RagdollConfig>();
                Debug.LogWarning("[RagdollController] No RagdollConfig assigned; using default values.", this);
            }

            if (ragdollRoot == null)
            {
                ragdollRoot = transform;
            }

            var bodyList = new List<Rigidbody>();
            ragdollRoot.GetComponentsInChildren(true, bodyList);
            // A Rigidbody on the character root itself is not a bone.
            bodyList.RemoveAll(b => b.transform == transform);
            bodies = bodyList.ToArray();

            var colliderList = new List<Collider>();
            for (int i = 0; i < bodies.Length; i++)
            {
                Collider[] own = bodies[i].GetComponents<Collider>();
                colliderList.AddRange(own);

                if (bodies[i].GetComponent<RagdollBoneCollisionReporter>() == null)
                {
                    bodies[i].gameObject.AddComponent<RagdollBoneCollisionReporter>();
                }

                bodies[i].GetComponent<RagdollBoneCollisionReporter>().owner = this;
            }

            colliders = colliderList.ToArray();

            if (rootBody == null && bodies.Length > 0)
            {
                rootBody = bodies[0];
            }

            // GetComponentsInChildren is depth-first, so parents always precede their children.
            poseTransforms = ragdollRoot.GetComponentsInChildren<Transform>(true);
            int count = poseTransforms.Length;
            restPositions = new Vector3[count];
            restRotations = new Quaternion[count];
            blendFromPositions = new Vector3[count];
            blendFromRotations = new Quaternion[count];
            capturedWorldPositions = new Vector3[count];
            capturedWorldRotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                restPositions[i] = poseTransforms[i].localPosition;
                restRotations[i] = poseTransforms[i].localRotation;
            }

            ApplyBodySettings();
            SetSimulated(false);
            initialized = true;

            if (bodies.Length == 0)
            {
                Debug.LogWarning("[RagdollController] No Rigidbodies found under the ragdoll root; knockdowns will have no physics.", this);
            }
        }

        /// <summary>Hands the bones to physics. Every bone starts with <paramref name="inheritedVelocity"/>.</summary>
        public void Activate(Vector3 inheritedVelocity)
        {
            Initialize();
            if (IsActive)
            {
                return;
            }

            IsBlending = false;
            if (animator != null)
            {
                animator.enabled = false;
            }

            SetBehavioursEnabled(false);
            Physics.SyncTransforms();
            ApplyBodySettings();
            SetSimulated(true);

            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].linearVelocity = inheritedVelocity;
                bodies[i].angularVelocity = Vector3.zero;
                bodies[i].WakeUp();
            }

            IsActive = true;
            Activated?.Invoke();
        }

        /// <summary>
        /// Throws the active ragdoll. Every bone receives the same velocity change (which keeps the joints
        /// relaxed), the bone nearest the hit gets a little extra, and the body is tipped over.
        /// </summary>
        public void ApplyLaunch(Vector3 velocityChange, Vector3 worldPoint)
        {
            if (!IsActive || bodies.Length == 0)
            {
                return;
            }

            velocityChange = Vector3.ClampMagnitude(velocityChange, config.maxLinearVelocity);
            Rigidbody nearest = null;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].AddForce(velocityChange, ForceMode.VelocityChange);
                float sqr = (bodies[i].worldCenterOfMass - worldPoint).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = bodies[i];
                }
            }

            Vector3 flat = Vector3.ProjectOnPlane(velocityChange, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 direction = flat.normalized;
            if (nearest != null && config.hitBoneExtraSpeed > 0f)
            {
                nearest.AddForceAtPosition(direction * Mathf.Min(config.hitBoneExtraSpeed, velocityChange.magnitude * 0.25f),
                    nearest.ClosestPointOnBounds(worldPoint), ForceMode.VelocityChange);
            }

            if (rootBody != null && config.tumbleSpin > 0f)
            {
                // Rotating about (up x direction) tips the top of the body towards the push direction.
                rootBody.AddTorque(Vector3.Cross(Vector3.up, direction) * config.tumbleSpin, ForceMode.VelocityChange);
            }
        }

        /// <summary>
        /// Returns the bones to animation control.
        /// </summary>
        /// <param name="blendToAnimatedPose">Blend smoothly from the current pose (recovery) or snap to the rest pose (respawn).</param>
        /// <param name="relocateCharacter">
        /// Optional callback that moves the character root (e.g. teleports the motor to where the body
        /// came to rest). The visible pose is preserved across the move.
        /// </param>
        public void Deactivate(bool blendToAnimatedPose, Action relocateCharacter = null)
        {
            Initialize();
            bool wasActive = IsActive;

            if (blendToAnimatedPose && wasActive)
            {
                for (int i = 0; i < poseTransforms.Length; i++)
                {
                    capturedWorldPositions[i] = poseTransforms[i].position;
                    capturedWorldRotations[i] = poseTransforms[i].rotation;
                }
            }

            SetSimulated(false);
            IsActive = false;

            relocateCharacter?.Invoke();

            if (blendToAnimatedPose && wasActive && config.poseBlendTime > 0f)
            {
                // Index 0 is the ragdoll root itself: it follows the character, it is not a bone.
                for (int i = 1; i < poseTransforms.Length; i++)
                {
                    poseTransforms[i].SetPositionAndRotation(capturedWorldPositions[i], capturedWorldRotations[i]);
                }

                for (int i = 0; i < poseTransforms.Length; i++)
                {
                    blendFromPositions[i] = poseTransforms[i].localPosition;
                    blendFromRotations[i] = poseTransforms[i].localRotation;
                }

                blendElapsed = 0f;
                IsBlending = true;
            }
            else
            {
                IsBlending = false;
                ApplyRestPose();
            }

            if (animator != null)
            {
                animator.enabled = true;
            }

            SetBehavioursEnabled(true);

            if (wasActive)
            {
                Deactivated?.Invoke();
            }
        }

        /// <summary>Snaps every bone back to the pose it had at startup.</summary>
        public void ApplyRestPose()
        {
            if (poseTransforms == null)
            {
                return;
            }

            // Index 0 (the ragdoll root) keeps whatever local pose its owner gave it.
            for (int i = 1; i < poseTransforms.Length; i++)
            {
                poseTransforms[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
            }
        }

        private void Update()
        {
            if (!IsBlending)
            {
                return;
            }

            // Start each frame from the rest pose. The Animator (which runs after Update) and any
            // procedural animation then overwrite the bones they drive, and LateUpdate blends from
            // the ragdoll pose towards whatever the result is.
            ApplyRestPose();
        }

        private void LateUpdate()
        {
            if (!IsBlending)
            {
                return;
            }

            blendElapsed += Time.deltaTime;
            float t = config.poseBlendTime <= 0f ? 1f : Mathf.Clamp01(blendElapsed / config.poseBlendTime);
            float eased = t * t * (3f - 2f * t);

            for (int i = 1; i < poseTransforms.Length; i++)
            {
                Transform bone = poseTransforms[i];
                bone.SetLocalPositionAndRotation(
                    Vector3.Lerp(blendFromPositions[i], bone.localPosition, eased),
                    Quaternion.Slerp(blendFromRotations[i], bone.localRotation, eased));
            }

            if (t >= 1f)
            {
                IsBlending = false;
            }
        }

        internal void ReportBoneCollision(Collision collision)
        {
            if (!IsActive || BoneCollided == null)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < config.collisionEventMinSpeed || Time.time - lastCollisionEventTime < config.collisionEventCooldown)
            {
                return;
            }

            lastCollisionEventTime = Time.time;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            BoneCollided.Invoke(speed, point);
        }

        private void ApplyBodySettings()
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                body.linearDamping = config.linearDamping;
                body.angularDamping = config.angularDamping;
                body.maxLinearVelocity = config.maxLinearVelocity;
                body.maxAngularVelocity = config.maxAngularVelocity;
                body.maxDepenetrationVelocity = config.maxDepenetrationVelocity;
                body.solverIterations = config.solverIterations;
                body.solverVelocityIterations = config.solverVelocityIterations;
            }
        }

        private void SetSimulated(bool simulated)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (simulated)
                {
                    body.isKinematic = false;
                    body.collisionDetectionMode = config.collisionDetection;
                    body.interpolation = config.interpolate ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                    body.detectCollisions = true;
                }
                else
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    // Continuous modes are not valid on kinematic bodies; switch back before going kinematic.
                    body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    body.interpolation = RigidbodyInterpolation.None;
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = simulated;
            }
        }

        private void SetBehavioursEnabled(bool value)
        {
            if (disableWhileSimulated == null)
            {
                return;
            }

            for (int i = 0; i < disableWhileSimulated.Length; i++)
            {
                if (disableWhileSimulated[i] != null)
                {
                    disableWhileSimulated[i].enabled = value;
                }
            }
        }
    }
}
