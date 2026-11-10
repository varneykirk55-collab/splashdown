using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Physics tuning for <see cref="RagdollController"/>. Conservative defaults chosen for stability.</summary>
    [CreateAssetMenu(menuName = "Bikini Wipeout/Ragdoll Config", fileName = "RagdollConfig")]
    public sealed class RagdollConfig : ScriptableObject
    {
        [Header("Rigidbody settings (applied to every bone)")]
        [Min(0f)] public float linearDamping = 0.08f;
        [Min(0f)] public float angularDamping = 0.9f;
        [Min(0.1f)] public float maxLinearVelocity = 18f;
        [Min(0.1f)] public float maxAngularVelocity = 14f;

        [Tooltip("Limits how violently overlapping bodies are pushed apart. Low values prevent explosions.")]
        [Min(0.1f)] public float maxDepenetrationVelocity = 5f;

        [Range(4, 40)] public int solverIterations = 14;
        [Range(1, 20)] public int solverVelocityIterations = 4;
        public CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;
        public bool interpolate = true;

        [Header("Launch")]
        [Tooltip("Extra speed (m/s) given to the bone nearest the hit, on top of the whole-body launch.")]
        [Min(0f)] public float hitBoneExtraSpeed = 2.0f;

        [Tooltip("Spin (rad/s) that tips the body over in the direction of the hit.")]
        [Min(0f)] public float tumbleSpin = 3.0f;

        [Header("Recovery")]
        [Tooltip("Seconds to blend from the ragdoll pose back to the animated pose.")]
        [Min(0f)] public float poseBlendTime = 0.35f;

        [Header("Collision feedback")]
        [Tooltip("Bone impacts slower than this (m/s) make no sound.")]
        [Min(0f)] public float collisionEventMinSpeed = 2.5f;
        [Min(0f)] public float collisionEventCooldown = 0.12f;
    }
}
