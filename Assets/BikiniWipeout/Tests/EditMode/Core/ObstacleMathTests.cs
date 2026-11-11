using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class HazardCycleTests
    {
        [Test]
        public void Cycle_RunsIdleThenWarningThenActive()
        {
            var cycle = new HazardCycle(4f, 1f, 1f, 0f);
            Assert.AreEqual(2f, cycle.IdleTime, 1e-4f);
            Assert.AreEqual(HazardPhase.Idle, cycle.Evaluate(0f, out _));
            Assert.AreEqual(HazardPhase.Idle, cycle.Evaluate(1.99f, out _));
            Assert.AreEqual(HazardPhase.Warning, cycle.Evaluate(2.01f, out float warning));
            Assert.AreEqual(0.01f, warning, 0.005f);
            Assert.AreEqual(HazardPhase.Active, cycle.Evaluate(3.5f, out float active));
            Assert.AreEqual(0.5f, active, 0.001f);
            Assert.AreEqual(HazardPhase.Idle, cycle.Evaluate(4.01f, out _));
        }

        [Test]
        public void Cycle_IsPeriodic_AndDeterministic()
        {
            var cycle = new HazardCycle(3.2f, 0.7f, 1.1f, 0.8f);
            for (float t = 0f; t < 3.2f; t += 0.13f)
            {
                HazardPhase a = cycle.Evaluate(t, out float pa);
                HazardPhase b = cycle.Evaluate(t + 3.2f * 5f, out float pb);
                Assert.AreEqual(a, b, "t=" + t);
                Assert.AreEqual(pa, pb, 0.01f);
            }
        }

        [Test]
        public void Offset_ShiftsTheCycle_AndNegativeTimeIsSafe()
        {
            var plain = new HazardCycle(4f, 1f, 1f, 0f);
            var shifted = new HazardCycle(4f, 1f, 1f, 3f);
            Assert.AreEqual(plain.Evaluate(3.2f, out _), shifted.Evaluate(0.2f, out _));
            Assert.AreEqual(HazardPhase.Active, plain.Evaluate(-0.5f, out _));
        }

        [Test]
        public void InvalidTimings_AreRepaired()
        {
            var cycle = new HazardCycle(0.1f, 2f, 3f, 0f);
            Assert.Greater(cycle.Period, 5f, "The period must be long enough to hold the warning and the active phase.");
            Assert.Greater(cycle.IdleTime, 0f);
            var noWarning = new HazardCycle(2f, 0f, 0.5f, 0f);
            Assert.AreEqual(HazardPhase.Active, noWarning.Evaluate(1.6f, out _));
        }

        [Test]
        public void ActivationIndex_AdvancesOncePerCycle()
        {
            var cycle = new HazardCycle(4f, 1f, 1f, 0f);
            Assert.AreEqual(-1, cycle.ActivationIndex(2.9f));
            Assert.AreEqual(0, cycle.ActivationIndex(3.01f));
            Assert.AreEqual(0, cycle.ActivationIndex(6.9f));
            Assert.AreEqual(1, cycle.ActivationIndex(7.01f));
        }
    }

    public class SlipMathTests
    {
        [Test]
        public void FlatGround_DoesNotSlide()
        {
            Assert.AreEqual(0f, SlipMath.SlideAcceleration(1f, 14f, 0.12f));
            Assert.AreEqual(0f, SlipMath.SlideAcceleration(1f, 14f, 0f));
        }

        [Test]
        public void SlopesBelowTheSlipAngle_Hold_AndSteeperOnesSlideFaster()
        {
            const float traction = 0.12f;
            float slip = SlipMath.SlipAngle(traction);
            Assert.AreEqual(6.84f, slip, 0.05f);

            float Cos(float degrees) => (float)System.Math.Cos(degrees * System.Math.PI / 180.0);
            Assert.AreEqual(0f, SlipMath.SlideAcceleration(Cos(slip - 1f), 14f, traction));
            float at15 = SlipMath.SlideAcceleration(Cos(15f), 14f, traction);
            float at30 = SlipMath.SlideAcceleration(Cos(30f), 14f, traction);
            float at60 = SlipMath.SlideAcceleration(Cos(60f), 14f, traction);
            Assert.Greater(at15, 0f);
            Assert.Greater(at30, at15);
            Assert.Greater(at60, at30);
            // g (sin 30 - 0.12 cos 30) = 14 * (0.5 - 0.1039)
            Assert.AreEqual(5.545f, at30, 0.01f);
        }

        [Test]
        public void MoreTraction_MeansLessSlide_AndBadInputIsHarmless()
        {
            float slick = SlipMath.SlideAcceleration(0.9f, 14f, 0.05f);
            float grippy = SlipMath.SlideAcceleration(0.9f, 14f, 0.4f);
            Assert.Greater(slick, grippy);
            Assert.AreEqual(0f, SlipMath.SlideAcceleration(float.NaN, 14f, 0.1f));
            Assert.AreEqual(14f, SlipMath.SlideAcceleration(-0.5f, 14f, 0.1f), 0.001f, "Overhangs are treated as vertical.");
            Assert.GreaterOrEqual(SlipMath.SlideAcceleration(0.5f, -14f, 0.1f), 0f, "The sign of gravity does not matter.");
        }

        [Test]
        public void Launch_MatchesHandCalculation()
        {
            // 10.3 m/s against gravity 24: apex 2.21 m, 0.429 s up; falling back 1.21 m to a pad 1 m higher at 32.4 m/s^2: 0.273 s.
            Assert.AreEqual(2.21f, SlipMath.LaunchApex(10.3f, -24f), 0.01f);
            Assert.AreEqual(0.702f, SlipMath.LaunchAirTime(10.3f, 1f, -24f, 1.35f), 0.005f);
            Assert.AreEqual(0f, SlipMath.LaunchAirTime(10.3f, 2.3f, -24f, 1.35f), "Above the apex is out of reach.");
            Assert.AreEqual(0f, SlipMath.LaunchAirTime(0f, 0f, -24f, 1.35f));
            Assert.Greater(SlipMath.LaunchAirTime(10.3f, -1f, -24f, 1.35f), SlipMath.LaunchAirTime(10.3f, 1f, -24f, 1.35f));
        }
    }

    public class TiltSolverTests
    {
        [Test]
        public void Tilt_SettlesOnItsTarget_WithoutLeavingItsLimits()
        {
            float angle = 0f, velocity = 0f;
            float peak = 0f;
            for (int i = 0; i < 400; i++)
            {
                TiltSolver.Step(ref angle, ref velocity, 9f, 14f, 5.5f, 9f, 26f, 0.02f);
                peak = System.Math.Max(peak, angle);
                Assert.LessOrEqual(System.Math.Abs(velocity), 26f + 1e-3f);
            }

            Assert.AreEqual(9f, angle, 0.05f);
            Assert.LessOrEqual(peak, 9f + 1e-4f, "The deck must never pass its limit.");
        }

        [Test]
        public void Tilt_ReturnsToLevel_WhenUnloaded()
        {
            float angle = 9f, velocity = 0f;
            for (int i = 0; i < 500; i++)
            {
                TiltSolver.Step(ref angle, ref velocity, 0f, 14f, 5.5f, 9f, 26f, 0.02f);
            }

            Assert.AreEqual(0f, angle, 0.05f);
            Assert.AreEqual(0f, velocity, 0.05f);
        }

        [Test]
        public void Tilt_IsStable_ForHugeTimeStepsAndBadState()
        {
            float angle = 0f, velocity = 0f;
            for (int i = 0; i < 50; i++)
            {
                TiltSolver.Step(ref angle, ref velocity, i % 2 == 0 ? 500f : -500f, 400f, 0f, 12f, 200f, 1.5f);
                Assert.IsFalse(float.IsNaN(angle) || float.IsInfinity(angle));
                Assert.LessOrEqual(System.Math.Abs(angle), 12f + 1e-4f);
            }

            angle = float.NaN;
            velocity = float.PositiveInfinity;
            TiltSolver.Step(ref angle, ref velocity, 3f, 14f, 5.5f, 9f, 26f, 0.02f);
            Assert.IsFalse(float.IsNaN(angle) || float.IsNaN(velocity));

            float before = angle;
            TiltSolver.Step(ref angle, ref velocity, float.NaN, 14f, 5.5f, 9f, 26f, 0.02f);
            Assert.AreEqual(before, angle, "A NaN target is ignored.");
            TiltSolver.Step(ref angle, ref velocity, 3f, 14f, 5.5f, 9f, 26f, 0f);
            Assert.AreEqual(before, angle, "No time, no movement.");
        }

        [Test]
        public void Tilt_RespectsTheSpeedLimit()
        {
            float angle = -9f, velocity = 0f;
            TiltSolver.Step(ref angle, ref velocity, 9f, 1000f, 0f, 9f, 26f, 0.5f);
            Assert.LessOrEqual(angle, -9f + 26f * 0.5f + 1e-3f);
        }
    }
}
