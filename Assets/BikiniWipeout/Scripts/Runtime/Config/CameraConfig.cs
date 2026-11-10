using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Tuning for <see cref="ThirdPersonCamera"/>, including behaviour during wipeouts.</summary>
    [CreateAssetMenu(menuName = "Bikini Wipeout/Camera Config", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Framing")]
        [Min(0.5f)] public float distance = 5.5f;
        [Min(0.1f)] public float minDistance = 1.2f;

        [Tooltip("Vertical offset of the orbit pivot above the character's focus point.")]
        public float pivotHeightOffset = 0.45f;

        [Range(20f, 100f)] public float fieldOfView = 60f;

        [Header("Look")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [Min(0f)] public float mouseSensitivity = 0.12f;

        [Tooltip("Degrees per second at full stick deflection.")]
        [Min(0f)] public float gamepadLookSpeed = 170f;

        public bool invertY;
        [Range(-89f, 0f)] public float minPitch = -25f;
        [Range(0f, 89f)] public float maxPitch = 68f;
        public float startPitch = 16f;

        [Header("Follow")]
        [Min(0f)] public float followSmoothTime = 0.07f;

        [Header("Collision")]
        [Min(0.01f)] public float collisionRadius = 0.25f;

        [Tooltip("How fast the camera returns to full distance after an obstruction clears (m/s).")]
        [Min(0.1f)] public float distanceRecoverSpeed = 6f;

        [Header("Wipeout behaviour")]
        [Tooltip("Camera pulls back by this factor while the character is ragdolled.")]
        [Min(1f)] public float wipeoutDistanceMultiplier = 1.35f;

        [Tooltip("Added field of view while the character is ragdolled.")]
        [Range(0f, 30f)] public float wipeoutFovBoost = 6f;

        [Tooltip("Follow smoothing while ragdolled. Higher = lazier camera.")]
        [Min(0f)] public float wipeoutFollowSmoothTime = 0.18f;

        [Tooltip("How fast the wipeout framing blends in and out (1/s).")]
        [Min(0.1f)] public float wipeoutBlendSpeed = 3.5f;
    }
}
