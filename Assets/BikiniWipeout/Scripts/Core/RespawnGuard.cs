using System;

namespace BikiniWipeout.Core
{
    /// <summary>
    /// Prevents respawn loops. It (1) ignores hazards while a respawn is pending and for a short time
    /// afterwards, and (2) falls back to an earlier checkpoint when the character keeps dying
    /// immediately after respawning at the same one.
    /// </summary>
    public sealed class RespawnGuard
    {
        private readonly float hazardCooldown;
        private readonly float loopWindow;
        private readonly int loopThreshold;

        private double lastRespawnTime = double.NegativeInfinity;
        private int quickDeaths;
        private int fallbackSteps;
        private int lastActiveIndex = -1;

        /// <param name="hazardCooldown">Seconds after a respawn during which hazards are ignored.</param>
        /// <param name="loopWindow">A death this soon after a respawn counts towards a loop.</param>
        /// <param name="loopThreshold">Consecutive quick deaths before stepping back one checkpoint.</param>
        public RespawnGuard(float hazardCooldown, float loopWindow, int loopThreshold)
        {
            this.hazardCooldown = Math.Max(0f, hazardCooldown);
            this.loopWindow = Math.Max(this.hazardCooldown, loopWindow);
            this.loopThreshold = Math.Max(1, loopThreshold);
        }

        public bool IsRespawnPending { get; private set; }

        /// <summary>How many checkpoints the guard is currently stepping back by.</summary>
        public int FallbackSteps => fallbackSteps;

        public bool CanTriggerHazard(double now)
        {
            return !IsRespawnPending && now - lastRespawnTime >= hazardCooldown;
        }

        /// <summary>
        /// Call when a hazard claims the character. Returns the checkpoint index to respawn at,
        /// or -1 if the hazard must be ignored (respawn already pending / still in cooldown).
        /// </summary>
        public int BeginRespawn(double now, int activeCheckpointIndex)
        {
            if (!CanTriggerHazard(now))
            {
                return -1;
            }

            if (activeCheckpointIndex != lastActiveIndex)
            {
                // Progress was made: the loop history no longer applies.
                lastActiveIndex = activeCheckpointIndex;
                quickDeaths = 0;
                fallbackSteps = 0;
            }

            if (now - lastRespawnTime <= loopWindow)
            {
                quickDeaths++;
                if (quickDeaths >= loopThreshold)
                {
                    fallbackSteps++;
                    quickDeaths = 0;
                }
            }
            else
            {
                quickDeaths = 0;
                fallbackSteps = 0;
            }

            IsRespawnPending = true;
            return Math.Max(0, activeCheckpointIndex - fallbackSteps);
        }

        /// <summary>Call when the character has actually been placed at the checkpoint.</summary>
        public void CompleteRespawn(double now)
        {
            IsRespawnPending = false;
            lastRespawnTime = now;
        }

        public void Reset()
        {
            IsRespawnPending = false;
            lastRespawnTime = double.NegativeInfinity;
            quickDeaths = 0;
            fallbackSteps = 0;
            lastActiveIndex = -1;
        }
    }
}
