using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Obstacle C: a kinematic platform that travels along waypoints. Its position is a pure function
    /// of elapsed time, so it never drifts and resets to exactly the same motion. The character motor
    /// rides it automatically (it follows whatever surface it stands on).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class MovingPlatform : MonoBehaviour, IResettable
    {
        [Tooltip("Path points relative to the platform's starting pose. The first is normally (0,0,0).")]
        [SerializeField] internal Vector3[] waypoints = { Vector3.zero, new Vector3(4f, 0f, 0f) };

        [Tooltip("Metres per second.")]
        [SerializeField, Min(0.01f)] internal float speed = 2.2f;

        [Tooltip("Seconds the platform waits at each waypoint.")]
        [SerializeField, Min(0f)] internal float dwellTime = 0.8f;

        [SerializeField] internal PathLoopMode loopMode = PathLoopMode.PingPong;

        [Tooltip("Seconds into the cycle at which the platform starts. Use it to stagger platforms.")]
        [SerializeField, Min(0f)] internal float startTimeOffset;

        [Tooltip("Accelerate and brake between waypoints instead of moving at constant speed.")]
        [SerializeField] internal bool easeInOut = true;

        [SerializeField] internal bool playOnStart = true;

        private Rigidbody body;
        private PathTimeline timeline;
        private Vector3 originPosition;
        private Quaternion originRotation;
        private float elapsed;
        private bool running;
        private bool originCaptured;

        public bool IsRunning => running;
        public float CycleDuration => timeline != null ? timeline.CycleDuration : 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            originPosition = transform.position;
            originRotation = transform.rotation;
            originCaptured = true;
            RebuildTimeline();
        }

        private void Start()
        {
            ResetState();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && originCaptured)
            {
                RebuildTimeline();
            }
        }

        private void FixedUpdate()
        {
            if (!running)
            {
                return;
            }

            elapsed += Time.fixedDeltaTime;
            body.MovePosition(PositionAt(elapsed));
        }

        /// <summary>Returns the platform to the start of its cycle.</summary>
        [ContextMenu("Reset")]
        public void ResetState()
        {
            elapsed = 0f;
            running = playOnStart;
            Vector3 start = PositionAt(0f);
            transform.position = start;
            if (body != null)
            {
                body.position = start;
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

        public void Resume()
        {
            running = true;
        }

        /// <summary>World position of the platform <paramref name="time"/> seconds after a reset.</summary>
        public Vector3 PositionAt(float time)
        {
            if (timeline == null || waypoints == null || waypoints.Length == 0)
            {
                return originPosition;
            }

            PathSample sample = timeline.Evaluate(time + startTimeOffset);
            float t = easeInOut ? PathTimeline.Ease(sample.T) : sample.T;
            Vector3 local = Vector3.Lerp(waypoints[sample.FromIndex], waypoints[sample.ToIndex], t);
            return originPosition + originRotation * local;
        }

        private void RebuildTimeline()
        {
            int count = waypoints != null ? waypoints.Length : 0;
            if (count < 2)
            {
                timeline = new PathTimeline(new float[0], 0f, speed, dwellTime, loopMode);
                return;
            }

            var lengths = new float[count - 1];
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = Vector3.Distance(waypoints[i], waypoints[i + 1]);
            }

            float closing = Vector3.Distance(waypoints[count - 1], waypoints[0]);
            timeline = new PathTimeline(lengths, closing, speed, dwellTime, loopMode);
        }

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            Vector3 basePosition = Application.isPlaying && originCaptured ? originPosition : transform.position;
            Quaternion baseRotation = Application.isPlaying && originCaptured ? originRotation : transform.rotation;
            Gizmos.color = new Color(0.4f, 1f, 0.3f, 0.9f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                Vector3 p = basePosition + baseRotation * waypoints[i];
                Gizmos.DrawWireCube(p, new Vector3(0.4f, 0.1f, 0.4f));
                if (i + 1 < waypoints.Length)
                {
                    Gizmos.DrawLine(p, basePosition + baseRotation * waypoints[i + 1]);
                }
            }
        }
    }
}
