using System;
using System.Collections.Generic;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Logical animation states. They match the state names in the Animator controller.</summary>
    public enum CharacterAnimState
    {
        Idle,
        Walk,
        Run,
        Jump,
        Fall,
        Land,
        Knockdown,
        Recover
    }

    /// <summary>
    /// Translates gameplay state (motor + knockdown) into a logical animation state and Animator
    /// parameters. It only reads from gameplay code, so animation can be reworked without touching it.
    ///
    /// Animator parameters written (only those that exist on the controller):
    ///   Speed (float, m/s), MotionBlend (float, 0 = idle, 0.5 = walk speed, 1 = run speed),
    ///   VerticalSpeed (float), Grounded (bool), KnockedDown (bool), Recovering (bool),
    ///   Jump (trigger), Land (trigger)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimationStateController : MonoBehaviour
    {
        public static readonly int SpeedParam = Animator.StringToHash("Speed");
        public static readonly int MotionBlendParam = Animator.StringToHash("MotionBlend");
        public static readonly int VerticalSpeedParam = Animator.StringToHash("VerticalSpeed");
        public static readonly int GroundedParam = Animator.StringToHash("Grounded");
        public static readonly int KnockedDownParam = Animator.StringToHash("KnockedDown");
        public static readonly int RecoveringParam = Animator.StringToHash("Recovering");
        public static readonly int JumpParam = Animator.StringToHash("Jump");
        public static readonly int LandParam = Animator.StringToHash("Land");

        [SerializeField] internal Animator animator;
        [SerializeField] internal CharacterMotor motor;
        [SerializeField] internal KnockdownController knockdown;

        [Tooltip("Below this planar speed (m/s) the character counts as idle.")]
        [SerializeField, Min(0f)] internal float idleSpeedThreshold = 0.2f;

        [Tooltip("How long the Land state is held after touchdown.")]
        [SerializeField, Min(0f)] internal float landDuration = 0.18f;

        [Tooltip("Damping time for the Speed / MotionBlend parameters.")]
        [SerializeField, Min(0f)] internal float speedDampTime = 0.08f;

        private readonly HashSet<int> availableParameters = new HashSet<int>();
        private RuntimeAnimatorController cachedController;
        private float landTimer;
        private bool subscribed;

        public CharacterAnimState CurrentState { get; private set; } = CharacterAnimState.Idle;

        /// <summary>Seconds spent in <see cref="CurrentState"/>.</summary>
        public float TimeInState { get; private set; }

        /// <summary>(previous, next)</summary>
        public event Action<CharacterAnimState, CharacterAnimState> StateChanged;

        private void Awake()
        {
            if (motor == null) motor = GetComponentInParent<CharacterMotor>();
            if (knockdown == null) knockdown = GetComponentInParent<KnockdownController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (subscribed && motor != null)
            {
                motor.Jumped -= OnJumped;
                motor.Landed -= OnLanded;
            }

            subscribed = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (landTimer > 0f)
            {
                landTimer -= dt;
            }

            TimeInState += dt;
            CharacterAnimState next = Evaluate();
            if (next != CurrentState)
            {
                CharacterAnimState previous = CurrentState;
                CurrentState = next;
                TimeInState = 0f;
                StateChanged?.Invoke(previous, next);
            }

            WriteAnimatorParameters(dt);
        }

        private CharacterAnimState Evaluate()
        {
            KnockdownPhase phase = knockdown != null ? knockdown.Phase : KnockdownPhase.Normal;
            if (phase == KnockdownPhase.Ragdoll)
            {
                return CharacterAnimState.Knockdown;
            }

            if (phase == KnockdownPhase.Recovering)
            {
                return CharacterAnimState.Recover;
            }

            if (motor == null || motor.IsSuspended)
            {
                return CharacterAnimState.Idle;
            }

            if (!motor.IsGrounded)
            {
                return motor.VerticalSpeed > 0.5f ? CharacterAnimState.Jump : CharacterAnimState.Fall;
            }

            if (landTimer > 0f)
            {
                return CharacterAnimState.Land;
            }

            float speed = motor.PlanarSpeed;
            if (speed < idleSpeedThreshold)
            {
                return CharacterAnimState.Idle;
            }

            PlayerMovementConfig config = motor.Config;
            float runThreshold = config != null ? (config.walkSpeed + config.runSpeed) * 0.5f : 4.5f;
            return speed < runThreshold ? CharacterAnimState.Walk : CharacterAnimState.Run;
        }

        private void WriteAnimatorParameters(float dt)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return;
            }

            RefreshParameterCache();

            float speed = motor != null ? motor.PlanarSpeed : 0f;
            SetFloat(SpeedParam, speed, dt);
            SetFloat(MotionBlendParam, ComputeMotionBlend(speed), dt);

            if (availableParameters.Contains(VerticalSpeedParam))
            {
                animator.SetFloat(VerticalSpeedParam, motor != null ? motor.VerticalSpeed : 0f);
            }

            SetBool(GroundedParam, motor == null || motor.IsGrounded);
            SetBool(KnockedDownParam, CurrentState == CharacterAnimState.Knockdown);
            SetBool(RecoveringParam, CurrentState == CharacterAnimState.Recover);
        }

        private float ComputeMotionBlend(float speed)
        {
            PlayerMovementConfig config = motor != null ? motor.Config : null;
            if (config == null || config.walkSpeed <= 0f || config.runSpeed <= config.walkSpeed)
            {
                return 0f;
            }

            if (speed <= config.walkSpeed)
            {
                return 0.5f * speed / config.walkSpeed;
            }

            return 0.5f + 0.5f * Mathf.Clamp01((speed - config.walkSpeed) / (config.runSpeed - config.walkSpeed));
        }

        private void OnJumped()
        {
            landTimer = 0f;
            SetTrigger(JumpParam);
        }

        private void OnLanded(float impactSpeed)
        {
            landTimer = landDuration;
            SetTrigger(LandParam);
        }

        private void Subscribe()
        {
            if (subscribed || motor == null)
            {
                return;
            }

            motor.Jumped += OnJumped;
            motor.Landed += OnLanded;
            subscribed = true;
        }

        private void RefreshParameterCache()
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == cachedController)
            {
                return;
            }

            cachedController = controller;
            availableParameters.Clear();
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                availableParameters.Add(parameters[i].nameHash);
            }
        }

        private void SetFloat(int id, float value, float dt)
        {
            if (availableParameters.Contains(id))
            {
                animator.SetFloat(id, value, speedDampTime, dt);
            }
        }

        private void SetBool(int id, bool value)
        {
            if (availableParameters.Contains(id))
            {
                animator.SetBool(id, value);
            }
        }

        private void SetTrigger(int id)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return;
            }

            RefreshParameterCache();
            if (availableParameters.Contains(id))
            {
                animator.SetTrigger(id);
            }
        }
    }
}
