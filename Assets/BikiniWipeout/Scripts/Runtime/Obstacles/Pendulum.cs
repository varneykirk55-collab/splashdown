using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Swinging pendulum: a kinematic arm that rocks about a pivot. The angle is a pure function of
    /// time, so it resets to exactly the same motion. Give the arm an <see cref="ImpactSource"/>
    /// and it knocks characters over like any other obstacle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Pendulum : MonoBehaviour, IResettable
    {
        [Tooltip("Kinematic Rigidbody at the pivot. The arm and weight hang below it as children.")]
        [SerializeField] internal Rigidbody armBody;

        [Tooltip("Swing axis in this object's local space. Forward (Z) swings the weight left and right.")]
        [SerializeField] internal Vector3 localAxis = Vector3.forward;

        [Tooltip("Maximum swing angle to either side, in degrees.")]
        [SerializeField, Range(0f, 85f)] internal float amplitude = 42f;

        [Tooltip("Seconds for one full swing there and back.")]
        [SerializeField, Min(0.2f)] internal float period = 3.2f;

        [Tooltip("0..1: where in the swing the pendulum starts. Use it to stagger a row of pendulums.")]
        [SerializeField, Range(0f, 1f)] internal float startPhase;

        [SerializeField] internal bool playOnStart = true;

        [Tooltip("How hard the weight hits. Forwarded to the arm's ImpactSource.")]
        [SerializeField, Min(0f)] internal float impactForce = 1.4f;

        [SerializeField] internal ImpactSource impactSource;

        private float elapsed;
        private bool running;

        public bool IsRunning => running;

        /// <summary>Current swing angle in degrees.</summary>
        public float CurrentAngle => AngleAt(elapsed);

        public float ImpactForce
        {
            get => impactForce;
            set
            {
                impactForce = Mathf.Max(0f, value);
                ApplyImpactForce();
            }
        }

        private void Awake()
        {
            if (armBody == null) armBody = GetComponentInChildren<Rigidbody>();
            if (impactSource == null) impactSource = GetComponentInChildren<ImpactSource>();

            if (armBody != null)
            {
                armBody.isKinematic = true;
                armBody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            ApplyImpactForce();
        }

        private void Start()
        {
            ResetState();
        }

        private void OnValidate()
        {
            ApplyImpactForce();
        }

        private void FixedUpdate()
        {
            if (!running || armBody == null)
            {
                return;
            }

            elapsed += Time.fixedDeltaTime;
            armBody.MoveRotation(RotationAt(elapsed));
        }

        /// <summary>Swing angle (degrees) <paramref name="time"/> seconds after a reset.</summary>
        public float AngleAt(float time)
        {
            return amplitude * Mathf.Sin((time / period + startPhase) * Mathf.PI * 2f);
        }

        [ContextMenu("Reset")]
        public void ResetState()
        {
            elapsed = 0f;
            running = playOnStart;
            if (armBody != null)
            {
                Quaternion rotation = RotationAt(0f);
                armBody.transform.rotation = rotation;
                armBody.rotation = rotation;
            }
        }

        [ContextMenu("Restart")]
        public void Restart()
        {
            ResetState();
            running = true;
        }

        [ContextMenu("Stop")]
        public void Stop()
        {
            running = false;
        }

        private Quaternion RotationAt(float time)
        {
            Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.forward;
            return transform.rotation * Quaternion.AngleAxis(AngleAt(time), axis);
        }

        private void ApplyImpactForce()
        {
            if (impactSource != null)
            {
                impactSource.ForceMultiplier = impactForce;
            }
        }
    }
}
