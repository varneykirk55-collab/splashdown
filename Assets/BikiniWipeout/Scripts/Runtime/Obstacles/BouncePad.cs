using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Obstacle 04: a spring pad that throws the character upwards the moment it lands on it.
    /// The launch speed is fixed, so the height is the same every time; steering stays with the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BouncePad : MonoBehaviour
    {
        [Tooltip("Upward speed given to the character (m/s). Height reached = speed^2 / (2 * gravity).")]
        [SerializeField, Min(0f)] internal float launchSpeed = 10.3f;

        [Tooltip("Radius of the pad top (m). The pad's origin is the centre of its top face.")]
        [SerializeField, Min(0.1f)] internal float radius = 1.4f;

        [Tooltip("Optional. The part that squashes and springs back when the pad fires.")]
        [SerializeField] internal Transform visual;

        [SerializeField, Min(0f)] internal float cooldown = 0.2f;
        [SerializeField] internal AudioEventId audioEvent = AudioEventId.SpringBounce;

        [Tooltip("Bounciness given to the pad's colliders, so a ragdoll that lands on it is thrown back up too. 0 = off.")]
        [SerializeField, Range(0f, 1f)] internal float ragdollBounciness = 0.85f;

        private PhysicsMaterial runtimeMaterial;
        private Vector3 visualScale = Vector3.one;
        private float squash;
        private float nextTime;

        /// <summary>How many times the pad has fired.</summary>
        public int LaunchCount { get; private set; }

        public float LaunchSpeed
        {
            get => launchSpeed;
            set => launchSpeed = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            if (visual != null)
            {
                visualScale = visual.localScale;
            }
        }

        private void Start()
        {
            if (ragdollBounciness <= 0f)
            {
                return;
            }

            // Physics materials do not affect the CharacterController, only bodies such as ragdoll bones.
            runtimeMaterial = new PhysicsMaterial("BouncePad (runtime)")
            {
                dynamicFriction = 0.3f,
                staticFriction = 0.3f,
                bounciness = ragdollBounciness,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            foreach (Collider solid in GetComponentsInChildren<Collider>())
            {
                if (!solid.isTrigger)
                {
                    solid.sharedMaterial = runtimeMaterial;
                }
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void Update()
        {
            if (squash > 0f && visual != null)
            {
                squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 2.8f);
                // Compress, then overshoot slightly on the way back.
                float wave = Mathf.Sin(squash * Mathf.PI * 2.5f) * squash;
                visual.localScale = new Vector3(visualScale.x * (1f + 0.12f * wave), visualScale.y * (1f - 0.45f * wave),
                    visualScale.z * (1f + 0.12f * wave));
            }

            if (Time.time < nextTime || !PlayerLocator.TryGetActiveMotor(out _, out CharacterMotor motor) || !motor.IsGrounded)
            {
                return;
            }

            if (!PlayerLocator.IsStandingOn(motor, transform) && !IsOnTop(motor.transform.position))
            {
                return;
            }

            motor.Launch(launchSpeed);
            nextTime = Time.time + cooldown;
            squash = 1f;
            LaunchCount++;
            AudioManager.Play(audioEvent, transform.position);
        }

        private bool IsOnTop(Vector3 feet)
        {
            Vector3 local = feet - transform.position;
            float reach = radius + 0.1f;
            return local.y > -0.15f && local.y < 0.25f && local.x * local.x + local.z * local.z <= reach * reach;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.7f, 0.9f);
            float apex = launchSpeed * launchSpeed / (2f * 24f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * apex);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * apex, 0.25f);
        }
    }
}
