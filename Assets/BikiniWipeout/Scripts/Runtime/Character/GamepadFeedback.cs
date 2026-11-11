using UnityEngine;
using UnityEngine.InputSystem;

namespace BikiniWipeout
{
    /// <summary>
    /// Controller rumble for wipeouts, shoves and hard landings. Only vibrates while a gamepad is the
    /// device in use, always stops on pause / disable, and can be switched off.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamepadFeedback : MonoBehaviour
    {
        [SerializeField] internal KnockdownController knockdown;
        [SerializeField] internal CharacterMotor motor;

        [SerializeField] internal bool enableRumble = true;

        [Header("Knockdown")]
        [SerializeField, Range(0f, 1f)] internal float knockdownLow = 0.9f;
        [SerializeField, Range(0f, 1f)] internal float knockdownHigh = 0.5f;
        [SerializeField, Min(0.01f)] internal float knockdownDuration = 0.35f;

        [Header("Shove")]
        [SerializeField, Range(0f, 1f)] internal float shoveLow = 0.35f;
        [SerializeField, Range(0f, 1f)] internal float shoveHigh = 0.2f;
        [SerializeField, Min(0.01f)] internal float shoveDuration = 0.15f;

        [Header("Landing")]
        [Tooltip("Landing speed (m/s) below which there is no rumble.")]
        [SerializeField, Min(0f)] internal float minLandingSpeed = 8f;
        [SerializeField, Range(0f, 1f)] internal float landingLow = 0.4f;
        [SerializeField, Min(0.01f)] internal float landingDuration = 0.12f;

        private Gamepad rumbling;
        private float low;
        private float high;
        private float timeLeft;
        private float totalTime;

        private void Awake()
        {
            if (knockdown == null) knockdown = GetComponent<KnockdownController>();
            if (motor == null) motor = GetComponent<CharacterMotor>();
        }

        private void Start()
        {
            if (knockdown != null)
            {
                knockdown.KnockedDown += OnKnockedDown;
                knockdown.Shoved += OnShoved;
            }

            if (motor != null)
            {
                motor.Landed += OnLanded;
            }
        }

        private void OnDestroy()
        {
            if (knockdown != null)
            {
                knockdown.KnockedDown -= OnKnockedDown;
                knockdown.Shoved -= OnShoved;
            }

            if (motor != null)
            {
                motor.Landed -= OnLanded;
            }

            Stop();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Update()
        {
            if (rumbling == null)
            {
                return;
            }

            // Real time, and silence while paused (the game's time scale is 0 then).
            if (Time.timeScale <= 0f || !enableRumble)
            {
                Stop();
                return;
            }

            timeLeft -= Time.unscaledDeltaTime;
            if (timeLeft <= 0f)
            {
                Stop();
                return;
            }

            float fade = Mathf.Clamp01(timeLeft / totalTime);
            rumbling.SetMotorSpeeds(low * fade, high * fade);
        }

        private void OnKnockedDown(ImpactData impact)
        {
            Pulse(knockdownLow, knockdownHigh, knockdownDuration);
        }

        private void OnShoved(ImpactData impact)
        {
            Pulse(shoveLow, shoveHigh, shoveDuration);
        }

        private void OnLanded(float speed)
        {
            if (speed >= minLandingSpeed)
            {
                Pulse(landingLow, 0f, landingDuration);
            }
        }

        private void Pulse(float lowMotor, float highMotor, float duration)
        {
            if (!enableRumble || !InputDeviceTracker.UsingGamepad || Time.timeScale <= 0f)
            {
                return;
            }

            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                return;
            }

            // A stronger pulse replaces a weaker one that is still running.
            if (rumbling != null && timeLeft > 0f && Mathf.Max(lowMotor, highMotor) < Mathf.Max(low, high) * (timeLeft / totalTime))
            {
                return;
            }

            rumbling = pad;
            low = lowMotor;
            high = highMotor;
            totalTime = duration;
            timeLeft = duration;
            pad.SetMotorSpeeds(low, high);
        }

        private void Stop()
        {
            if (rumbling != null)
            {
                rumbling.ResetHaptics();
                rumbling = null;
            }

            timeLeft = 0f;
        }
    }
}
