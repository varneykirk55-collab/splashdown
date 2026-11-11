using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Decorative motion for buoys, floating props and flags: a gentle bob and sway. No physics.</summary>
    [DisallowMultipleComponent]
    public sealed class Bobbing : MonoBehaviour
    {
        [SerializeField] internal float height = 0.12f;
        [SerializeField] internal float swayDegrees = 4f;
        [SerializeField, Min(0.1f)] internal float period = 3f;
        [SerializeField] internal float phase;

        private Vector3 basePosition;
        private Quaternion baseRotation;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
        }

        private void Update()
        {
            float t = (Time.time / period + phase) * Mathf.PI * 2f;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t) * height);
            transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t * 0.7f) * swayDegrees, 0f, Mathf.Cos(t) * swayDegrees);
        }
    }
}
