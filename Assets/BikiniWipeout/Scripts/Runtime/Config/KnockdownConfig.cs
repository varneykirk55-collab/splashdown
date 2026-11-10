using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Tuning for knockdown thresholds, timing and recovery (<see cref="KnockdownController"/>).</summary>
    [CreateAssetMenu(menuName = "Bikini Wipeout/Knockdown Config", fileName = "KnockdownConfig")]
    public sealed class KnockdownConfig : ScriptableObject
    {
        [Header("Impact thresholds")]
        public ImpactThresholds impact = ImpactThresholds.Default;

        [Tooltip("Upward launch speed as a fraction of the horizontal launch speed.")]
        [Range(0f, 1.5f)] public float upwardLaunchRatio = 0.4f;

        [Tooltip("Scales the push applied by impacts that are too weak to knock the character down.")]
        [Min(0f)] public float shoveScale = 1f;

        [Header("Timing")]
        public KnockdownTimings timings = KnockdownTimings.Default;

        [Tooltip("Hips slower than this (m/s) count as settled.")]
        [Min(0f)] public float settleSpeed = 0.7f;

        [Tooltip("Knockdown immunity after a respawn.")]
        [Min(0f)] public float respawnImmunity = 1.5f;

        [Header("Safe repositioning")]
        [Tooltip("How far above the hips the ground search starts.")]
        [Min(0.1f)] public float groundProbeHeight = 1.2f;

        [Tooltip("How far below the hips ground may be and still count.")]
        [Min(0.1f)] public float groundProbeDepth = 2.0f;

        [Tooltip("Radius searched around the body for a free spot to stand up.")]
        [Min(0f)] public float safeSearchRadius = 1.6f;

        [Tooltip("Seconds between attempts when no free spot is found.")]
        [Min(0.05f)] public float recoveryRetryDelay = 0.3f;

        [Tooltip("After this many failed attempts the character is rescued to the last checkpoint.")]
        [Min(1)] public int maxRecoveryAttempts = 8;
    }
}
