using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class ImpactEvaluatorTests
    {
        private static ImpactThresholds Thresholds => new ImpactThresholds
        {
            MinShoveSpeed = 0.5f,
            KnockdownSpeed = 2.5f,
            VelocityTransfer = 1f,
            MinLaunchSpeed = 4f,
            MaxLaunchSpeed = 12f
        };

        [TestCase(0f)]
        [TestCase(0.3f)]
        [TestCase(-5f)]
        public void GentleContact_DoesNothing(float closingSpeed)
        {
            ImpactResult result = ImpactEvaluator.Evaluate(closingSpeed, 1f, Thresholds);
            Assert.AreEqual(ImpactOutcome.None, result.Outcome);
            Assert.AreEqual(0f, result.LaunchSpeed);
        }

        [Test]
        public void ModerateContact_Shoves()
        {
            ImpactResult result = ImpactEvaluator.Evaluate(1.5f, 1f, Thresholds);
            Assert.AreEqual(ImpactOutcome.Shove, result.Outcome);
            Assert.AreEqual(1.5f, result.LaunchSpeed, 0.0001f);
        }

        [Test]
        public void HardContact_KnocksDown_AtLeastAtMinimumLaunchSpeed()
        {
            ImpactResult result = ImpactEvaluator.Evaluate(2.5f, 1f, Thresholds);
            Assert.AreEqual(ImpactOutcome.Knockdown, result.Outcome);
            Assert.AreEqual(4f, result.LaunchSpeed, 0.0001f);
        }

        [Test]
        public void LaunchSpeed_ScalesWithImpact()
        {
            ImpactResult result = ImpactEvaluator.Evaluate(8f, 1f, Thresholds);
            Assert.AreEqual(ImpactOutcome.Knockdown, result.Outcome);
            Assert.AreEqual(8f, result.LaunchSpeed, 0.0001f);
        }

        [Test]
        public void LaunchSpeed_IsCapped_ForStability()
        {
            ImpactResult result = ImpactEvaluator.Evaluate(500f, 3f, Thresholds);
            Assert.AreEqual(ImpactOutcome.Knockdown, result.Outcome);
            Assert.AreEqual(12f, result.LaunchSpeed, 0.0001f);
        }

        [Test]
        public void ForceMultiplier_CanTurnAShoveIntoAKnockdown()
        {
            Assert.AreEqual(ImpactOutcome.Shove, ImpactEvaluator.Evaluate(2f, 1f, Thresholds).Outcome);
            Assert.AreEqual(ImpactOutcome.Knockdown, ImpactEvaluator.Evaluate(2f, 1.5f, Thresholds).Outcome);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void ZeroOrNegativeMultiplier_MakesObstacleHarmless(float multiplier)
        {
            Assert.AreEqual(ImpactOutcome.None, ImpactEvaluator.Evaluate(50f, multiplier, Thresholds).Outcome);
        }

        [Test]
        public void NaN_IsTreatedAsNoImpact()
        {
            Assert.AreEqual(ImpactOutcome.None, ImpactEvaluator.Evaluate(float.NaN, 1f, Thresholds).Outcome);
            Assert.AreEqual(ImpactOutcome.None, ImpactEvaluator.Evaluate(5f, float.NaN, Thresholds).Outcome);
        }

        [Test]
        public void DefaultThresholds_AreConsistent()
        {
            ImpactThresholds defaults = ImpactThresholds.Default;
            Assert.Less(defaults.MinShoveSpeed, defaults.KnockdownSpeed);
            Assert.LessOrEqual(defaults.MinLaunchSpeed, defaults.MaxLaunchSpeed);
            Assert.Greater(defaults.VelocityTransfer, 0f);
        }
    }
}
