using System;

namespace BikiniWipeout.Core
{
    public enum ImpactOutcome
    {
        /// <summary>Too gentle to matter.</summary>
        None,

        /// <summary>Pushes the character without taking control away.</summary>
        Shove,

        /// <summary>Full ragdoll knockdown.</summary>
        Knockdown
    }

    [Serializable]
    public struct ImpactThresholds
    {
        /// <summary>Closing speed (m/s) below which an impact is ignored.</summary>
        public float MinShoveSpeed;

        /// <summary>Closing speed (m/s) at or above which the character is knocked down.</summary>
        public float KnockdownSpeed;

        /// <summary>How much of the closing speed is turned into launch speed.</summary>
        public float VelocityTransfer;

        /// <summary>Knockdowns always launch at least this fast, so they read clearly.</summary>
        public float MinLaunchSpeed;

        /// <summary>Hard cap on launch speed. Keeps the ragdoll stable.</summary>
        public float MaxLaunchSpeed;

        public static ImpactThresholds Default => new ImpactThresholds
        {
            MinShoveSpeed = 0.6f,
            KnockdownSpeed = 2.5f,
            VelocityTransfer = 1.15f,
            MinLaunchSpeed = 4.5f,
            MaxLaunchSpeed = 13f
        };
    }

    public readonly struct ImpactResult
    {
        public ImpactResult(ImpactOutcome outcome, float launchSpeed)
        {
            Outcome = outcome;
            LaunchSpeed = launchSpeed;
        }

        public ImpactOutcome Outcome { get; }

        /// <summary>Speed (m/s) to give the character along the impact direction.</summary>
        public float LaunchSpeed { get; }
    }

    /// <summary>Turns a measured impact into a gameplay outcome. Pure math, shared by all obstacles.</summary>
    public static class ImpactEvaluator
    {
        /// <param name="closingSpeed">Speed at which obstacle and character approach each other (m/s).</param>
        /// <param name="forceMultiplier">Per-obstacle tuning value. 1 = natural, 0 = harmless.</param>
        public static ImpactResult Evaluate(float closingSpeed, float forceMultiplier, ImpactThresholds thresholds)
        {
            if (float.IsNaN(closingSpeed) || float.IsNaN(forceMultiplier) || forceMultiplier <= 0f)
            {
                return new ImpactResult(ImpactOutcome.None, 0f);
            }

            float effective = closingSpeed * forceMultiplier;
            if (effective < thresholds.MinShoveSpeed)
            {
                return new ImpactResult(ImpactOutcome.None, 0f);
            }

            float maxLaunch = Math.Max(0f, thresholds.MaxLaunchSpeed);
            float transferred = effective * Math.Max(0f, thresholds.VelocityTransfer);

            if (effective < thresholds.KnockdownSpeed)
            {
                return new ImpactResult(ImpactOutcome.Shove, Math.Min(transferred, maxLaunch));
            }

            float minLaunch = Math.Min(Math.Max(0f, thresholds.MinLaunchSpeed), maxLaunch);
            float launch = Math.Min(Math.Max(transferred, minLaunch), maxLaunch);
            return new ImpactResult(ImpactOutcome.Knockdown, launch);
        }
    }
}
