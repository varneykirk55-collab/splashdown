using BikiniWipeout.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>In-game HUD: timer, checkpoint progress, wipeout / fall counters, countdown and short messages.</summary>
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] internal CourseManager course;
        [SerializeField] internal Text timerText;
        [SerializeField] internal Text checkpointText;
        [SerializeField] internal Text countersText;
        [SerializeField] internal Text bestText;
        [SerializeField] internal Text countdownText;
        [SerializeField] internal Text messageText;
        [SerializeField, Min(0.1f)] internal float messageDuration = 1.6f;

        private CourseSession session;
        private long shownCentiseconds = -1;
        private float countdownHideTime = -1f;
        private float messageHideTime = -1f;

        private void Start()
        {
            if (course == null)
            {
                course = CourseManager.Current;
            }

            if (course == null)
            {
                enabled = false;
                return;
            }

            session = course.Session;
            session.CountdownSecondTick += OnCountdownSecond;
            session.PhaseChanged += OnPhaseChanged;
            session.CountersChanged += RefreshCounters;

            if (course.Checkpoints != null)
            {
                course.Checkpoints.CheckpointActivated += OnCheckpointActivated;
            }

            if (course.Player != null && course.Player.Knockdown != null)
            {
                course.Player.Knockdown.KnockedDown += OnKnockedDown;
            }

            SetText(countdownText, string.Empty);
            SetText(messageText, string.Empty);
            SetText(bestText, course.BestSeconds > 0f ? "BEST  " + TimeFormatter.Format(course.BestSeconds) : "BEST  --");
            RefreshCounters();
            RefreshCheckpoint();
            RefreshTimer(true);

            // The countdown may already have announced its first second before this HUD subscribed.
            if (session.Phase == CoursePhase.Countdown)
            {
                OnCountdownSecond(Mathf.CeilToInt(session.CountdownRemaining));
            }
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.CountdownSecondTick -= OnCountdownSecond;
                session.PhaseChanged -= OnPhaseChanged;
                session.CountersChanged -= RefreshCounters;
            }

            if (course != null && course.Checkpoints != null)
            {
                course.Checkpoints.CheckpointActivated -= OnCheckpointActivated;
            }

            if (course != null && course.Player != null && course.Player.Knockdown != null)
            {
                course.Player.Knockdown.KnockedDown -= OnKnockedDown;
            }
        }

        private void Update()
        {
            RefreshTimer(false);

            float now = Time.unscaledTime;
            if (countdownHideTime > 0f && now >= countdownHideTime)
            {
                countdownHideTime = -1f;
                SetText(countdownText, string.Empty);
            }

            if (messageHideTime > 0f && now >= messageHideTime)
            {
                messageHideTime = -1f;
                SetText(messageText, string.Empty);
            }
        }

        public void ShowMessage(string message)
        {
            SetText(messageText, message);
            messageHideTime = Time.unscaledTime + messageDuration;
        }

        private void RefreshTimer(bool force)
        {
            long centiseconds = (long)(session.ElapsedSeconds * 100f);
            if (!force && centiseconds == shownCentiseconds)
            {
                return;
            }

            shownCentiseconds = centiseconds;
            SetText(timerText, TimeFormatter.Format(session.ElapsedSeconds));
        }

        private void RefreshCounters()
        {
            SetText(countersText, "WIPEOUTS  " + session.Wipeouts + "\nFALLS  " + session.Falls);
        }

        private void RefreshCheckpoint()
        {
            CheckpointManager checkpoints = course.Checkpoints;
            if (checkpoints == null || checkpoints.Count == 0)
            {
                SetText(checkpointText, string.Empty);
                return;
            }

            // Index 0 is the start platform, so "reached" checkpoints are counted from 1.
            int total = Mathf.Max(0, checkpoints.Count - 1);
            SetText(checkpointText, "CHECKPOINT  " + checkpoints.CurrentIndex + " / " + total);
        }

        private void OnCountdownSecond(int second)
        {
            countdownHideTime = -1f;
            SetText(countdownText, second.ToString());
        }

        private void OnPhaseChanged(CoursePhase phase)
        {
            if (phase == CoursePhase.Running)
            {
                SetText(countdownText, "GO!");
                countdownHideTime = Time.unscaledTime + 0.9f;
            }
            else if (phase == CoursePhase.Finished)
            {
                SetText(countdownText, string.Empty);
                SetText(messageText, string.Empty);
                RefreshTimer(true);
            }
        }

        private void OnCheckpointActivated(Checkpoint checkpoint)
        {
            RefreshCheckpoint();
            ShowMessage("CHECKPOINT!");
        }

        private void OnKnockedDown(ImpactData impact)
        {
            ShowMessage("WIPEOUT!");
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
