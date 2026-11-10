using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Reusable "this thing hurts" component. Put it on the object that owns the Rigidbody (or on a
    /// static collider) and give it a collider or trigger zone. On contact it measures how fast its
    /// own surface is moving at the contact point and reports that to any <see cref="IImpactReceiver"/>.
    /// It knows nothing about ragdolls: the receiver decides what an impact means.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactSource : MonoBehaviour
    {
        [Tooltip("Scales how hard this obstacle hits. 1 = natural, 0 = harmless.")]
        [SerializeField, Min(0f)] internal float forceMultiplier = 1f;

        [SerializeField] internal ImpactSourceKind kind = ImpactSourceKind.Generic;

        [Tooltip("Optional. Dynamic bodies report their physics velocity; kinematic ones are tracked from their motion.")]
        [SerializeField] internal Rigidbody body;

        [Tooltip("Solid colliders used to find the contact point. Auto-collected from children when empty.")]
        [SerializeField] internal Collider[] surfaceColliders;

        [Tooltip("Minimum seconds between two impacts reported by this obstacle.")]
        [SerializeField, Min(0f)] internal float rehitInterval = 0.35f;

        private Vector3 lastPosition;
        private Quaternion lastRotation = Quaternion.identity;
        private float nextHitTime;

        public float ForceMultiplier
        {
            get => forceMultiplier;
            set => forceMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Raised after a receiver accepted an impact (useful for per-obstacle effects).</summary>
        public event System.Action<ImpactData> ImpactDelivered;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (surfaceColliders == null || surfaceColliders.Length == 0)
            {
                surfaceColliders = CollectSurfaceColliders();
            }

            CapturePose();
        }

        private void OnEnable()
        {
            CapturePose();
        }

        private void FixedUpdate()
        {
            // Sampled before the physics step, so during the following collision callbacks
            // (current pose - last pose) / fixedDeltaTime is the velocity of this step.
            CapturePose();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryImpact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryImpact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryImpact(collision.collider);
        }

        /// <summary>Velocity of the obstacle surface at a world point.</summary>
        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            if (body != null && !body.isKinematic)
            {
                return body.GetPointVelocity(worldPoint);
            }

            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
            {
                return Vector3.zero;
            }

            GetPose(out Vector3 position, out Quaternion rotation);
            Vector3 localOffset = Quaternion.Inverse(rotation) * (worldPoint - position);
            Vector3 previousPoint = lastPosition + lastRotation * localOffset;
            return (worldPoint - previousPoint) / dt;
        }

        private void TryImpact(Collider other)
        {
            if (!isActiveAndEnabled || other == null || Time.time < nextHitTime)
            {
                return;
            }

            IImpactReceiver receiver = other.GetComponentInParent<IImpactReceiver>();
            if (receiver == null)
            {
                return;
            }

            Vector3 center = receiver.ImpactCenter;
            Vector3 point = ClosestSurfacePoint(center);
            Vector3 velocity = GetPointVelocity(point);

            Vector3 normal = center - point;
            if (normal.sqrMagnitude < 0.0001f)
            {
                // Receiver centre is inside the obstacle: fall back to the direction of travel.
                normal = velocity.sqrMagnitude > 0.0001f ? velocity : center - transform.position;
            }

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }

            var impact = new ImpactData
            {
                Point = point,
                Normal = normal.normalized,
                SourceVelocity = velocity,
                ForceMultiplier = forceMultiplier,
                Kind = kind,
                Source = gameObject
            };

            if (receiver.ReceiveImpact(in impact))
            {
                nextHitTime = Time.time + rehitInterval;
                ImpactDelivered?.Invoke(impact);
            }
        }

        private Vector3 ClosestSurfacePoint(Vector3 target)
        {
            Vector3 best = transform.position;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < surfaceColliders.Length; i++)
            {
                Collider c = surfaceColliders[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 p = c.ClosestPoint(target);
                float sqr = (p - target).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p;
                }
            }

            return best;
        }

        private Collider[] CollectSurfaceColliders()
        {
            Collider[] all = GetComponentsInChildren<Collider>(true);
            int solidCount = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (SupportsClosestPoint(all[i]) && !all[i].isTrigger)
                {
                    solidCount++;
                }
            }

            // Prefer solid colliders; fall back to trigger zones if the obstacle has no solid shape.
            bool useTriggers = solidCount == 0;
            var result = new System.Collections.Generic.List<Collider>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (SupportsClosestPoint(all[i]) && all[i].isTrigger == useTriggers)
                {
                    result.Add(all[i]);
                }
            }

            return result.ToArray();
        }

        private static bool SupportsClosestPoint(Collider c)
        {
            // Collider.ClosestPoint only supports primitive and convex mesh colliders.
            var mesh = c as MeshCollider;
            return mesh == null || mesh.convex;
        }

        private void CapturePose()
        {
            GetPose(out lastPosition, out lastRotation);
        }

        private void GetPose(out Vector3 position, out Quaternion rotation)
        {
            if (body != null)
            {
                position = body.position;
                rotation = body.rotation;
            }
            else
            {
                position = transform.position;
                rotation = transform.rotation;
            }
        }
    }
}
