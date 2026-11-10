using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class PathTimelineTests
    {
        // Two waypoints 4 m apart at 2 m/s with a 1 s wait: 1 s wait, 2 s out, 1 s wait, 2 s back = 6 s.
        private static PathTimeline TwoPointPingPong => new PathTimeline(new[] { 4f }, 0f, 2f, 1f, PathLoopMode.PingPong);

        [Test]
        public void PingPong_CycleDuration_IncludesTravelAndDwell()
        {
            Assert.AreEqual(6f, TwoPointPingPong.CycleDuration, 0.0001f);
            Assert.AreEqual(2, TwoPointPingPong.WaypointCount);
        }

        [Test]
        public void PingPong_WaitsThenTravels()
        {
            PathTimeline timeline = TwoPointPingPong;

            PathSample waiting = timeline.Evaluate(0.5f);
            Assert.AreEqual(0, waiting.FromIndex);
            Assert.AreEqual(1, waiting.ToIndex);
            Assert.AreEqual(0f, waiting.T);

            PathSample halfway = timeline.Evaluate(2f);
            Assert.AreEqual(0, halfway.FromIndex);
            Assert.AreEqual(1, halfway.ToIndex);
            Assert.AreEqual(0.5f, halfway.T, 0.0001f);
        }

        [Test]
        public void PingPong_ReturnsAlongTheSamePath()
        {
            PathTimeline timeline = TwoPointPingPong;

            PathSample waitingAtEnd = timeline.Evaluate(3.5f);
            Assert.AreEqual(1, waitingAtEnd.FromIndex);
            Assert.AreEqual(0, waitingAtEnd.ToIndex);
            Assert.AreEqual(0f, waitingAtEnd.T);

            PathSample returning = timeline.Evaluate(5.5f);
            Assert.AreEqual(1, returning.FromIndex);
            Assert.AreEqual(0, returning.ToIndex);
            Assert.AreEqual(0.75f, returning.T, 0.0001f);
        }

        [Test]
        public void Evaluate_IsPeriodic_AndDeterministic()
        {
            PathTimeline timeline = TwoPointPingPong;
            PathSample a = timeline.Evaluate(2.25f);
            PathSample b = timeline.Evaluate(2.25f + 6f * 7f);
            PathSample c = timeline.Evaluate(2.25f);

            Assert.AreEqual(a.FromIndex, b.FromIndex);
            Assert.AreEqual(a.ToIndex, b.ToIndex);
            Assert.AreEqual(a.T, b.T, 0.001f);
            Assert.AreEqual(a.T, c.T);
        }

        [Test]
        public void Evaluate_HandlesNegativeTime()
        {
            PathSample sample = TwoPointPingPong.Evaluate(-1f); // same as t = 5
            Assert.AreEqual(1, sample.FromIndex);
            Assert.AreEqual(0, sample.ToIndex);
            Assert.AreEqual(0.5f, sample.T, 0.0001f);
        }

        [Test]
        public void Loop_ClosesBackToTheFirstWaypoint()
        {
            // Triangle: 3 + 4 + 5 m at 1 m/s, no dwell = 12 s.
            var timeline = new PathTimeline(new[] { 3f, 4f }, 5f, 1f, 0f, PathLoopMode.Loop);
            Assert.AreEqual(12f, timeline.CycleDuration, 0.0001f);

            PathSample second = timeline.Evaluate(5f);
            Assert.AreEqual(1, second.FromIndex);
            Assert.AreEqual(2, second.ToIndex);
            Assert.AreEqual(0.5f, second.T, 0.0001f);

            PathSample closing = timeline.Evaluate(9.5f);
            Assert.AreEqual(2, closing.FromIndex);
            Assert.AreEqual(0, closing.ToIndex);
            Assert.AreEqual(0.5f, closing.T, 0.0001f);
        }

        [Test]
        public void MultiSegmentPingPong_VisitsWaypointsInOrderAndBack()
        {
            var timeline = new PathTimeline(new[] { 2f, 2f }, 0f, 2f, 0f, PathLoopMode.PingPong);
            Assert.AreEqual(4f, timeline.CycleDuration, 0.0001f);

            Assert.AreEqual(0, timeline.Evaluate(0.5f).FromIndex);
            Assert.AreEqual(1, timeline.Evaluate(1.5f).FromIndex);
            Assert.AreEqual(2, timeline.Evaluate(2.5f).FromIndex);
            Assert.AreEqual(1, timeline.Evaluate(2.5f).ToIndex);
            Assert.AreEqual(1, timeline.Evaluate(3.5f).FromIndex);
            Assert.AreEqual(0, timeline.Evaluate(3.5f).ToIndex);
        }

        [Test]
        public void SingleWaypoint_StaysPut()
        {
            var timeline = new PathTimeline(new float[0], 0f, 2f, 1f, PathLoopMode.PingPong);
            PathSample sample = timeline.Evaluate(123f);
            Assert.AreEqual(0, sample.FromIndex);
            Assert.AreEqual(0, sample.ToIndex);
            Assert.AreEqual(0f, sample.T);
        }

        [Test]
        public void Ease_IsClamped_Monotonic_AndSymmetric()
        {
            Assert.AreEqual(0f, PathTimeline.Ease(-1f));
            Assert.AreEqual(1f, PathTimeline.Ease(2f));
            Assert.AreEqual(0.5f, PathTimeline.Ease(0.5f), 0.0001f);

            float previous = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float value = PathTimeline.Ease(i / 20f);
                Assert.GreaterOrEqual(value, previous);
                previous = value;
            }
        }
    }
}
