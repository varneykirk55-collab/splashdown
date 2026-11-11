using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Simple orbit camera: mouse / right-stick look, smoothed follow, collision avoidance, and a
    /// wider, lazier framing while the character is ragdolled. All tuning lives in <see cref="CameraConfig"/>.
    /// </summary>
    [DefaultExecutionOrder(200)]
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
        private ZoomController zoom;
        private float wipeoutBlend;
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private bool initialized;
        private SphereCollider collisionProbe;

        /// <summary>When false, look input is ignored (pause, menus, results).</summary>
        public bool LookEnabled { get; set; } = true;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public PlayerCharacter Target => target;

        /// <summary>Current zoom distance before wipeout framing and collision are applied.</summary>
        public float ZoomDistance => zoom != null ? zoom.Current : (config != null ? config.distance : 0f);

        /// <summary>Distance the zoom is heading towards.</summary>
        public float ZoomTargetDistance => zoom != null ? zoom.Target : ZoomDistance;

        /// <summary>The distance the camera is actually at this frame (after collision and wipeout framing).</summary>
        public float ActualDistance => currentDistance;

        /// <summary>Sets the zoom target (clamped to the configured range).</summary>
        public void SetZoomDistance(float distance, bool instant = false)
        {
            zoom.SetTarget(distance);
            if (instant)
            {
                zoom.Snap();
            }
        }

        /// <summary>Returns the zoom to the configured default distance.</summary>
        public void ResetZoom()
        {
            SetZoomDistance(config.distance);
        }

        private void Awake()
        {
            collisionProbe = gameObject.AddComponent<SphereCollider>();
            collisionProbe.enabled = false;
            collisionProbe.hideFlags = HideFlags.HideInInspector;
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CameraConfig>();
                Debug.LogWarning("[ThirdPersonCamera] No CameraConfig assigned; using default values.", this);
            }

            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            zoom = new ZoomController(config.zoomMinDistance, config.zoomMaxDistance, config.distance, config.zoomSmoothTime);
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
            UpdateZoom(dt);

            bool wipeout = target.Knockdown != null && target.Knockdown.IsDown;
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

            // Bound lag during fast falls so the contestant cannot drop out of frame.
            pivot = desiredPivot + Vector3.ClampMagnitude(pivot - desiredPivot, config.maxFocusLag);
            pivot = ResolveOverlap(pivot);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 backward = rotation * Vector3.back;

            // Player zoom (Phase 1 polish) combined with Astra's wipeout framing and minimum distance.
            float wantedDistance = Mathf.Max(config.minDistance,
                zoom.Current * Mathf.Lerp(1f, config.wipeoutDistanceMultiplier, wipeoutBlend));
            float allowedDistance = wantedDistance;
            if (Physics.SphereCast(pivot, config.collisionRadius, backward, out RaycastHit hit, wantedDistance,
                    GameLayers.CameraObstructionMask, QueryTriggerInteraction.Ignore))
            {
                allowedDistance = Mathf.Max(0f, hit.distance - 0.05f);
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

            Vector3 cameraPosition = pivot + backward * currentDistance;
            // Sphere casts do not report an initially overlapping pivot. Resolve the endpoint too.
            cameraPosition = ResolveOverlap(cameraPosition);
            transform.SetPositionAndRotation(cameraPosition, rotation);

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
            // The player's chosen zoom survives respawns.
            zoom.Configure(config.zoomMinDistance, config.zoomMaxDistance, config.zoomSmoothTime);
            zoom.Snap();
            currentDistance = zoom.Current;
            wipeoutBlend = 0f;
            initialized = true;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(pivot + rotation * Vector3.back * currentDistance, rotation);
        }

        private Vector3 ResolveOverlap(Vector3 position)
        {
            collisionProbe.radius = config.collisionRadius;
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                foreach (Collider obstacle in Physics.OverlapSphere(position, config.collisionRadius,
                    GameLayers.CameraObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    if (Physics.ComputePenetration(collisionProbe, position, Quaternion.identity,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out Vector3 direction, out float depth))
                    {
                        position += direction * (depth + 0.01f);
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            return position;
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
                // Response curve: small stick movements turn slowly for precise aiming, full deflection turns at full speed.
                float magnitude = Mathf.Clamp01(look.magnitude);
                Vector2 shaped = look.normalized * Mathf.Pow(magnitude, config.gamepadLookExponent);
                float step = config.gamepadLookSpeed * Time.unscaledDeltaTime;
                yawDelta = shaped.x * step;
                pitchDelta = shaped.y * step;
            }

            yaw = Mathf.Repeat(yaw + yawDelta, 360f);
            pitch = Mathf.Clamp(pitch + (config.invertY ? pitchDelta : -pitchDelta), config.minPitch, config.maxPitch);
        }

        private void UpdateZoom(float dt)
        {
            // Limits and smoothing come from the config asset every frame so they can be tuned live.
            zoom.Configure(config.zoomMinDistance, config.zoomMaxDistance, config.zoomSmoothTime);

            ICharacterInput input = target.Input;
            if (LookEnabled && input != null)
            {
                float amount = input.Zoom;
                if (input.ZoomIsScroll)
                {
                    zoom.AddNotches(ZoomController.NormalizeScroll(amount), config.scrollZoomStep);
                }
                else
                {
                    zoom.AddContinuous(amount, config.heldZoomSpeed, Time.unscaledDeltaTime);
                }
            }

            // Zoom keeps easing in real time, so it never freezes mid-way if the game is paused.
            zoom.Update(Time.unscaledDeltaTime);
        }
    }
}
