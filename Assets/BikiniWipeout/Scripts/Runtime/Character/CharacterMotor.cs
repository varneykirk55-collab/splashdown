using System;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Independent reasons for taking steering away from the player.</summary>
    [Flags]
    public enum ControlLock
    {
        None = 0,
        Course = 1,
        Knockdown = 2,
        Respawn = 4,
        Script = 8
    }

    /// <summary>
    /// Third-person movement on a CharacterController: camera-relative walk/run, jump with coyote time
    /// and buffering, air control, gravity, moving-platform following and external pushes.
    /// It has no knowledge of ragdolls, obstacles or the visual model, so the placeholder character
    /// can be swapped for the final one without touching this class.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class CharacterMotor : MonoBehaviour
    {
        private const float MaxPlatformStep = 2f;

        [SerializeField] internal PlayerMovementConfig config;

        [Tooltip("Movement is relative to this transform's facing (normally the camera). Falls back to Camera.main.")]
        [SerializeField] internal Transform viewTransform;

        private CharacterController controller;
        private ICharacterInput input;
        private ControlLock locks;
        private bool suspended;

        private Vector3 planarVelocity;
        private Vector3 externalVelocity;
        private float verticalVelocity;
        private bool grounded;
        private GraceTimer coyoteTimer;
        private GraceTimer jumpBuffer;

        private Transform groundTransform;
        private Vector3 groundLocalPoint;
        private float groundYaw;
        private Vector3 platformVelocity;
        private Transform cachedMainCamera;
        private bool launched;

        /// <summary>Raised when a jump starts.</summary>
        public event Action Jumped;

        /// <summary>Raised on touchdown with the downward speed at impact (m/s).</summary>
        public event Action<float> Landed;

        /// <summary>Actual velocity over the last frame (m/s), excluding moving-platform carry.</summary>
        public Vector3 Velocity { get; private set; }

        public float PlanarSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;
        public float VerticalSpeed => grounded ? 0f : verticalVelocity;
        public bool IsGrounded => grounded;

        /// <summary>True when nothing is locking player steering.</summary>
        public bool ControlEnabled => locks == ControlLock.None;

        /// <summary>True while another system (the ragdoll) owns the body and the motor is switched off.</summary>
        public bool IsSuspended => suspended;

        /// <summary>The moving surface currently carrying the character, if any.</summary>
        public Transform GroundTransform => groundTransform;

        public PlayerMovementConfig Config => config;
        public CharacterController Controller => controller;
        public float CapsuleHeight => controller != null ? controller.height : 1.7f;
        public float CapsuleRadius => controller != null ? controller.radius : 0.3f;

        public ICharacterInput InputSource
        {
            get => input;
            set => input = value;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
                Debug.LogWarning("[CharacterMotor] No PlayerMovementConfig assigned; using default values.", this);
            }

            if (input == null)
            {
                input = GetComponent<ICharacterInput>();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (suspended || dt <= 0f || !controller.enabled)
            {
                return;
            }

            FollowGround(dt);

            coyoteTimer.Tick(dt);
            jumpBuffer.Tick(dt);

            Vector2 move = Vector2.zero;
            bool run = false;
            bool jumpHeld = false;
            if (ControlEnabled && input != null)
            {
                move = input.Move;
                run = input.RunHeld != config.runByDefault;
                jumpHeld = input.JumpHeld;
                if (input.JumpPressedThisFrame)
                {
                    jumpBuffer.Start(config.jumpBufferTime);
                }
            }
            else
            {
                jumpBuffer.Clear();
            }

            // --- Horizontal: smooth acceleration towards the camera-relative target velocity.
            Vector3 wishDirection = CameraRelative(move);
            bool hasInput = wishDirection.sqrMagnitude > 0.0001f;
            Vector3 targetVelocity = wishDirection * (run ? config.runSpeed : config.walkSpeed);

            float acceleration;
            if (grounded)
            {
                acceleration = hasInput ? config.groundAcceleration : config.groundDeceleration;
            }
            else
            {
                acceleration = hasInput ? config.airAcceleration : config.airDeceleration;
            }

            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, acceleration * dt);

            if (hasInput)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wishDirection.normalized, Vector3.up);
                float turnSpeed = grounded ? config.groundTurnSpeed : config.airTurnSpeed;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * dt);
            }

            // --- Jump.
            bool jumped = false;
            if (jumpBuffer.IsActive && (grounded || coyoteTimer.IsActive))
            {
                verticalVelocity = config.JumpSpeed + Mathf.Max(0f, platformVelocity.y);
                jumpBuffer.Clear();
                coyoteTimer.Clear();
                InheritPlatformMomentum();
                DetachFromGround();
                grounded = false;
                jumped = true;
                Jumped?.Invoke();
            }

            // --- Gravity.
            if (grounded && !jumped)
            {
                verticalVelocity = -config.groundStickSpeed;
            }
            else if (!jumped)
            {
                float gravity = config.gravity;
                if (verticalVelocity < 0f)
                {
                    gravity *= config.fallGravityMultiplier;
                }
                else if (!jumpHeld && !launched)
                {
                    gravity *= config.lowJumpGravityMultiplier;
                }

                verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -config.terminalFallSpeed);
            }

            if (launched && (grounded || verticalVelocity <= 0f))
            {
                launched = false;
            }

            float damping = grounded ? config.externalGroundDamping : config.externalAirDamping;
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, damping * dt);

            // --- Move.
            float fallSpeedBeforeMove = Mathf.Max(0f, -verticalVelocity);
            Vector3 before = transform.position;
            Vector3 velocity = planarVelocity + externalVelocity + Vector3.up * verticalVelocity;
            CollisionFlags flags = controller.Move(velocity * dt);
            Velocity = (transform.position - before) / dt;

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            // --- Ground state.
            bool wasGrounded = grounded;
            Transform ground = null;
            grounded = !jumped && ProbeGround(out ground);

            if (grounded)
            {
                if (!wasGrounded)
                {
                    verticalVelocity = -config.groundStickSpeed;
                    if (fallSpeedBeforeMove >= config.minLandingSpeed)
                    {
                        Landed?.Invoke(fallSpeedBeforeMove);
                    }
                }

                if (ground != null)
                {
                    AttachToGround(ground);
                }
                else
                {
                    DetachFromGround();
                }
            }
            else
            {
                if (wasGrounded && !jumped)
                {
                    coyoteTimer.Start(config.coyoteTime);
                    InheritPlatformMomentum();
                }

                DetachFromGround();
            }
        }

        /// <summary>Locks or unlocks player steering for one reason. Physics keeps running while locked.</summary>
        public void SetControlLock(ControlLock reason, bool locked)
        {
            if (locked)
            {
                locks |= reason;
            }
            else
            {
                locks &= ~reason;
            }
        }

        /// <summary>
        /// Switches the motor (and its CharacterController) off or on. Used while the ragdoll owns the body.
        /// </summary>
        public void SetSuspended(bool value)
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            suspended = value;
            controller.enabled = !value;
            if (value)
            {
                ResetMotion();
            }
        }

        /// <summary>Adds a horizontal push (m/s) that decays over time. Used for shoves.</summary>
        public void AddExternalVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            externalVelocity = Vector3.ClampMagnitude(externalVelocity + velocity, 20f);
        }

        /// <summary>
        /// Throws the character upwards at <paramref name="upwardSpeed"/> m/s (bounce pads, geysers).
        /// Horizontal motion and steering are untouched, and the rise is not cut short when the jump
        /// button is not held, so the height reached depends only on the launch speed.
        /// </summary>
        public void Launch(float upwardSpeed)
        {
            if (suspended || upwardSpeed <= 0f || controller == null || !controller.enabled)
            {
                return;
            }

            verticalVelocity = Mathf.Max(verticalVelocity, upwardSpeed);
            launched = true;
            InheritPlatformMomentum();
            DetachFromGround();
            grounded = false;
            coyoteTimer.Clear();
            jumpBuffer.Clear();
        }

        /// <summary>
        /// Re-anchors the character to the surface it stands on at its current position. Call after moving the
        /// character directly through <see cref="Controller"/> (e.g. a sliding surface); otherwise the next frame
        /// carries it back to the point stored before the move and the displacement is lost.
        /// </summary>
        public void RebaseGroundAnchor()
        {
            if (groundTransform == null)
            {
                return;
            }

            groundLocalPoint = groundTransform.InverseTransformPoint(transform.position);
            groundYaw = groundTransform.eulerAngles.y;
        }

        /// <summary>Moves the character instantly and clears all motion state.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            // A CharacterController overrides transform changes unless it is disabled first.
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            Vector3 flatForward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            Quaternion upright = flatForward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
                : Quaternion.identity;
            transform.SetPositionAndRotation(position, upright);
            Physics.SyncTransforms();
            controller.enabled = wasEnabled;
            ResetMotion();
        }

        /// <summary>Zeroes velocities, timers and platform tracking.</summary>
        public void ResetMotion()
        {
            planarVelocity = Vector3.zero;
            externalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            Velocity = Vector3.zero;
            grounded = false;
            launched = false;
            coyoteTimer.Clear();
            jumpBuffer.Clear();
            DetachFromGround();
        }

        private Vector3 CameraRelative(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Transform view = ResolveView();
            Vector3 forward = Vector3.forward;
            if (view != null)
            {
                forward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                {
                    // Looking straight up or down: use the camera's up vector instead.
                    forward = Vector3.ProjectOnPlane(view.up, Vector3.up);
                }
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
        }

        private Transform ResolveView()
        {
            if (viewTransform != null)
            {
                return viewTransform;
            }

            if (cachedMainCamera == null)
            {
                Camera main = Camera.main;
                cachedMainCamera = main != null ? main.transform : null;
            }

            return cachedMainCamera;
        }

        private bool ProbeGround(out Transform ground)
        {
            ground = null;
            if (verticalVelocity > 0.01f)
            {
                return false;
            }

            const float lift = 0.1f;
            float radius = Mathf.Max(0.01f, controller.radius - 0.01f);
            Vector3 bottomSphere = transform.position + controller.center
                + Vector3.down * Mathf.Max(0f, controller.height * 0.5f - controller.radius);
            Vector3 origin = bottomSphere + Vector3.up * lift;
            float distance = lift + controller.skinWidth + config.groundProbeDistance;

            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance,
                    GameLayers.StandableMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit + 1f)
            {
                ground = hit.rigidbody != null ? hit.rigidbody.transform : hit.collider.transform;
                return true;
            }

            return controller.isGrounded;
        }

        private void AttachToGround(Transform ground)
        {
            if (ground != groundTransform)
            {
                groundTransform = ground;
                platformVelocity = Vector3.zero;
            }

            groundLocalPoint = ground.InverseTransformPoint(transform.position);
            groundYaw = ground.eulerAngles.y;
        }

        private void DetachFromGround()
        {
            groundTransform = null;
            platformVelocity = Vector3.zero;
        }

        /// <summary>Carries the character along with whatever it stood on at the end of the last frame.</summary>
        private void FollowGround(float dt)
        {
            if (groundTransform == null)
            {
                platformVelocity = Vector3.zero;
                return;
            }

            Vector3 delta = groundTransform.TransformPoint(groundLocalPoint) - transform.position;
            if (delta.sqrMagnitude > MaxPlatformStep * MaxPlatformStep)
            {
                // The surface teleported (e.g. an obstacle reset): do not drag the character with it.
                DetachFromGround();
                return;
            }

            if (delta.sqrMagnitude > 1e-10f)
            {
                controller.Move(delta);
            }

            float yawDelta = Mathf.DeltaAngle(groundYaw, groundTransform.eulerAngles.y);
            if (Mathf.Abs(yawDelta) > 0.0001f)
            {
                transform.Rotate(0f, yawDelta, 0f, Space.World);
            }

            platformVelocity = delta / dt;
        }

        private void InheritPlatformMomentum()
        {
            externalVelocity += new Vector3(platformVelocity.x, 0f, platformVelocity.z);
        }
    }
}
