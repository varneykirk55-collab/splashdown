using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Audio hook for physical bodies: plays an event when this body hits something hard enough (e.g. a ball bouncing).</summary>
    [DisallowMultipleComponent]
    public sealed class CollisionSound : MonoBehaviour
    {
        [SerializeField] internal AudioEventId audioEvent = AudioEventId.BallBounce;

        [Tooltip("Impacts slower than this (m/s) are silent.")]
        [SerializeField, Min(0f)] internal float minSpeed = 2f;

        [Tooltip("Impact speed (m/s) that plays at full volume.")]
        [SerializeField, Min(0.1f)] internal float fullVolumeSpeed = 8f;

        [SerializeField, Min(0f)] internal float cooldown = 0.15f;

        private float nextTime;

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed || Time.time < nextTime)
            {
                return;
            }

            nextTime = Time.time + cooldown;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            AudioManager.Play(audioEvent, point, Mathf.Clamp01(0.3f + speed / fullVolumeSpeed));
        }
    }
}
