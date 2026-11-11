using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Audio hook for moving mechanisms (sweeper arms, pendulums, beams): plays a whoosh each time
    /// the tracked point sweeps past the listener. Costs one distance check per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProximityWhoosh : MonoBehaviour
    {
        [Tooltip("The moving point to track, e.g. the tip of an arm. Defaults to this transform.")]
        [SerializeField] internal Transform tip;

        [SerializeField] internal AudioEventId audioEvent = AudioEventId.MechanismWhoosh;
        [SerializeField, Min(0.5f)] internal float radius = 5f;
        [SerializeField, Min(0f)] internal float cooldown = 0.5f;

        private Transform listener;
        private bool inside;
        private float nextTime;

        private void Awake()
        {
            if (tip == null) tip = transform;
        }

        private void Update()
        {
            if (listener == null)
            {
                Camera main = Camera.main;
                if (main == null) return;
                listener = main.transform;
            }

            bool near = (tip.position - listener.position).sqrMagnitude <= radius * radius;
            if (near && !inside && Time.time >= nextTime)
            {
                nextTime = Time.time + cooldown;
                AudioManager.Play(audioEvent, tip.position);
            }

            inside = near;
        }
    }
}
