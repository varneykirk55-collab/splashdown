using System;

namespace BikiniWipeout.Core
{
    public enum CoursePhase
    {
        NotStarted,
        Countdown,
        Running,
        Finished
    }

    /// <summary>
    /// Engine-free state machine for one run of a course: countdown, timer, pause, counters, finish.
    /// Driven by <c>CourseManager</c> at runtime and by plain unit tests.
    /// </summary>
    public sealed class CourseSession
    {
        private readonly float countdownSeconds;
        private int lastAnnouncedSecond;

        public CourseSession(float countdownSeconds)
        {
            this.countdownSeconds = Math.Max(0f, countdownSeconds);
            Phase = CoursePhase.NotStarted;
        }

        public CoursePhase Phase { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>Seconds left before the run starts. Zero outside the countdown phase.</summary>
        public float CountdownRemaining { get; private set; }

        /// <summary>Run time. Frozen once the course is finished.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>Knockdowns caused by obstacles.</summary>
        public int Wipeouts { get; private set; }

        /// <summary>Falls into a hazard (water) that forced a respawn.</summary>
        public int Falls { get; private set; }

        /// <summary>True while the contestant is allowed to steer the character.</summary>
        public bool IsPlayerControlAllowed => Phase == CoursePhase.Running && !IsPaused;

        public event Action<CoursePhase> PhaseChanged;

        /// <summary>Raised once per whole second of countdown (3, 2, 1).</summary>
        public event Action<int> CountdownSecondTick;

        public event Action<bool> PauseChanged;
        public event Action CountersChanged;

        public void BeginCountdown()
        {
            if (Phase != CoursePhase.NotStarted)
            {
                return;
            }

            CountdownRemaining = countdownSeconds;
            ElapsedSeconds = 0f;
            lastAnnouncedSecond = int.MaxValue;

            if (countdownSeconds <= 0f)
            {
                SetPhase(CoursePhase.Running);
                return;
            }

            SetPhase(CoursePhase.Countdown);
            AnnounceCountdownSecond();
        }

        /// <summary>Advances countdown/timer. Does nothing while paused.</summary>
        public void Tick(float deltaTime)
        {
            if (IsPaused || deltaTime <= 0f)
            {
                return;
            }

            switch (Phase)
            {
                case CoursePhase.Countdown:
                    CountdownRemaining -= deltaTime;
                    if (CountdownRemaining <= 0f)
                    {
                        // Carry the overshoot into the run so the timer does not lose a partial frame.
                        float overshoot = -CountdownRemaining;
                        CountdownRemaining = 0f;
                        SetPhase(CoursePhase.Running);
                        ElapsedSeconds = overshoot;
                    }
                    else
                    {
                        AnnounceCountdownSecond();
                    }

                    break;

                case CoursePhase.Running:
                    ElapsedSeconds += deltaTime;
                    break;
            }
        }

        /// <summary>Stops the timer. Returns false if the course is not currently running.</summary>
        public bool TryFinish()
        {
            if (Phase != CoursePhase.Running)
            {
                return false;
            }

            SetPhase(CoursePhase.Finished);
            return true;
        }

        /// <summary>Pausing is only meaningful during countdown or the run itself.</summary>
        public bool SetPaused(bool paused)
        {
            if (paused == IsPaused)
            {
                return false;
            }

            if (paused && Phase != CoursePhase.Countdown && Phase != CoursePhase.Running)
            {
                return false;
            }

            IsPaused = paused;
            PauseChanged?.Invoke(paused);
            return true;
        }

        public void RegisterWipeout()
        {
            if (Phase != CoursePhase.Running)
            {
                return;
            }

            Wipeouts++;
            CountersChanged?.Invoke();
        }

        public void RegisterFall()
        {
            if (Phase != CoursePhase.Running)
            {
                return;
            }

            Falls++;
            CountersChanged?.Invoke();
        }

        private void AnnounceCountdownSecond()
        {
            int second = (int)Math.Ceiling(CountdownRemaining);
            if (second < lastAnnouncedSecond && second > 0)
            {
                lastAnnouncedSecond = second;
                CountdownSecondTick?.Invoke(second);
            }
        }

        private void SetPhase(CoursePhase next)
        {
            if (Phase == next)
            {
                return;
            }

            Phase = next;
            PhaseChanged?.Invoke(next);
        }
    }
}
