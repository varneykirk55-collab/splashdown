using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    public enum SlipShape
    {
        /// <summary>Any collider; the slope is read with a ray under the character (water slides, ramps).</summary>
        Collider,

        /// <summary>A sphere collider; the slope comes from where the character stands on the ball (Big Balls).</summary>
        Sphere
    }

    /// <summary>
    /// Low-traction footing: obstacle 07 (water slide ramp) and obstacle 13 (the Big Balls).
    ///
    /// The player's controls are never altered. Instead the surface behaves like a slope with very
    /// little friction: wherever it is steeper than its traction can hold, the character accelerates
    /// downhill (g * (sin - traction * cos)), on top of whatever the player steers. On a ball that
    /// means the flat crown is safe and everything else slides you off; an off-centre landing turns
    /// part of the landing speed into a sideways slide and a small hop. When contact ends, the
    /// slide speed is handed to the motor as ordinary momentum, which fades out by itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlipperySurface : MonoBehaviour
    {
        [SerializeField] internal SlipShape shape = SlipShape.Collider;

        [Tooltip("The surface. Defaults to the collider on this object. Must be a SphereCollider for the Sphere shape.")]
        [SerializeField] internal Collider surface;

        [Header("Traction")]
        [Tooltip("Friction coefficient of the surface. 0 = ice, 1 = grippy. The surface holds slopes up to atan(traction).")]
        [SerializeField, Range(0f, 1f)] internal float traction = 0.12f;

        [Tooltip("Gravity used for sliding (m/s^2).")]
        [SerializeField, Min(0f)] internal float slideGravity = 14f;

        [SerializeField, Min(0f)] internal float maxSlideSpeed = 9f;

        [Tooltip("How quickly a slide dies out where the surface is flat enough to stand on (m/s^2).")]
        [SerializeField, Min(0f)] internal float flatDamping = 10f;

        [Tooltip("The character counts as touching the surface within this distance (m).")]
        [SerializeField, Min(0.01f)] internal float contactDistance = 0.3f;

        [Header("Landings")]
        [Tooltip("Fraction of the landing speed turned into a downhill slide on a sloping landing.")]
        [SerializeField, Range(0f, 1f)] internal float landingBounce = 0.6f;

        [Tooltip("Fraction of the landing speed returned as a hop on a sloping landing. 0 = none.")]
        [SerializeField, Range(0f, 1f)] internal float landingHop = 0.3f;

        [Tooltip("Landings on slopes gentler than this (degrees) are clean: no bounce.")]
        [SerializeField, Range(0f, 60f)] internal float bounceMinAngle = 16f;

        [Tooltip("Hops slower than this (m/s) are dropped.")]
        [SerializeField, Min(0f)] internal float hopMinSpeed = 2f;

        [Header("Ragdoll contact")]
        [Tooltip("Gives the collider a slick, bouncy physics material so a ragdoll slides and bounces off it.")]
        [SerializeField] internal bool applyRagdollMaterial = true;
        [SerializeField, Range(0f, 1f)] internal float ragdollFriction = 0.05f;
        [SerializeField, Range(0f, 1f)] internal float ragdollBounciness = 0.45f;

        [Header("Audio")]
        [SerializeField] internal AudioEventId slideEvent = AudioEventId.SlideWhoosh;
        [SerializeField] internal AudioEventId landEvent = AudioEventId.None;

        private PhysicsMaterial runtimeMaterial;
        private CharacterMotor subscribedMotor;
        private Vector3 slideDirection;
        private float slideSpeed;
        private float nextSlideSoundTime;

        /// <summary>True while the character is touching this surface.</summary>
        public bool InContact { get; private set; }

        /// <summary>Current downhill slide speed added by the surface (m/s).</summary>
        public float SlideSpeed => slideSpeed;

        public float Traction
        {
            get => traction;
            set => traction = Mathf.Clamp01(value);
        }

        /// <summary>World-space radius of the ball (Sphere shape only).</summary>
        public float SphereRadius
        {
            get
            {
                var sphere = surface as SphereCollider;
                if (sphere == null)
                {
                    return 0f;
                }

                Vector3 scale = sphere.transform.lossyScale;
                return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            }
        }

        private void Awake()
        {
            if (surface == null)
            {
                surface = GetComponent<Collider>();
            }

            if (applyRagdollMaterial && surface != null)
            {
                runtimeMaterial = new PhysicsMaterial("SlipperySurface (runtime)")
                {
                    dynamicFriction = ragdollFriction,
                    staticFriction = ragdollFriction,
                    bounciness = ragdollBounciness,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
                surface.sharedMaterial = runtimeMaterial;
            }
        }

        private void OnDisable()
        {
            Subscribe(null);
            InContact = false;
            slideSpeed = 0f;
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
            float dt = Time.deltaTime;
            if (dt <= 0f || surface == null)
            {
                return;
            }

            if (!PlayerLocator.TryGetActiveMotor(out _, out CharacterMotor motor))
            {
                // Ragdolled, respawning or absent: the physics engine (or nothing) is in charge.
                InContact = false;
                slideSpeed = 0f;
                return;
            }

            Subscribe(motor);
            if (!TryGetContact(motor, out Vector3 normal))
            {
                Release(motor);
                return;
            }

            InContact = true;
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal);
            float acceleration = SlipMath.SlideAcceleration(normal.y, slideGravity, traction);
            if (acceleration > 0f && downhill.sqrMagnitude > 1e-6f)
            {
                slideDirection = downhill.normalized;
                slideSpeed = Mathf.Min(maxSlideSpeed, slideSpeed + acceleration * dt);
            }
            else
            {
                slideSpeed = Mathf.MoveTowards(slideSpeed, 0f, flatDamping * dt);
                if (downhill.sqrMagnitude > 1e-6f)
                {
                    slideDirection = downhill.normalized;
                }
            }

            if (slideSpeed <= 0.01f)
            {
                return;
            }

            // A plain displacement through the CharacterController: it collides and slides like any
            // other move, so the character can never be pushed through geometry or teleported.
            motor.Controller.Move(slideDirection * (slideSpeed * dt));

            if (slideSpeed > 2.5f && Time.time >= nextSlideSoundTime)
            {
                nextSlideSoundTime = Time.time + 0.7f;
                AudioManager.Play(slideEvent, motor.transform.position, Mathf.Clamp01(slideSpeed / Mathf.Max(0.1f, maxSlideSpeed)));
            }
        }

        /// <summary>
        /// Is the character touching the surface, and what is the surface normal under it?
        /// </summary>
        public bool TryGetContact(CharacterMotor motor, out Vector3 normal)
        {
            normal = Vector3.up;
            if (motor == null || surface == null || !surface.enabled)
            {
                return false;
            }

            Vector3 feet = motor.transform.position;
            if (shape == SlipShape.Sphere)
            {
                var sphere = surface as SphereCollider;
                if (sphere == null)
                {
                    return false;
                }

                float capsuleRadius = motor.CapsuleRadius;
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                // The rounded bottom of the capsule rests on the ball, so measure from its centre.
                Vector3 offset = feet + Vector3.up * capsuleRadius - center;
                float distance = offset.magnitude;
                if (distance < 0.001f || offset.y <= 0f || distance > SphereRadius + capsuleRadius + contactDistance)
                {
                    return false;
                }

                normal = offset / distance;
                return true;
            }

            const float lift = 0.6f;
            var ray = new Ray(feet + Vector3.up * lift, Vector3.down);
            if (surface.Raycast(ray, out RaycastHit hit, lift + contactDistance))
            {
                normal = hit.normal;
                return true;
            }

            return false;
        }

        private void Release(CharacterMotor motor)
        {
            if (InContact && slideSpeed > 0.5f)
            {
                // Hand the slide over as ordinary momentum; the motor's own damping brings it to rest.
                motor.AddExternalVelocity(slideDirection * slideSpeed);
            }

            InContact = false;
            slideSpeed = 0f;
        }

        private void Subscribe(CharacterMotor motor)
        {
            if (motor == subscribedMotor)
            {
                return;
            }

            if (subscribedMotor != null)
            {
                subscribedMotor.Landed -= OnLanded;
            }

            subscribedMotor = motor;
            if (subscribedMotor != null)
            {
                subscribedMotor.Landed += OnLanded;
            }
        }

        private void OnLanded(float fallSpeed)
        {
            CharacterMotor motor = subscribedMotor;
            if (motor == null || !isActiveAndEnabled || !TryGetContact(motor, out Vector3 normal))
            {
                return;
            }

            AudioManager.Play(landEvent, motor.transform.position, Mathf.Clamp01(0.4f + fallSpeed / 12f));

            float slope = Mathf.Acos(Mathf.Clamp(normal.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal);
            if (slope < bounceMinAngle || downhill.sqrMagnitude < 1e-6f)
            {
                return;
            }

            // An off-centre landing: the part of the impact along the slope becomes a slide,
            // and a fraction of the part into the surface comes back as a hop.
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - normal.y * normal.y));
            InContact = true;
            slideDirection = downhill.normalized;
            slideSpeed = Mathf.Min(maxSlideSpeed, slideSpeed + fallSpeed * sin * landingBounce);

            float hop = fallSpeed * normal.y * landingHop;
            if (landingHop > 0f && hop >= hopMinSpeed)
            {
                motor.Launch(hop);
            }
        }
    }
}
