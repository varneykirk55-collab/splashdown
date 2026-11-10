using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class RespawnGuardTests
    {
        private static RespawnGuard NewGuard()
        {
            return new RespawnGuard(1f, 2.5f, 3);
        }

        [Test]
        public void FirstHazard_RespawnsAtActiveCheckpoint()
        {
            RespawnGuard guard = NewGuard();
            Assert.IsTrue(guard.CanTriggerHazard(10));
            Assert.AreEqual(2, guard.BeginRespawn(10, 2));
            Assert.IsTrue(guard.IsRespawnPending);
        }

        [Test]
        public void Hazards_AreIgnored_WhileRespawnIsPending()
        {
            RespawnGuard guard = NewGuard();
            guard.BeginRespawn(10, 1);

            Assert.IsFalse(guard.CanTriggerHazard(10.2));
            Assert.AreEqual(-1, guard.BeginRespawn(10.2, 1));
        }

        [Test]
        public void Hazards_AreIgnored_DuringCooldownAfterRespawn()
        {
            RespawnGuard guard = NewGuard();
            guard.BeginRespawn(10, 1);
            guard.CompleteRespawn(11);

            Assert.IsFalse(guard.CanTriggerHazard(11.5));
            Assert.AreEqual(-1, guard.BeginRespawn(11.5, 1));
            Assert.IsTrue(guard.CanTriggerHazard(12.01));
        }

        [Test]
        public void RepeatedQuickDeaths_FallBackToPreviousCheckpoint()
        {
            RespawnGuard guard = NewGuard();
            double now = 0;
            guard.BeginRespawn(now, 2);
            guard.CompleteRespawn(now += 1);

            // Three deaths, each 1.5 s after respawning (inside the 2.5 s loop window).
            Assert.AreEqual(2, guard.BeginRespawn(now += 1.5, 2));
            guard.CompleteRespawn(now += 1);
            Assert.AreEqual(2, guard.BeginRespawn(now += 1.5, 2));
            guard.CompleteRespawn(now += 1);
            Assert.AreEqual(1, guard.BeginRespawn(now += 1.5, 2), "Third quick death steps back one checkpoint.");
            Assert.AreEqual(1, guard.FallbackSteps);
        }

        [Test]
        public void Fallback_NeverGoesBelowTheStart()
        {
            RespawnGuard guard = NewGuard();
            double now = 0;
            guard.BeginRespawn(now, 0);
            guard.CompleteRespawn(now += 1);

            for (int i = 0; i < 12; i++)
            {
                int index = guard.BeginRespawn(now += 1.2, 0);
                Assert.AreEqual(0, index);
                guard.CompleteRespawn(now += 1);
            }
        }

        [Test]
        public void SlowDeath_ClearsLoopHistory()
        {
            RespawnGuard guard = NewGuard();
            double now = 0;
            guard.BeginRespawn(now, 2);
            guard.CompleteRespawn(now += 1);
            guard.BeginRespawn(now += 1.5, 2);
            guard.CompleteRespawn(now += 1);
            guard.BeginRespawn(now += 1.5, 2);
            guard.CompleteRespawn(now += 1);

            // Survives for a while this time.
            Assert.AreEqual(2, guard.BeginRespawn(now += 20, 2));
            Assert.AreEqual(0, guard.FallbackSteps);
            guard.CompleteRespawn(now += 1);

            // The quick-death counter started again from zero.
            Assert.AreEqual(2, guard.BeginRespawn(now += 1.5, 2));
        }

        [Test]
        public void ReachingANewCheckpoint_ClearsFallback()
        {
            RespawnGuard guard = NewGuard();
            double now = 0;
            guard.BeginRespawn(now, 1);
            guard.CompleteRespawn(now += 1);
            for (int i = 0; i < 3; i++)
            {
                guard.BeginRespawn(now += 1.5, 1);
                guard.CompleteRespawn(now += 1);
            }

            Assert.AreEqual(1, guard.FallbackSteps);

            Assert.AreEqual(2, guard.BeginRespawn(now += 1.5, 2), "A newly reached checkpoint is used as-is.");
            Assert.AreEqual(0, guard.FallbackSteps);
        }

        [Test]
        public void Reset_ForgetsEverything()
        {
            RespawnGuard guard = NewGuard();
            guard.BeginRespawn(5, 1);
            guard.Reset();

            Assert.IsFalse(guard.IsRespawnPending);
            Assert.IsTrue(guard.CanTriggerHazard(5));
            Assert.AreEqual(1, guard.BeginRespawn(5, 1));
        }
    }
}
