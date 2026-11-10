using System;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Water trigger volume. Anything implementing <see cref="IHazardVictim"/> that enters it is
    /// claimed (the player respawns at a checkpoint); a splash effect and sound are played at the
    /// surface. No swimming in Foundation 1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterHazard : MonoBehaviour
    {
        [Tooltip("World-space height of the water surface, used to place the splash.")]
        [SerializeField] internal float surfaceHeight;

        [Tooltip("Optional. Instantiated at the entry point; should destroy itself (see SplashEffect).")]
        [SerializeField] internal GameObject splashPrefab;

        [Tooltip("Minimum seconds between two splashes from this volume.")]
        [SerializeField, Min(0f)] internal float splashCooldown = 0.2f;

        private float lastSplashTime = float.NegativeInfinity;

        /// <summary>Raised whenever something splashes into this water. Argument: surface point.</summary>
        public event Action<Vector3> Splashed;

        public float SurfaceHeight => surfaceHeight;

        private void OnTriggerEnter(Collider other)
        {
            IHazardVictim victim = other.GetComponentInParent<IHazardVictim>();
            if (victim == null)
            {
                return;
            }

            Vector3 center = other.bounds.center;
            var surfacePoint = new Vector3(center.x, surfaceHeight, center.z);
            if (victim.OnHazardEntered(surfacePoint))
            {
                Splash(surfacePoint);
            }
        }

        /// <summary>Plays the splash effect and sound at a point on the surface.</summary>
        public void Splash(Vector3 surfacePoint)
        {
            if (Time.time - lastSplashTime < splashCooldown)
            {
                return;
            }

            lastSplashTime = Time.time;
            if (splashPrefab != null)
            {
                Instantiate(splashPrefab, surfacePoint, Quaternion.identity);
            }

            AudioManager.Play(AudioEventId.WaterSplash, surfacePoint);
            Splashed?.Invoke(surfacePoint);
        }
    }
}
