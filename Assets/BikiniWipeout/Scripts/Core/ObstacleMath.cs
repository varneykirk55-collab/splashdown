using System;

namespace BikiniWipeout.Core
{
    public enum HazardPhase
    {
        Idle,

        /// <summary>The hazard is about to fire: show and sound the warning.</summary>
        Warning,

        Active
    }

    /// <summary>
    /// Repeating idle -> warning -> active cycle for timed hazards (water jets, tipping buckets).
    /// The phase is a pure function of elapsed time, so hazards are predictable and reset exactly.
    /// </summary>
    public readonly struct HazardCycle
    {
        public HazardCycle(float period, float warningTime, float activeTime, float offset)
        {
            ActiveTime = Math.Max(0.05f, activeTime);
            WarningTime = Math.Max(0f, warningTime);
            Period = Math.Max(period, ActiveTime + WarningTime + 0.05f);
            Offset = offset;
        }

        public float Period { get; }
        public float WarningTime { get; }
        public float ActiveTime { get; }
        public float Offset { get; }

        /// <summary>Seconds of idle at the start of each cycle.</summary>
        public float IdleTime => Period - WarningTime - ActiveTime;

        /// <summary>Phase at <paramref name="time"/>, with 0..1 progress through that phase.</summary>
        public HazardPhase Evaluate(float time, out float progress)
        {
            float t = (time + Offset) % Period;
            if (t < 0f)
            {
                t += Period;
            }

            float idle = IdleTime;
            if (t < idle)
            {
                progress = idle <= 0f ? 1f : t / idle;
                return HazardPhase.Idle;
            }

            t -= idle;
            if (t < WarningTime)
            {
                progress = t / WarningTime;
                return HazardPhase.Warning;
            }

            progress = Math.Min(1f, (t - WarningTime) / ActiveTime);
            return HazardPhase.Active;
        }

        /// <summary>How many activations have started by <paramref name="time"/> (used to fire once per cycle).</summary>
        public int ActivationIndex(float time)
        {
            float shifted = time + Offset - IdleTime - WarningTime;
            return (int)Math.Floor(shifted / Period);
        }
    }

    /// <summary>Physics of standing on low-traction slopes (big balls, water slides) and of launches.</summary>
    public static class SlipMath
    {
        /// <summary>
        /// Acceleration (m/s^2) down a slope for a body with Coulomb friction: g (sin - traction * cos).
        /// <paramref name="normalY"/> is the vertical component of the unit surface normal (1 = flat).
        /// Never negative: friction cannot push uphill.
        /// </summary>
        public static float SlideAcceleration(float normalY, float gravity, float traction)
        {
            if (float.IsNaN(normalY) || float.IsNaN(gravity) || float.IsNaN(traction))
            {
                return 0f;
            }

            float cos = Math.Min(1f, Math.Max(0f, normalY));
            float sin = (float)Math.Sqrt(Math.Max(0f, 1f - cos * cos));
            return Math.Max(0f, Math.Abs(gravity) * (sin - Math.Max(0f, traction) * cos));
        }

        /// <summary>Slope angle (degrees) above which a surface with this traction starts to slide.</summary>
        public static float SlipAngle(float traction)
        {
            return (float)(Math.Atan(Math.Max(0f, traction)) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Seconds in the air after an upward launch at <paramref name="launchSpeed"/>, landing
        /// <paramref name="rise"/> metres higher. Returns 0 if that height is out of reach.
        /// Uses the same gravity model as the motor (heavier while falling).
        /// </summary>
        public static float LaunchAirTime(float launchSpeed, float rise, float gravity, float fallGravityMultiplier)
        {
            float g = Math.Abs(gravity);
            if (g <= 0f || launchSpeed <= 0f)
            {
                return 0f;
            }

            float apex = launchSpeed * launchSpeed / (2f * g);
            if (rise >= apex)
            {
                return 0f;
            }

            double up = launchSpeed / g;
            double down = Math.Sqrt(2.0 * (apex - rise) / (g * Math.Max(1f, fallGravityMultiplier)));
            return (float)(up + down);
        }

        /// <summary>Height reached by a launch (metres above the take-off point).</summary>
        public static float LaunchApex(float launchSpeed, float gravity)
        {
            float g = Math.Abs(gravity);
            return g <= 0f ? 0f : launchSpeed * launchSpeed / (2f * g);
        }
    }

    /// <summary>
    /// Damped spring that drives the tilt of seesaws and the floating arena. Semi-implicit and clamped,
    /// so it cannot overshoot its limits or blow up, whatever the frame time.
    /// </summary>
    public static class TiltSolver
    {
        public const float MaxStep = 0.05f;

        /// <summary>Advances one tilt axis towards <paramref name="target"/> (degrees).</summary>
        public static void Step(ref float angle, ref float velocity, float target, float stiffness, float damping,
            float maxAngle, float maxSpeed, float dt)
        {
            if (float.IsNaN(angle) || float.IsNaN(velocity) || float.IsInfinity(angle) || float.IsInfinity(velocity))
            {
                angle = 0f;
                velocity = 0f;
            }

            if (float.IsNaN(target) || dt <= 0f)
            {
                return;
            }

            maxAngle = Math.Abs(maxAngle);
            maxSpeed = Math.Abs(maxSpeed);
            target = Math.Min(maxAngle, Math.Max(-maxAngle, target));
            float remaining = dt;
            while (remaining > 0f)
            {
                float step = Math.Min(remaining, MaxStep);
                remaining -= step;
                float acceleration = (target - angle) * Math.Max(0f, stiffness) - velocity * Math.Max(0f, damping);
                velocity += acceleration * step;
                velocity = Math.Min(maxSpeed, Math.Max(-maxSpeed, velocity));
                angle += velocity * step;
                if (angle > maxAngle)
                {
                    angle = maxAngle;
                    velocity = Math.Min(0f, velocity);
                }
                else if (angle < -maxAngle)
                {
                    angle = -maxAngle;
                    velocity = Math.Max(0f, velocity);
                }
            }
        }
    }
}
