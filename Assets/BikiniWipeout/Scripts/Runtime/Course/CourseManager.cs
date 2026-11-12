using System;
using BikiniWipeout.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BikiniWipeout
{
    public readonly struct CourseResult
    {
        public CourseResult(float timeSeconds, int wipeouts, int falls, float bestSeconds, bool isNewBest)
        {
            TimeSeconds = timeSeconds;
            Wipeouts = wipeouts;
            Falls = falls;
            BestSeconds = bestSeconds;
            IsNewBest = isNewBest;
        }

        public float TimeSeconds { get; }
        public int Wipeouts { get; }
        public int Falls { get; }
        public float BestSeconds { get; }
        public bool IsNewBest { get; }
    }

    /// <summary>
    /// Runs one attempt at a course: countdown, timer, pause, counters, finish, restart and returning
    /// to the menu. Rules and timing live in the engine-free <see cref="CourseSession"/>; this class
    /// connects it to the scene (player, checkpoints, audio, cursor, time scale).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class CourseManager : MonoBehaviour
    {
        [Tooltip("Identifies this course for best-time storage.")]
        [SerializeField] internal string courseId = "foundation1";

        [SerializeField, Min(0f)] internal float countdownSeconds = 3f;
        [SerializeField] internal PlayerCharacter player;
        [SerializeField] internal CheckpointManager checkpoints;
        [SerializeField] internal ThirdPersonCamera gameCamera;
        [SerializeField] internal MusicTrackId music = MusicTrackId.Course;

        [Tooltip("Lock and hide the mouse cursor during play.")]
        [SerializeField] internal bool manageCursor = true;

        private BestTimeRecord bestTime;
        private bool subscribed;
        private bool sceneTransition;

        /// <summary>The course in the active scene, if any.</summary>
        public static CourseManager Current { get; private set; }

        public CourseSession Session { get; private set; }
        public CourseResult LastResult { get; private set; }
        public PlayerCharacter Player => player;
        public CheckpointManager Checkpoints => checkpoints;
        public float BestSeconds => bestTime != null ? bestTime.BestSeconds : 0f;

        /// <summary>Raised when the finish line is crossed.</summary>
        public event Action<CourseResult> Finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        private void Awake()
        {
            Current = this;
            GraphicsSettingsController.ApplySaved();
            Time.timeScale = 1f;
            Session = new CourseSession(countdownSeconds);
            bestTime = new BestTimeRecord(new PlayerPrefsStore(), courseId);
            Session.PhaseChanged += OnPhaseChanged;
            Session.PauseChanged += OnPauseChanged;
            Session.CountdownSecondTick += OnCountdownSecond;
        }

        private void Start()
        {
            if (player != null)
            {
                if (player.Knockdown != null)
                {
                    player.Knockdown.KnockedDown += OnPlayerKnockedDown;
                }

                if (player.Respawner != null)
                {
                    player.Respawner.FellInHazard += OnPlayerFell;
                }
            }

            if (checkpoints != null)
            {
                checkpoints.CheckpointActivated += OnCheckpointActivated;
            }

            InputSystem.onDeviceChange += OnDeviceChange;
            subscribed = true;
            ApplyControlState();
            AudioManager.PlayMusic(music);
            Session.BeginCountdown();
        }

        private void OnDestroy()
        {
            if (subscribed)
            {
                InputSystem.onDeviceChange -= OnDeviceChange;

                if (player != null && player.Knockdown != null)
                {
                    player.Knockdown.KnockedDown -= OnPlayerKnockedDown;
                }

                if (player != null && player.Respawner != null)
                {
                    player.Respawner.FellInHazard -= OnPlayerFell;
                }

                if (checkpoints != null)
                {
                    checkpoints.CheckpointActivated -= OnCheckpointActivated;
                }
            }

            if (Current == this)
            {
                Current = null;
                // Never leave the game frozen or the cursor captured when the scene goes away.
                Time.timeScale = 1f;
                SetCursorCaptured(false);
            }
        }

        private void Update()
        {
            ICharacterInput input = player != null ? player.Input : null;
            if (input != null && input.PausePressedThisFrame)
            {
                PauseMenuController pauseMenu = Session.IsPaused ? FindFirstObjectByType<PauseMenuController>() : null;
                if (pauseMenu == null || !pauseMenu.TryCloseSettings()) TogglePause();
            }

            // Scaled time: the session stands still while the game is paused (timeScale 0).
            Session.Tick(Time.deltaTime);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyControlState();
            }
        }

        public void TogglePause()
        {
            SetPaused(!Session.IsPaused);
        }

        public void SetPaused(bool paused)
        {
            Session.SetPaused(paused);
        }

        /// <summary>Restarts the course from the beginning by reloading the scene.</summary>
        public void RestartCourse()
        {
            if (sceneTransition) return;
            sceneTransition = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            if (sceneTransition || !Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu)) return;
            sceneTransition = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        /// <summary>Called by the <see cref="FinishLine"/>.</summary>
        public void NotifyFinishReached()
        {
            if (!Session.TryFinish())
            {
                return;
            }

            float time = Session.ElapsedSeconds;
            bool isNewBest = bestTime.Submit(time);
            LastResult = new CourseResult(time, Session.Wipeouts, Session.Falls, bestTime.BestSeconds, isNewBest);
            AudioManager.Play(AudioEventId.FinishCelebration);
            AudioManager.PlayMusic(MusicTrackId.Results);
            Finished?.Invoke(LastResult);
        }

        private void OnPhaseChanged(CoursePhase phase)
        {
            if (phase == CoursePhase.Running)
            {
                AudioManager.Play(AudioEventId.CountdownGo);
            }

            ApplyControlState();
        }

        private void OnPauseChanged(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
            ApplyControlState();
        }

        /// <summary>If the controller being played with is unplugged mid-run, pause instead of leaving the player stranded.</summary>
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            bool lost = change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed;
            if (lost && device is Gamepad && InputDeviceTracker.UsingGamepad && !Session.IsPaused &&
                (Session.Phase == CoursePhase.Countdown || Session.Phase == CoursePhase.Running))
            {
                SetPaused(true);
            }
        }

        private void OnCountdownSecond(int second)
        {
            AudioManager.Play(AudioEventId.CountdownTick);
        }

        private void OnPlayerKnockedDown(ImpactData impact)
        {
            Session.RegisterWipeout();
        }

        private void OnPlayerFell(Vector3 point)
        {
            Session.RegisterFall();
        }

        private void OnCheckpointActivated(Checkpoint checkpoint)
        {
            AudioManager.Play(AudioEventId.CheckpointActivated, checkpoint.SpawnPosition);
        }

        /// <summary>Applies steering lock, camera look and cursor capture for the current session state.</summary>
        private void ApplyControlState()
        {
            bool playing = Session.IsPlayerControlAllowed;
            if (player != null && player.Motor != null)
            {
                player.Motor.SetControlLock(ControlLock.Course, !playing);
            }

            // The camera may be turned during the countdown, but not in menus.
            bool inMenu = Session.IsPaused || Session.Phase == CoursePhase.Finished;
            if (gameCamera != null)
            {
                gameCamera.LookEnabled = !inMenu;
            }

            SetCursorCaptured(!inMenu);
        }

        private void SetCursorCaptured(bool captured)
        {
            if (!manageCursor)
            {
                return;
            }

            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
