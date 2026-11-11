using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// TEMPORARY. Moves the primitive mannequin's limbs in code so the logical animation states are
    /// readable before real clips exist. It is not animation coverage: every state in the Animator
    /// controller is still an empty placeholder.
    ///
    /// To replace: import the humanoid character + clips, assign the clips to the Animator states,
    /// and delete this component from the player prefab. Nothing else depends on it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaceholderProceduralAnimator : MonoBehaviour
    {
        private const int ChannelCount = 11;
        private const int LeftThigh = 0, RightThigh = 1, LeftKnee = 2, RightKnee = 3;
        private const int LeftShoulder = 4, RightShoulder = 5, LeftElbow = 6, RightElbow = 7;
        private const int ArmSpread = 8, ChestLean = 9, HipsDrop = 10;

        [SerializeField] internal AnimationStateController stateSource;
        [SerializeField] internal CharacterMotor motor;

        [Header("Bones")]
        [SerializeField] internal Transform hips;
        [SerializeField] internal Transform chest;
        [SerializeField] internal Transform leftUpperArm;
        [SerializeField] internal Transform leftLowerArm;
        [SerializeField] internal Transform rightUpperArm;
        [SerializeField] internal Transform rightLowerArm;
        [SerializeField] internal Transform leftUpperLeg;
        [SerializeField] internal Transform leftLowerLeg;
        [SerializeField] internal Transform rightUpperLeg;
        [SerializeField] internal Transform rightLowerLeg;

        [Header("Tuning")]
        [Tooltip("How quickly the pose follows state changes (1/s).")]
        [SerializeField, Min(1f)] internal float poseSharpness = 14f;

        [Tooltip("Seconds the get-up motion takes. Should roughly match the knockdown recovery duration.")]
        [SerializeField, Min(0.1f)] internal float recoverDuration = 0.9f;

        private readonly float[] target = new float[ChannelCount];
        private readonly float[] current = new float[ChannelCount];
        private Transform[] bones;
        private Quaternion[] restRotations;
        private Vector3 hipsRestPosition;
        private float phase;
        private bool hasRest;

        private void Awake()
        {
            CaptureRest();
        }

        private void LateUpdate()
        {
            if (!hasRest || stateSource == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            CharacterAnimState state = stateSource.CurrentState;
            if (state == CharacterAnimState.Knockdown)
            {
                return;
            }

            BuildTargetPose(state, dt);

            float blend = 1f - Mathf.Exp(-poseSharpness * dt);
            for (int i = 0; i < ChannelCount; i++)
            {
                current[i] = Mathf.Lerp(current[i], target[i], blend);
            }

            ApplyPose();
        }

        private void BuildTargetPose(CharacterAnimState state, float dt)
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                target[i] = 0f;
            }

            // Angle convention (degrees about the bone's local X axis): negative swings a limb forward,
            // positive swings it back. Knees bend with positive values, elbows with negative values.
            switch (state)
            {
                case CharacterAnimState.Walk:
                case CharacterAnimState.Run:
                    BuildLocomotionPose(dt);
                    break;

                case CharacterAnimState.Jump:
                    target[LeftThigh] = -38f;
                    target[RightThigh] = -8f;
                    target[LeftKnee] = 58f;
                    target[RightKnee] = 22f;
                    target[LeftShoulder] = -125f;
                    target[RightShoulder] = -125f;
                    target[LeftElbow] = -20f;
                    target[RightElbow] = -20f;
                    target[ArmSpread] = 14f;
                    target[ChestLean] = 4f;
                    break;

                case CharacterAnimState.Fall:
                    target[LeftThigh] = -18f;
                    target[RightThigh] = 10f;
                    target[LeftKnee] = 26f;
                    target[RightKnee] = 30f;
                    target[LeftShoulder] = -55f;
                    target[RightShoulder] = -55f;
                    target[LeftElbow] = -30f;
                    target[RightElbow] = -30f;
                    target[ArmSpread] = 48f;
                    break;

                case CharacterAnimState.Land:
                    SetCrouch(0.55f);
                    break;

                case CharacterAnimState.Recover:
                    float progress = Mathf.Clamp01(stateSource.TimeInState / recoverDuration);
                    float eased = progress * progress * (3f - 2f * progress);
                    SetCrouch(1f - eased);
                    break;

                default:
                    // Idle: a slow breathing sway.
                    float breathe = Mathf.Sin(Time.time * 1.7f);
                    target[ChestLean] = 1.5f * breathe;
                    target[ArmSpread] = 6f + breathe;
                    target[LeftElbow] = -8f;
                    target[RightElbow] = -8f;
                    break;
            }
        }

        private void BuildLocomotionPose(float dt)
        {
            float speed = motor != null ? motor.PlanarSpeed : 0f;
            float walkSpeed = 3.2f;
            float runSpeed = 6.5f;
            if (motor != null && motor.Config != null)
            {
                walkSpeed = motor.Config.walkSpeed;
                runSpeed = Mathf.Max(walkSpeed + 0.01f, motor.Config.runSpeed);
            }

            float runAmount = Mathf.Clamp01((speed - walkSpeed) / (runSpeed - walkSpeed));
            float cycleLength = Mathf.Lerp(2.2f, 3.6f, runAmount);
            phase = Mathf.Repeat(phase + speed / cycleLength * dt * Mathf.PI * 2f, Mathf.PI * 2f);

            float amplitude = Mathf.Lerp(24f, 44f, runAmount) * Mathf.Clamp01(speed / walkSpeed);
            float swing = Mathf.Sin(phase);

            target[LeftThigh] = -swing * amplitude;
            target[RightThigh] = swing * amplitude;

            float baseKnee = Mathf.Lerp(8f, 22f, runAmount);
            float liftKnee = Mathf.Lerp(26f, 58f, runAmount);
            target[LeftKnee] = baseKnee + Mathf.Max(0f, Mathf.Cos(phase)) * liftKnee;
            target[RightKnee] = baseKnee + Mathf.Max(0f, -Mathf.Cos(phase)) * liftKnee;

            // Arms counter-swing against the legs.
            target[LeftShoulder] = swing * amplitude * 0.8f;
            target[RightShoulder] = -swing * amplitude * 0.8f;
            float elbow = -Mathf.Lerp(18f, 78f, runAmount);
            target[LeftElbow] = elbow;
            target[RightElbow] = elbow;
            target[ArmSpread] = 5f;
            target[ChestLean] = Mathf.Lerp(3f, 12f, runAmount);
        }

        /// <summary>Crouch pose. The hips drop matches the leg angles so the feet stay on the ground.</summary>
        private void SetCrouch(float depth)
        {
            const float thighLength = 0.42f;
            const float shinLength = 0.48f;

            float thigh = 80f * depth;
            float knee = 120f * depth;
            float shinFromVertical = knee - thigh;

            target[LeftThigh] = -thigh;
            target[RightThigh] = -thigh;
            target[LeftKnee] = knee;
            target[RightKnee] = knee;
            target[HipsDrop] = thighLength * (1f - Mathf.Cos(thigh * Mathf.Deg2Rad))
                + shinLength * (1f - Mathf.Cos(shinFromVertical * Mathf.Deg2Rad));
            target[ChestLean] = 40f * depth;
            target[LeftShoulder] = 25f * depth;
            target[RightShoulder] = 25f * depth;
            target[LeftElbow] = -45f * depth;
            target[RightElbow] = -45f * depth;
            target[ArmSpread] = 10f * depth;
        }

        private void ApplyPose()
        {
            Rotate(leftUpperLeg, 6, current[LeftThigh], 0f);
            Rotate(rightUpperLeg, 8, current[RightThigh], 0f);
            Rotate(leftLowerLeg, 7, current[LeftKnee], 0f);
            Rotate(rightLowerLeg, 9, current[RightKnee], 0f);

            // A positive roll swings a hanging limb towards +X, so the left arm spreads with a negative roll.
            Rotate(leftUpperArm, 2, current[LeftShoulder], -current[ArmSpread]);
            Rotate(rightUpperArm, 4, current[RightShoulder], current[ArmSpread]);
            Rotate(leftLowerArm, 3, current[LeftElbow], 0f);
            Rotate(rightLowerArm, 5, current[RightElbow], 0f);
            Rotate(chest, 1, current[ChestLean], 0f);

            if (hips != null)
            {
                hips.localPosition = hipsRestPosition + Vector3.down * current[HipsDrop];
            }
        }

        private void Rotate(Transform bone, int index, float pitch, float roll)
        {
            if (bone != null)
            {
                bone.localRotation = restRotations[index] * Quaternion.Euler(pitch, 0f, roll);
            }
        }

        private void CaptureRest()
        {
            bones = new[]
            {
                hips, chest, leftUpperArm, leftLowerArm, rightUpperArm, rightLowerArm,
                leftUpperLeg, leftLowerLeg, rightUpperLeg, rightLowerLeg
            };

            restRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                restRotations[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            }

            hipsRestPosition = hips != null ? hips.localPosition : Vector3.zero;
            hasRest = true;
        }
    }
}
