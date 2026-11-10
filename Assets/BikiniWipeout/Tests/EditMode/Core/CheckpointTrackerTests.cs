using System;
using System.Collections.Generic;
using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class CheckpointTrackerTests
    {
        [Test]
        public void StartsAtIndexZero()
        {
            var tracker = new CheckpointTracker(3);
            Assert.AreEqual(0, tracker.CurrentIndex);
            Assert.AreEqual(3, tracker.Count);
        }

        [Test]
        public void RequiresAtLeastOneCheckpoint()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CheckpointTracker(0));
        }

        [Test]
        public void Activate_MovesForward_AndRaisesEvent()
        {
            var tracker = new CheckpointTracker(3);
            var activated = new List<int>();
            tracker.Activated += activated.Add;

            Assert.IsTrue(tracker.TryActivate(1));
            Assert.IsTrue(tracker.TryActivate(2));

            Assert.AreEqual(2, tracker.CurrentIndex);
            CollectionAssert.AreEqual(new[] { 1, 2 }, activated);
        }

        [Test]
        public void Activate_NeverMovesBackwards_OrRepeats()
        {
            var tracker = new CheckpointTracker(4);
            tracker.TryActivate(2);

            Assert.IsFalse(tracker.TryActivate(1));
            Assert.IsFalse(tracker.TryActivate(2));
            Assert.IsFalse(tracker.TryActivate(0));
            Assert.AreEqual(2, tracker.CurrentIndex);
        }

        [Test]
        public void Activate_CanSkipAhead()
        {
            var tracker = new CheckpointTracker(4);
            Assert.IsTrue(tracker.TryActivate(3));
            Assert.AreEqual(3, tracker.CurrentIndex);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(99)]
        public void Activate_RejectsOutOfRange(int index)
        {
            var tracker = new CheckpointTracker(3);
            Assert.IsFalse(tracker.TryActivate(index));
            Assert.AreEqual(0, tracker.CurrentIndex);
        }

        [Test]
        public void Reset_ReturnsToStart()
        {
            var tracker = new CheckpointTracker(3);
            tracker.TryActivate(2);
            tracker.Reset();
            Assert.AreEqual(0, tracker.CurrentIndex);
            Assert.IsTrue(tracker.TryActivate(1));
        }
    }
}
