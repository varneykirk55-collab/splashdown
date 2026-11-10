using System;

namespace BikiniWipeout.Core
{
    /// <summary>
    /// Camera zoom model: a target distance that input moves (in discrete wheel notches or continuous
    /// axis/button input) and a smoothed current distance that follows it. Engine-free so the
    /// rules are unit tested; <c>ThirdPersonCamera</c> only feeds it input and reads the result.
    ///
    /// Sign convention for all input: positive = zoom IN (camera moves closer).
    /// </summary>
    public sealed class ZoomController
    {
        private float min;
        private float max;
        private float smoothTime;
        private float velocity;

        public ZoomController(float minDistance, float maxDistance, float initialDistance, float smoothTime)
        {
            Configure(minDistance, maxDistance, smoothTime);
            Target = Clamp(initialDistance);
            Current = Target;
        }

        /// <summary>Distance the camera is heading towards.</summary>
        public float Target { get; private set; }

        /// <summary>Smoothed distance to use this frame.</summary>
        public float Current { get; private set; }

        public float Min => min;
        public float Max => max;

        /// <summary>0 = fully zoomed in, 1 = fully zoomed out.</summary>
        public float Normalized => max - min <= 0.0001f ? 0f : (Current - min) / (max - min);

        /// <summary>Re-applies limits and smoothing. Safe to call every frame (config assets can be tuned live).</summary>
        public void Configure(float minDistance, float maxDistance, float smoothTimeSeconds)
        {
            min = Math.Max(0.01f, minDistance);
            max = Math.Max(min, maxDistance);
            smoothTime = Math.Max(0f, smoothTimeSeconds);
            Target = Clamp(Target);
            Current = Clamp(Current);
        }

        /// <summary>Mouse wheel: each notch moves the target by <paramref name="stepSize"/> metres.</summary>
        public void AddNotches(float notches, float stepSize)
        {
            if (float.IsNaN(notches) || float.IsInfinity(notches) || notches == 0f)
            {
                return;
            }

            Target = Clamp(Target - notches * Math.Max(0f, stepSize));
        }

        /// <summary>Held input (d-pad, keys): <paramref name="axis"/> in -1..1 moves the target at <paramref name="speed"/> m/s.</summary>
        public void AddContinuous(float axis, float speed, float deltaTime)
        {
            if (float.IsNaN(axis) || axis == 0f || deltaTime <= 0f)
            {
                return;
            }

            float clamped = axis < -1f ? -1f : (axis > 1f ? 1f : axis);
            Target = Clamp(Target - clamped * Math.Max(0f, speed) * deltaTime);
        }

        public void SetTarget(float distance)
        {
            Target = Clamp(distance);
        }

        /// <summary>Jumps to the target with no smoothing (respawn, scene start).</summary>
        public void Snap()
        {
            Current = Target;
            velocity = 0f;
        }

        /// <summary>Advances the smoothing and returns <see cref="Current"/>.</summary>
        public float Update(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return Current;
            }

            if (smoothTime <= 0.0001f)
            {
                Current = Target;
                velocity = 0f;
                return Current;
            }

            // Critically damped spring (same formulation as Unity's Mathf.SmoothDamp, without the engine reference).
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = Current - Target;
            float temp = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temp) * exp;
            Current = Target + (change + temp) * exp;

            // Never overshoot past the target.
            float remaining = Current - Target;
            if (change != 0f && Math.Sign(change) != Math.Sign(remaining) && remaining != 0f)
            {
                Current = Target;
                velocity = 0f;
            }

            Current = Clamp(Current);
            return Current;
        }

        /// <summary>
        /// Converts a raw mouse-wheel value to notches. Windows reports ±120 per notch unless the Input
        /// System's scroll normalization is on, in which case it reports ±1; both are accepted.
        /// A single frame is capped so a fast spin cannot cross the whole range at once.
        /// </summary>
        public static float NormalizeScroll(float raw)
        {
            if (float.IsNaN(raw) || float.IsInfinity(raw))
            {
                return 0f;
            }

            float notches = Math.Abs(raw) >= 20f ? raw / 120f : raw;
            return notches < -4f ? -4f : (notches > 4f ? 4f : notches);
        }

        private float Clamp(float value)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
