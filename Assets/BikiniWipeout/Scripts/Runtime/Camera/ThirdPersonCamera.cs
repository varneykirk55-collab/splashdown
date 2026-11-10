using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Simple orbit camera: mouse / right-stick look, smoothed follow, collision avoidance, and a
    /// wider, lazier framing while the character is ragdolled. All tuning lives in <see cref="CameraConfig"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] internal PlayerCharacter target;
        [SerializeField] internal CameraConfig config;

        [Tooltip("Optional. Field of view is driven on this camera. Defaults to the Camera on this object.")]
        [SerializeField] internal Camera controlledCamera;

        private float yaw;
        private float pitch;
        private float currentDistance;
        private float wipeoutBlend;
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private bool initialized;

        /// <summary>When false, look input is ignored (pause, menus, results).</summary>
        public bool LookEnabled { get; set; } = true;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public PlayerCharacter Target => target;

        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CameraConfig>();
                Debug.LogWarning("[ThirdPersonCamera] No CameraConfig assigned; using default values.", this);
            }

            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }
        }

        private void Start()
        {
            if (target != null)
            {
                if (target.Respawner != null)
                {
                    target.Respawner.Respawned += SnapBehindTarget;
                }

                SnapBehindTarget();
            }
        }

        private void OnDestroy()
        {
            if (target != null && target.Respawner != null)
            {
                target.Respawner.Respawned -= SnapBehindTarget;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (!initialized)
            {
                SnapBehindTarget();
            }

            float dt = Time.deltaTime;
            ReadLookInput();

            bool wipeout = target.Knockdown != null && target.Knockdown.IsRagdolled;
            wipeoutBlend = Mathf.MoveTowards(wipeoutBlend, wipeout ? 1f : 0f, config.wipeoutBlendSpeed * dt);

            Vector3 desiredPivot = target.FocusPosition + Vector3.up * config.pivotHeightOffset;
            float smoothTime = Mathf.Lerp(config.followSmoothTime, config.wipeoutFollowSmoothTime, wipeoutBlend);
            if (smoothTime > 0f && dt > 0f)
            {
                pivot = Vector3.SmoothDamp(pivot, desiredPivot, ref pivotVelocity, smoothTime, Mathf.Infinity, dt);
            }
            else if (dt > 0f)
            {
                pivot = desiredPivot;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 backward = rotation * Vector3.back;

            float wantedDistance = config.distance * Mathf.Lerp(1f, config.wipeoutDistanceMultiplier, wipeoutBlend);
            float allowedDistance = wantedDistance;
            if (Physics.SphereCast(pivot, config.collisionRadius, backward, out RaycastHit hit, wantedDistance,
                    GameLayers.CameraObstructionMask, QueryTriggerInteraction.Ignore))
            {
                allowedDistance = Mathf.Max(config.minDistance, hit.distance);
            }

            // Pull in immediately when blocked, ease back out when the view clears.
            if (allowedDistance < currentDistance)
            {
                currentDistance = allowedDistance;
            }
            else
            {
                currentDistance = Mathf.MoveTowards(currentDistance, allowedDistance, config.distanceRecoverSpeed * dt);
            }

            transform.SetPositionAndRotation(pivot + backward * currentDistance, rotation);

            if (controlledCamera != null)
            {
                controlledCamera.fieldOfView = config.fieldOfView + config.wipeoutFovBoost * wipeoutBlend;
            }
        }

        /// <summary>Places the camera directly behind the character with no smoothing lag.</summary>
        public void SnapBehindTarget()
        {
            if (target == null)
            {
                return;
            }

            yaw = target.transform.eulerAngles.y;
            pitch = Mathf.Clamp(config.startPitch, config.minPitch, config.maxPitch);
            pivot = target.FocusPosition + Vector3.up * config.pivotHeightOffset;
            pivotVelocity = Vector3.zero;
            currentDistance = config.distance;
            wipeoutBlend = 0f;
            initialized = true;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(pivot + rotation * Vector3.back * currentDistance, rotation);
        }

        private void ReadLookInput()
        {
            ICharacterInput input = target.Input;
            if (!LookEnabled || input == null)
            {
                return;
            }

            Vector2 look = input.Look;
            if (look.sqrMagnitude < 0.000001f)
            {
                return;
            }

            float yawDelta;
            float pitchDelta;
            if (input.LookIsPointerDelta)
            {
                yawDelta = look.x * config.mouseSensitivity;
                pitchDelta = look.y * config.mouseSensitivity;
            }
            else
            {
                float step = config.gamepadLookSpeed * Time.unscaledDeltaTime;
                yawDelta = look.x * step;
                pitchDelta = look.y * step;
            }

            yaw = Mathf.Repeat(yaw + yawDelta, 360f);
            pitch = Mathf.Clamp(pitch + (config.invertY ? pitchDelta : -pitchDelta), config.minPitch, config.maxPitch);
        }
    }
}
