using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Obstacle A: an arm that rotates continuously around a pivot and knocks characters over.
    /// The arm is a kinematic Rigidbody carrying an <see cref="ImpactSource"/>; this component only
    /// drives the rotation. Speed and impact force can be changed at any time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotatingSweeper : MonoBehaviour, IResettable
    {
        [Tooltip("Kinematic Rigidbody of the rotating arm assembly.")]
        [SerializeField] internal Rigidbody armBody;

        [Tooltip("Degrees per second. Negative values reverse the direction.")]
        [SerializeField] internal float rotationSpeed = 70f;

        [Tooltip("Rotation axis in this object's local space.")]
        [SerializeField] internal Vector3 localAxis = Vector3.up;

        [Tooltip("Arm angle (degrees) when the course starts or the obstacle is reset.")]
        [SerializeField] internal float startAngle;

        [SerializeField] internal bool playOnStart = true;

        [Tooltip("How hard the arm hits. Forwarded to the arm's ImpactSource.")]
        [SerializeField, Min(0f)] internal float impactForce = 1.6f;

        [SerializeField] internal ImpactSource impactSource;

        private float angle;
        private bool running;

        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = value;
        }

        public float ImpactForce
        {
            get => impactForce;
            set
            {
                impactForce = Mathf.Max(0f, value);
                ApplyImpactForce();
            }
        }

        public bool IsRunning => running;
        public float CurrentAngle => angle;

        private void Awake()
        {
            if (armBody == null)
            {
                armBody = GetComponentInChildren<Rigidbody>();
            }

            if (impactSource == null)
            {
                impactSource = GetComponentInChildren<ImpactSource>();
            }

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

            angle = Mathf.Repeat(angle + rotationSpeed * Time.fixedDeltaTime, 360f);
            armBody.MoveRotation(RotationFor(angle));
        }

        /// <summary>Returns the arm to its start angle. It keeps running only if <see cref="playOnStart"/> is set.</summary>
        [ContextMenu("Reset")]
        public void ResetState()
        {
            angle = Mathf.Repeat(startAngle, 360f);
            running = playOnStart;
            SnapToAngle();
        }

        /// <summary>Resets the arm and starts it.</summary>
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

        public void Resume()
        {
            running = true;
        }

        private Quaternion RotationFor(float degrees)
        {
            Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
            return transform.rotation * Quaternion.AngleAxis(degrees, axis);
        }

        private void SnapToAngle()
        {
            if (armBody == null)
            {
                return;
            }

            Quaternion rotation = RotationFor(angle);
            armBody.transform.rotation = rotation;
            armBody.rotation = rotation;
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
