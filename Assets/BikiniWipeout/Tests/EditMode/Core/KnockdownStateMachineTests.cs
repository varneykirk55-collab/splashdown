using System.Collections.Generic;
using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class KnockdownStateMachineTests
    {
        private static KnockdownTimings Timings => new KnockdownTimings
        {
            MinRagdollTime = 1f,
            MaxRagdollTime = 4f,
            SettleTime = 0.5f,
            RecoveryDuration = 1f,
            PostRecoveryImmunity = 2f
        };

        private static void Advance(KnockdownStateMachine machine, float seconds, bool settled, float step = 0.05f)
        {
            int steps = (int)System.Math.Round(seconds / step);
            for (int i = 0; i < steps; i++)
            {
                machine.Tick(step, settled);
            }
        }

        [Test]
        public void StartsNormal_AndCanBeKnockedDown()
        {
            var machine = new KnockdownStateMachine(Timings);
            Assert.AreEqual(KnockdownPhase.Normal, machine.Phase);
            Assert.IsTrue(machine.CanBeKnockedDown);
        }

        [Test]
        public void Knockdown_EntersRagdoll_AndBlocksRepeatedTriggers()
        {
            var machine = new KnockdownStateMachine(Timings);

            Assert.IsTrue(machine.TryKnockdown());
            Assert.AreEqual(KnockdownPhase.Ragdoll, machine.Phase);
            Assert.IsFalse(machine.CanBeKnockedDown);
            Assert.IsFalse(machine.TryKnockdown(), "A second hit while down must not restart the knockdown.");
        }

        [Test]
        public void Recovery_IsNotRequested_BeforeMinimumRagdollTime()
        {
            var machine = new KnockdownStateMachine(Timings);
            machine.TryKnockdown();

            Advance(machine, 0.9f, true);

            Assert.IsFalse(machine.WantsRecovery);
        }

        [Test]
        public void Recovery_IsRequested_OnceSettledAfterMinimumTime()
        {
            var machine = new KnockdownStateMachine(Timings);
            machine.TryKnockdown();

            Advance(machine, 0.8f, false);  // still tumbling
            Advance(machine, 0.3f, true);   // 1.1 s in, but only settled for 0.3 s
            Assert.IsFalse(machine.WantsRecovery);

            Advance(machine, 0.3f, true);   // settled for 0.6 s
            Assert.IsTrue(machine.WantsRecovery);
        }

        [Test]
        public void Settling_MustBeContinuous()
        {
            var machine = new KnockdownStateMachine(Timings);
            machine.TryKnockdown();
            Advance(machine, 1.2f, false);

            Advance(machine, 0.4f, true);
            Advance(machine, 0.1f, false);  // bumped again
            Advance(machine, 0.4f, true);

            Assert.IsFalse(machine.WantsRecovery);
        }

        [Test]
        public void Recovery_IsRequested_AtMaximumTime_EvenIfNeverSettled()
        {
            var machine = new KnockdownStateMachine(Timings);
            machine.TryKnockdown();

            Advance(machine, 3.8f, false);
            Assert.IsFalse(machine.WantsRecovery);

            Advance(machine, 0.3f, false);
            Assert.IsTrue(machine.WantsRecovery);
        }

        [Test]
        public void FullCycle_ReturnsToNormal_WithImmunity_ThatExpires()
        {
            var machine = new KnockdownStateMachine(Timings);
            var phases = new List<KnockdownPhase>();
            machine.PhaseChanged += phases.Add;

            machine.TryKnockdown();
            Advance(machine, 2f, true);
            Assert.IsTrue(machine.BeginRecovery());
            Assert.AreEqual(KnockdownPhase.Recovering, machine.Phase);
            Assert.IsFalse(machine.TryKnockdown(), "Cannot be knocked down while getting up.");

            Advance(machine, 1.05f, false);
            Assert.AreEqual(KnockdownPhase.Normal, machine.Phase);
            Assert.IsTrue(machine.IsImmune);
            Assert.IsFalse(machine.TryKnockdown(), "Immune right after getting up.");

            Advance(machine, 2.1f, false);
            Assert.IsFalse(machine.IsImmune);
            Assert.IsTrue(machine.TryKnockdown());

            CollectionAssert.AreEqual(
                new[] { KnockdownPhase.Ragdoll, KnockdownPhase.Recovering, KnockdownPhase.Normal, KnockdownPhase.Ragdoll },
                phases);
        }

        [Test]
        public void BeginRecovery_OnlyWorksFromRagdoll()
        {
            var machine = new KnockdownStateMachine(Timings);
            Assert.IsFalse(machine.BeginRecovery());
            Assert.AreEqual(KnockdownPhase.Normal, machine.Phase);
        }

        [Test]
        public void DeferRecovery_DelaysTheNextRequest()
        {
            var machine = new KnockdownStateMachine(Timings);
            machine.TryKnockdown();
            Advance(machine, 2f, true);
            Assert.IsTrue(machine.WantsRecovery);

            machine.DeferRecovery(0.5f);
            Assert.IsFalse(machine.WantsRecovery);

            Advance(machine, 0.3f, true);
            Assert.IsFalse(machine.WantsRecovery);

            Advance(machine, 0.3f, true);
            Assert.IsTrue(machine.WantsRecovery);
            Assert.AreEqual(KnockdownPhase.Ragdoll, machine.Phase);
        }

        [Test]
        public void ForceReset_ReturnsToNormal_WithRequestedImmunity()
        {
            var machine = new KnockdownStateMachine(Timings);
            machine.TryKnockdown();
            Advance(machine, 0.5f, false);

            machine.ForceReset(1.5f);

            Assert.AreEqual(KnockdownPhase.Normal, machine.Phase);
            Assert.AreEqual(1.5f, machine.ImmunityRemaining, 0.0001f);
            Assert.IsFalse(machine.WantsRecovery);
            Assert.IsFalse(machine.TryKnockdown());

            Advance(machine, 1.6f, false);
            Assert.IsTrue(machine.TryKnockdown());
        }

        [Test]
        public void GrantImmunity_NeverShortensExistingImmunity()
        {
            var machine = new KnockdownStateMachine(Timings);
            machine.GrantImmunity(3f);
            machine.GrantImmunity(1f);
            Assert.AreEqual(3f, machine.ImmunityRemaining, 0.0001f);
        }

        [Test]
        public void TimeInPhase_RestartsOnEveryPhaseChange()
        {
            var machine = new KnockdownStateMachine(Timings);
            Advance(machine, 1f, false);
            machine.TryKnockdown();
            Assert.AreEqual(0f, machine.TimeInPhase);

            Advance(machine, 2f, true);
            Assert.AreEqual(2f, machine.TimeInPhase, 0.001f);

            machine.BeginRecovery();
            Assert.AreEqual(0f, machine.TimeInPhase);
        }
    }
}
