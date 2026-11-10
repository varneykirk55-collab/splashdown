using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Tuning for <see cref="ThirdPersonCamera"/>, including behaviour during wipeouts.</summary>
    [CreateAssetMenu(menuName = "Bikini Wipeout/Camera Config", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("Starting zoom distance. Clamped to the zoom range.")]
        [Min(0.5f)] public float distance = 5.5f;

        [Tooltip("Closest the collision handling may pull the camera when something blocks the view.")]
        [Min(0.1f)] public float minDistance = 1.2f;

        [Tooltip("Vertical offset of the orbit pivot above the character's focus point.")]
        public float pivotHeightOffset = 0.45f;

        [Range(20f, 100f)] public float fieldOfView = 60f;

        [Header("Zoom")]
        [Tooltip("Closest the player can zoom in (metres from the pivot).")]
        [Min(0.5f)] public float zoomMinDistance = 2.0f;

        [Tooltip("Furthest the player can zoom out.")]
        [Min(0.5f)] public float zoomMaxDistance = 12f;

        [Tooltip("Metres moved per mouse-wheel notch.")]
        [Min(0f)] public float scrollZoomStep = 0.9f;

        [Tooltip("Metres per second while a zoom button / d-pad direction is held.")]
        [Min(0f)] public float heldZoomSpeed = 6f;

        [Tooltip("Smoothing time of the zoom (seconds). 0 = instant.")]
        [Min(0f)] public float zoomSmoothTime = 0.12f;

        [Header("Look")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [Min(0f)] public float mouseSensitivity = 0.12f;

        [Tooltip("Degrees per second at full stick deflection.")]
        [Min(0f)] public float gamepadLookSpeed = 170f;

        [Tooltip("Stick response curve. 1 = linear; higher values give finer control near the centre of the stick.")]
        [Range(1f, 3f)] public float gamepadLookExponent = 1.6f;

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

        private void OnValidate()
        {
            zoomMaxDistance = Mathf.Max(zoomMaxDistance, zoomMinDistance);
        }
    }
}
