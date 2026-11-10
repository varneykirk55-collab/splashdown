using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Obstacle B: a large physical ball that keeps bouncing while patrolling back and forth.
    /// It is a dynamic Rigidbody, so collisions are real physics; this component adds the perpetual
    /// bounce (to a configurable height) and the sideways patrol, and resets the ball if it escapes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class BouncingBall : MonoBehaviour, IResettable
    {
        [Header("Bounce")]
        [Tooltip("Height (m) the bottom of the ball reaches above the surface it bounces on.")]
        [SerializeField, Min(0f)] internal float bounceHeight = 1.8f;

        [Tooltip("Physics bounciness used for side impacts (walls, other balls). 0..1")]
        [SerializeField, Range(0f, 1f)] internal float bounciness = 0.6f;

        [Header("Patrol")]
        [Tooltip("The ball travels between start - offset and start + offset (world axes).")]
        [SerializeField] internal Vector3 patrolOffset = new Vector3(3f, 0f, 0f);

        [SerializeField, Min(0f)] internal float travelSpeed = 2.4f;
        [SerializeField, Min(0f)] internal float steering = 12f;

        [Tooltip("-1..1: where along the patrol line the ball starts. The sign also sets the initial direction.")]
        [SerializeField, Range(-1f, 1f)] internal float startPhase;

        [Header("Impact")]
        [Tooltip("How hard the ball hits. Forwarded to the ball's ImpactSource.")]
        [SerializeField, Min(0f)] internal float impactForce = 1.25f;

        [SerializeField] internal ImpactSource impactSource;

        [Header("Safety")]
        [Tooltip("The ball resets if it falls this far below its start height or strays this far from its line.")]
        [SerializeField, Min(1f)] internal float resetDistance = 9f;

        private Rigidbody body;
        private PhysicsMaterial runtimeMaterial;
        private Vector3 home;
        private Vector3 patrolAxis;
        private float patrolHalfLength;
        private int direction = 1;

        public float BounceHeight
        {
            get => bounceHeight;
            set => bounceHeight = Mathf.Max(0f, value);
        }

        public float ImpactForce
        {
            get => impactForce;
            set
            {
                impactForce = Mathf.Max(0f, value);
                ApplyTuning();
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (impactSource == null)
            {
                impactSource = GetComponent<ImpactSource>();
            }

            home = transform.position;
            patrolHalfLength = patrolOffset.magnitude;
            patrolAxis = patrolHalfLength > 0.001f ? patrolOffset / patrolHalfLength : Vector3.right;

            runtimeMaterial = new PhysicsMaterial("BouncingBall (runtime)")
            {
                dynamicFriction = 0.4f,
                staticFriction = 0.4f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average
            };

            Collider[] colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger)
                {
                    colliders[i].sharedMaterial = runtimeMaterial;
                }
            }

            ApplyTuning();
        }

        private void Start()
        {
            ResetState();
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void OnValidate()
        {
            ApplyTuning();
        }

        private void FixedUpdate()
        {
            Vector3 offset = body.position - home;
            float along = Vector3.Dot(offset, patrolAxis);
            Vector3 lateral = offset - patrolAxis * along;
            lateral.y = 0f;

            if (offset.y < -resetDistance || Mathf.Abs(along) > patrolHalfLength + resetDistance || lateral.magnitude > resetDistance)
            {
                ResetState();
                return;
            }

            if (along >= patrolHalfLength)
            {
                direction = -1;
            }
            else if (along <= -patrolHalfLength)
            {
                direction = 1;
            }

            // Steer the horizontal velocity; gravity and bounces own the vertical axis.
            Vector3 desired = patrolHalfLength > 0.001f ? patrolAxis * (direction * travelSpeed) : Vector3.zero;
            desired -= lateral * 2f; // drift back onto the patrol line
            desired.y = 0f;

            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, desired, steering * Time.fixedDeltaTime);
            body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Bounce(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            Bounce(collision);
        }

        /// <summary>Returns the ball to its start position and motion.</summary>
        [ContextMenu("Reset")]
        public void ResetState()
        {
            if (body == null)
            {
                return;
            }

            direction = startPhase < 0f ? -1 : 1;
            Vector3 start = home + patrolAxis * (patrolHalfLength * startPhase);
            body.position = start;
            transform.position = start;
            body.angularVelocity = Vector3.zero;
            body.linearVelocity = patrolHalfLength > 0.001f ? patrolAxis * (direction * travelSpeed) : Vector3.zero;
            body.WakeUp();
        }

        private void Bounce(Collision collision)
        {
            // Only surfaces underneath the ball re-launch it.
            bool fromBelow = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y > 0.6f)
                {
                    fromBelow = true;
                    break;
                }
            }

            if (!fromBelow)
            {
                return;
            }

            float bounceSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * bounceHeight);
            Vector3 velocity = body.linearVelocity;
            if (velocity.y < bounceSpeed)
            {
                velocity.y = bounceSpeed;
                body.linearVelocity = velocity;
            }
        }

        private void ApplyTuning()
        {
            if (runtimeMaterial != null)
            {
                runtimeMaterial.bounciness = bounciness;
            }

            if (impactSource != null)
            {
                impactSource.ForceMultiplier = impactForce;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? home : transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            Gizmos.DrawLine(center - patrolOffset, center + patrolOffset);
            Gizmos.DrawWireSphere(center - patrolOffset, 0.2f);
            Gizmos.DrawWireSphere(center + patrolOffset, 0.2f);
        }
    }
}
