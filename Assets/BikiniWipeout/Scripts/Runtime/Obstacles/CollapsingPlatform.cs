using UnityEngine;

namespace BikiniWipeout
{
    public enum CollapseState
    {
        Idle,

        /// <summary>Stepped on: shaking as a warning, still solid.</summary>
        Shaking,
        Falling,

        /// <summary>Gone. Waiting to be restored.</summary>
        Down
    }

    /// <summary>
    /// Obstacle 09: one segment of a collapsing bridge. Standing on it starts a short, visible warning
    /// shake; then it drops away. It restores itself after a delay, and immediately when the course
    /// asks obstacles to reset (see <see cref="ObstacleResetter"/>), so a respawn never meets a hole.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class CollapsingPlatform : MonoBehaviour, IResettable
    {
        [Tooltip("Seconds between the first step on the segment and the moment it lets go.")]
        [SerializeField, Min(0f)] internal float warningDelay = 0.55f;

        [Tooltip("Seconds after letting go at which the segment stops being solid.")]
        [SerializeField, Min(0f)] internal float colliderDropDelay = 0.12f;

        [Tooltip("Seconds the segment stays gone before it is restored.")]
        [SerializeField, Min(0.5f)] internal float restoreDelay = 4.5f;

        [SerializeField, Min(1f)] internal float fallDistance = 9f;
        [SerializeField, Min(1f)] internal float fallGravity = 16f;

        [Tooltip("Degrees per second the segment tumbles while falling.")]
        [SerializeField] internal float tumbleSpeed = 55f;

        [Tooltip("Optional. Visual child that shakes during the warning (the collider does not move).")]
        [SerializeField] internal Transform shakeVisual;

        [SerializeField] internal AudioEventId warningEvent = AudioEventId.BridgeCrack;
        [SerializeField] internal AudioEventId collapseEvent = AudioEventId.BridgeCollapse;

        private Rigidbody body;
        private Collider[] colliders;
        private Renderer[] renderers;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 shakeHome;
        private float timer;

        public CollapseState State { get; private set; } = CollapseState.Idle;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            homePosition = transform.position;
            homeRotation = transform.rotation;
            if (shakeVisual != null)
            {
                shakeHome = shakeVisual.localPosition;
            }
        }

        private void Update()
        {
            switch (State)
            {
                case CollapseState.Idle:
                    if (PlayerLocator.TryGetActiveMotor(out _, out CharacterMotor motor) && PlayerLocator.IsStandingOn(motor, transform))
                    {
                        Trigger();
                    }

                    break;

                case CollapseState.Shaking:
                    timer += Time.deltaTime;
                    if (shakeVisual != null)
                    {
                        float strength = 0.035f + 0.05f * Mathf.Clamp01(timer / Mathf.Max(0.01f, warningDelay));
                        shakeVisual.localPosition = shakeHome + new Vector3(Mathf.Sin(timer * 71f), Mathf.Sin(timer * 93f) * 0.6f,
                            Mathf.Cos(timer * 83f)) * strength;
                    }

                    if (timer >= warningDelay)
                    {
                        BeginFall();
                    }

                    break;

                case CollapseState.Down:
                    timer += Time.deltaTime;
                    if (timer >= restoreDelay)
                    {
                        ResetState();
                    }

                    break;
            }
        }

        private void FixedUpdate()
        {
            if (State != CollapseState.Falling)
            {
                return;
            }

            timer += Time.fixedDeltaTime;
            float drop = 0.5f * fallGravity * timer * timer;
            body.MovePosition(homePosition + Vector3.down * drop);
            body.MoveRotation(homeRotation * Quaternion.Euler(tumbleSpeed * timer, 0f, tumbleSpeed * 0.4f * timer));

            if (timer >= colliderDropDelay)
            {
                SetSolid(false);
            }

            if (drop >= fallDistance)
            {
                State = CollapseState.Down;
                timer = 0f;
                SetVisible(false);
            }
        }

        /// <summary>Starts the warning shake, as if the character had stepped on the segment.</summary>
        [ContextMenu("Trigger")]
        public void Trigger()
        {
            if (State != CollapseState.Idle)
            {
                return;
            }

            State = CollapseState.Shaking;
            timer = 0f;
            AudioManager.Play(warningEvent, transform.position);
        }

        /// <summary>Puts the segment back in place, solid and visible.</summary>
        [ContextMenu("Reset")]
        public void ResetState()
        {
            State = CollapseState.Idle;
            timer = 0f;
            if (body == null)
            {
                return;
            }

            transform.SetPositionAndRotation(homePosition, homeRotation);
            body.position = homePosition;
            body.rotation = homeRotation;
            if (shakeVisual != null)
            {
                shakeVisual.localPosition = shakeHome;
            }

            SetSolid(true);
            SetVisible(true);
        }

        private void BeginFall()
        {
            State = CollapseState.Falling;
            timer = 0f;
            if (shakeVisual != null)
            {
                shakeVisual.localPosition = shakeHome;
            }

            AudioManager.Play(collapseEvent, transform.position);
        }

        private void SetSolid(bool solid)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled != solid)
                {
                    colliders[i].enabled = solid;
                }
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }
    }
}
