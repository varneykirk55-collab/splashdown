using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class ZoomControllerTests
    {
        private static ZoomController New(float initial = 6f, float smooth = 0.1f)
        {
            return new ZoomController(2f, 12f, initial, smooth);
        }

        [Test]
        public void StartsAtInitialDistance_ClampedToRange()
        {
            Assert.AreEqual(6f, New(6f).Current);
            Assert.AreEqual(2f, New(0.5f).Current);
            Assert.AreEqual(12f, New(99f).Current);
        }

        [Test]
        public void PositiveNotches_ZoomIn_NegativeZoomOut()
        {
            ZoomController zoom = New();
            zoom.AddNotches(1f, 0.9f);
            Assert.AreEqual(5.1f, zoom.Target, 0.0001f);

            zoom.AddNotches(-2f, 0.9f);
            Assert.AreEqual(6.9f, zoom.Target, 0.0001f);
        }

        [Test]
        public void Target_NeverLeavesTheLimits()
        {
            ZoomController zoom = New();
            zoom.AddNotches(1000f, 1f);
            Assert.AreEqual(2f, zoom.Target);
            zoom.AddNotches(-1000f, 1f);
            Assert.AreEqual(12f, zoom.Target);
            zoom.AddContinuous(1f, 1000f, 1f);
            Assert.AreEqual(2f, zoom.Target);
        }

        [Test]
        public void HeldInput_MovesAtTheRequestedSpeed()
        {
            ZoomController zoom = New(8f);
            for (int i = 0; i < 50; i++)
            {
                zoom.AddContinuous(1f, 4f, 0.02f); // 1 second at 4 m/s
            }

            Assert.AreEqual(4f, zoom.Target, 0.001f);

            zoom.AddContinuous(-0.5f, 4f, 1f);
            Assert.AreEqual(6f, zoom.Target, 0.001f);
        }

        [Test]
        public void HeldInput_IsClampedToUnitAxis_AndIgnoresInvalidValues()
        {
            ZoomController zoom = New(8f);
            zoom.AddContinuous(50f, 2f, 1f);
            Assert.AreEqual(6f, zoom.Target, 0.001f);

            zoom.AddContinuous(float.NaN, 2f, 1f);
            zoom.AddNotches(float.NaN, 1f);
            zoom.AddNotches(float.PositiveInfinity, 1f);
            zoom.AddContinuous(1f, 2f, 0f);
            Assert.AreEqual(6f, zoom.Target, 0.001f);
        }

        [Test]
        public void Update_ApproachesTarget_WithoutOvershoot_AndSettles()
        {
            ZoomController zoom = New(6f, 0.15f);
            zoom.SetTarget(3f);

            float previous = zoom.Current;
            for (int i = 0; i < 300; i++)
            {
                float value = zoom.Update(1f / 60f);
                Assert.LessOrEqual(value, previous + 0.0001f, "Must move monotonically towards the target.");
                Assert.GreaterOrEqual(value, 3f - 0.0001f, "Must not overshoot.");
                previous = value;
            }

            Assert.AreEqual(3f, zoom.Current, 0.001f);
        }

        [Test]
        public void Update_IsStable_WithLargeAndTinyTimeSteps()
        {
            ZoomController zoom = New(6f, 0.12f);
            zoom.SetTarget(10f);
            zoom.Update(5f);
            Assert.That(zoom.Current, Is.InRange(6f, 10f));
            Assert.IsFalse(float.IsNaN(zoom.Current));

            zoom.Update(0.0001f);
            zoom.Update(0f);
            zoom.Update(-1f);
            Assert.IsFalse(float.IsNaN(zoom.Current));
        }

        [Test]
        public void ZeroSmoothTime_IsInstant()
        {
            ZoomController zoom = New(6f, 0f);
            zoom.SetTarget(4f);
            Assert.AreEqual(4f, zoom.Update(0.016f));
        }

        [Test]
        public void Snap_JumpsStraightToTarget()
        {
            ZoomController zoom = New();
            zoom.SetTarget(3f);
            Assert.AreEqual(6f, zoom.Current);
            zoom.Snap();
            Assert.AreEqual(3f, zoom.Current);
        }

        [Test]
        public void Configure_ReClampsCurrentAndTarget_WhenLimitsShrink()
        {
            ZoomController zoom = New(11f);
            zoom.Configure(2f, 8f, 0.1f);
            Assert.AreEqual(8f, zoom.Target);
            Assert.AreEqual(8f, zoom.Current);

            zoom.Configure(5f, 3f, 0.1f); // inverted range collapses to a single value
            Assert.AreEqual(5f, zoom.Min);
            Assert.AreEqual(5f, zoom.Max);
            Assert.AreEqual(5f, zoom.Current);
        }

        [Test]
        public void Normalized_ReportsPositionInRange()
        {
            Assert.AreEqual(0f, New(2f).Normalized, 0.0001f);
            Assert.AreEqual(1f, New(12f).Normalized, 0.0001f);
            Assert.AreEqual(0.4f, New(6f).Normalized, 0.0001f);
        }

        [TestCase(120f, 1f)]
        [TestCase(-120f, -1f)]
        [TestCase(240f, 2f)]
        [TestCase(1f, 1f)]
        [TestCase(-1f, -1f)]
        [TestCase(0.25f, 0.25f)]
        [TestCase(0f, 0f)]
        public void NormalizeScroll_AcceptsRawAndNormalizedWheelValues(float raw, float expected)
        {
            Assert.AreEqual(expected, ZoomController.NormalizeScroll(raw), 0.0001f);
        }

        [Test]
        public void NormalizeScroll_CapsAFastSpin_AndRejectsInvalidValues()
        {
            Assert.AreEqual(4f, ZoomController.NormalizeScroll(12000f));
            Assert.AreEqual(-4f, ZoomController.NormalizeScroll(-12000f));
            Assert.AreEqual(0f, ZoomController.NormalizeScroll(float.NaN));
            Assert.AreEqual(0f, ZoomController.NormalizeScroll(float.PositiveInfinity));
        }
    }
}
