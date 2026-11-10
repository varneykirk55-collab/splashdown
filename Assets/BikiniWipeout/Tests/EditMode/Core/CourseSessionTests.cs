using System.Collections.Generic;
using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class CourseSessionTests
    {
        [Test]
        public void BeginCountdown_EntersCountdown_AndAnnouncesFirstSecond()
        {
            var session = new CourseSession(3f);
            var ticks = new List<int>();
            session.CountdownSecondTick += ticks.Add;

            session.BeginCountdown();

            Assert.AreEqual(CoursePhase.Countdown, session.Phase);
            Assert.AreEqual(3f, session.CountdownRemaining, 0.0001f);
            CollectionAssert.AreEqual(new[] { 3 }, ticks);
            Assert.IsFalse(session.IsPlayerControlAllowed);
        }

        [Test]
        public void Countdown_AnnouncesEachSecondOnce_ThenStartsRunning()
        {
            var session = new CourseSession(3f);
            var ticks = new List<int>();
            var phases = new List<CoursePhase>();
            session.CountdownSecondTick += ticks.Add;
            session.PhaseChanged += phases.Add;
            session.BeginCountdown();

            for (int i = 0; i < 400 && session.Phase == CoursePhase.Countdown; i++)
            {
                session.Tick(0.01f);
            }

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, ticks);
            CollectionAssert.AreEqual(new[] { CoursePhase.Countdown, CoursePhase.Running }, phases);
            Assert.AreEqual(0f, session.CountdownRemaining);
            Assert.IsTrue(session.IsPlayerControlAllowed);
        }

        [Test]
        public void Timer_DoesNotRunDuringCountdown_AndCarriesOvershootIntoRun()
        {
            var session = new CourseSession(1f);
            session.BeginCountdown();

            session.Tick(0.4f);
            Assert.AreEqual(0f, session.ElapsedSeconds);

            session.Tick(0.85f); // 0.25 s past the end of the countdown
            Assert.AreEqual(CoursePhase.Running, session.Phase);
            Assert.AreEqual(0.25f, session.ElapsedSeconds, 0.0001f);

            session.Tick(1.5f);
            Assert.AreEqual(1.75f, session.ElapsedSeconds, 0.0001f);
        }

        [Test]
        public void ZeroCountdown_StartsRunningImmediately()
        {
            var session = new CourseSession(0f);
            session.BeginCountdown();
            Assert.AreEqual(CoursePhase.Running, session.Phase);
        }

        [Test]
        public void BeginCountdown_Twice_IsIgnored()
        {
            var session = new CourseSession(0f);
            session.BeginCountdown();
            session.Tick(2f);
            session.BeginCountdown();
            Assert.AreEqual(CoursePhase.Running, session.Phase);
            Assert.AreEqual(2f, session.ElapsedSeconds, 0.0001f);
        }

        [Test]
        public void Pause_FreezesTimer_AndBlocksControl()
        {
            var session = new CourseSession(0f);
            session.BeginCountdown();
            session.Tick(1f);

            Assert.IsTrue(session.SetPaused(true));
            session.Tick(5f);
            Assert.AreEqual(1f, session.ElapsedSeconds, 0.0001f);
            Assert.IsFalse(session.IsPlayerControlAllowed);

            Assert.IsTrue(session.SetPaused(false));
            session.Tick(0.5f);
            Assert.AreEqual(1.5f, session.ElapsedSeconds, 0.0001f);
            Assert.IsTrue(session.IsPlayerControlAllowed);
        }

        [Test]
        public void Pause_FreezesCountdown()
        {
            var session = new CourseSession(3f);
            session.BeginCountdown();
            session.SetPaused(true);
            session.Tick(10f);
            Assert.AreEqual(CoursePhase.Countdown, session.Phase);
            Assert.AreEqual(3f, session.CountdownRemaining, 0.0001f);
        }

        [Test]
        public void Pause_IsRejected_BeforeStartAndAfterFinish()
        {
            var session = new CourseSession(0f);
            Assert.IsFalse(session.SetPaused(true));

            session.BeginCountdown();
            session.TryFinish();
            Assert.IsFalse(session.SetPaused(true));
            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void PauseChanged_FiresOnlyOnRealChanges()
        {
            var session = new CourseSession(0f);
            session.BeginCountdown();
            int changes = 0;
            session.PauseChanged += _ => changes++;

            session.SetPaused(true);
            session.SetPaused(true);
            session.SetPaused(false);
            session.SetPaused(false);

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void TryFinish_StopsTimer_AndOnlyWorksWhileRunning()
        {
            var session = new CourseSession(1f);
            Assert.IsFalse(session.TryFinish());

            session.BeginCountdown();
            Assert.IsFalse(session.TryFinish(), "Cannot finish during the countdown.");

            session.Tick(1f);
            session.Tick(12.5f);
            Assert.IsTrue(session.TryFinish());
            Assert.AreEqual(CoursePhase.Finished, session.Phase);

            session.Tick(30f);
            Assert.AreEqual(12.5f, session.ElapsedSeconds, 0.0001f);
            Assert.IsFalse(session.TryFinish(), "A run can only be finished once.");
            Assert.IsFalse(session.IsPlayerControlAllowed);
        }

        [Test]
        public void Counters_OnlyCountDuringTheRun()
        {
            var session = new CourseSession(1f);
            int events = 0;
            session.CountersChanged += () => events++;

            session.BeginCountdown();
            session.RegisterWipeout();
            session.RegisterFall();
            Assert.AreEqual(0, session.Wipeouts);
            Assert.AreEqual(0, session.Falls);

            session.Tick(1.1f);
            session.RegisterWipeout();
            session.RegisterWipeout();
            session.RegisterFall();
            Assert.AreEqual(2, session.Wipeouts);
            Assert.AreEqual(1, session.Falls);
            Assert.AreEqual(3, events);

            session.TryFinish();
            session.RegisterWipeout();
            session.RegisterFall();
            Assert.AreEqual(2, session.Wipeouts);
            Assert.AreEqual(1, session.Falls);
        }
    }
}
