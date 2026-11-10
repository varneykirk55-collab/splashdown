using System;

namespace BikiniWipeout.Core
{
    public enum KnockdownPhase
    {
        /// <summary>Player-controlled.</summary>
        Normal,

        /// <summary>Physics owns the body.</summary>
        Ragdoll,

        /// <summary>Getting back up; input still disabled.</summary>
        Recovering
    }

    [Serializable]
    public struct KnockdownTimings
    {
        /// <summary>Ragdoll always lasts at least this long, so a knockdown is clearly visible.</summary>
        public float MinRagdollTime;

        /// <summary>Recovery is attempted after this long even if the body never settles.</summary>
        public float MaxRagdollTime;

        /// <summary>The body must stay slow for this long to count as settled.</summary>
        public float SettleTime;

        /// <summary>Length of the get-up phase.</summary>
        public float RecoveryDuration;

        /// <summary>Knockdown immunity granted after getting up, to stop chain knockdowns.</summary>
        public float PostRecoveryImmunity;

        public static KnockdownTimings Default => new KnockdownTimings
        {
            MinRagdollTime = 1.1f,
            MaxRagdollTime = 4.5f,
            SettleTime = 0.35f,
            RecoveryDuration = 0.9f,
            PostRecoveryImmunity = 1.0f
        };
    }

    /// <summary>
    /// Engine-free knockdown / recovery flow: Normal -> Ragdoll -> Recovering -> Normal (+ immunity).
    /// It owns timing and re-trigger protection only; the physical ragdoll lives in <c>RagdollController</c>.
    /// </summary>
    public sealed class KnockdownStateMachine
    {
        private KnockdownTimings timings;
        private float settledFor;
        private float recoveryHoldoff;

        public KnockdownStateMachine(KnockdownTimings timings)
        {
            this.timings = timings;
        }

        public KnockdownPhase Phase { get; private set; }
        public float TimeInPhase { get; private set; }
        public float ImmunityRemaining { get; private set; }

        public bool IsImmune => ImmunityRemaining > 0f;
        public bool CanBeKnockedDown => Phase == KnockdownPhase.Normal && !IsImmune;

        /// <summary>
        /// True once the ragdoll may hand control back: minimum time elapsed and the body has either
        /// settled or been down for the maximum time. The owner answers by calling
        /// <see cref="BeginRecovery"/> or, if no safe spot exists yet, <see cref="DeferRecovery"/>.
        /// </summary>
        public bool WantsRecovery { get; private set; }

        public event Action<KnockdownPhase> PhaseChanged;

        public void SetTimings(KnockdownTimings newTimings)
        {
            timings = newTimings;
        }

        /// <summary>Starts a knockdown. Returns false if one is in progress or the character is immune.</summary>
        public bool TryKnockdown()
        {
            if (!CanBeKnockedDown)
            {
                return false;
            }

            settledFor = 0f;
            recoveryHoldoff = 0f;
            WantsRecovery = false;
            SetPhase(KnockdownPhase.Ragdoll);
            return true;
        }

        public void Tick(float deltaTime, bool bodySettled)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            TimeInPhase += deltaTime;

            switch (Phase)
            {
                case KnockdownPhase.Normal:
                    if (ImmunityRemaining > 0f)
                    {
                        ImmunityRemaining = Math.Max(0f, ImmunityRemaining - deltaTime);
                    }

                    break;

                case KnockdownPhase.Ragdoll:
                    settledFor = bodySettled ? settledFor + deltaTime : 0f;
                    if (recoveryHoldoff > 0f)
                    {
                        recoveryHoldoff -= deltaTime;
                    }

                    bool minElapsed = TimeInPhase >= timings.MinRagdollTime;
                    bool settled = settledFor >= timings.SettleTime;
                    bool timedOut = TimeInPhase >= timings.MaxRagdollTime;
                    WantsRecovery = recoveryHoldoff <= 0f && minElapsed && (settled || timedOut);
                    break;

                case KnockdownPhase.Recovering:
                    if (TimeInPhase >= timings.RecoveryDuration)
                    {
                        ImmunityRemaining = Math.Max(ImmunityRemaining, timings.PostRecoveryImmunity);
                        SetPhase(KnockdownPhase.Normal);
                    }

                    break;
            }
        }

        /// <summary>Ragdoll -> Recovering. Returns false when not in the ragdoll phase.</summary>
        public bool BeginRecovery()
        {
            if (Phase != KnockdownPhase.Ragdoll)
            {
                return false;
            }

            WantsRecovery = false;
            SetPhase(KnockdownPhase.Recovering);
            return true;
        }

        /// <summary>No safe place to stand yet: keep the ragdoll and ask again after <paramref name="delay"/>.</summary>
        public void DeferRecovery(float delay)
        {
            if (Phase != KnockdownPhase.Ragdoll)
            {
                return;
            }

            WantsRecovery = false;
            recoveryHoldoff = Math.Max(0f, delay);
        }

        /// <summary>Used by respawns: back to Normal immediately with fresh immunity.</summary>
        public void ForceReset(float immunitySeconds)
        {
            WantsRecovery = false;
            settledFor = 0f;
            recoveryHoldoff = 0f;
            ImmunityRemaining = Math.Max(0f, immunitySeconds);
            SetPhase(KnockdownPhase.Normal);
        }

        public void GrantImmunity(float seconds)
        {
            ImmunityRemaining = Math.Max(ImmunityRemaining, seconds);
        }

        private void SetPhase(KnockdownPhase next)
        {
            TimeInPhase = 0f;
            if (Phase == next)
            {
                return;
            }

            Phase = next;
            PhaseChanged?.Invoke(next);
        }
    }
}
