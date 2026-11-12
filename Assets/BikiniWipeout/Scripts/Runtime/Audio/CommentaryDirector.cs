using System;
using System.Collections.Generic;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Event-driven two-announcer commentary (John, British; Alex, American). Listens to the gameplay
    /// events that already exist (knockdowns, water falls, recovery, checkpoints, finish), turns them into
    /// <see cref="CommentaryTopic"/> requests, and lets the engine-free <see cref="CommentaryScheduler"/> decide
    /// what plays. Lives on the persistent AudioManager object and re-binds itself to whichever course is loaded.
    ///
    /// Announcer volume comes from the Announcer slider; the music is ducked while an exchange is active.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class CommentaryDirector : MonoBehaviour
    {
        [SerializeField] internal CommentaryLibrary library;

        [Header("Event interpretation")]
        [Tooltip("Obstacle speed (m/s, times its force multiplier) from which a knockdown counts as a violent launch.")]
        [SerializeField, Min(0f)] internal float launchSourceSpeed = 6f;

        [Tooltip("This many wipeouts / falls within the window (without a checkpoint) counts as repeated failure.")]
        [SerializeField, Min(2)] internal int repeatedFailureCount = 3;

        [SerializeField, Min(1f)] internal float repeatedFailureWindow = 90f;

        [Tooltip("Seconds to fade a cancelled background line out.")]
        [SerializeField, Min(0.01f)] internal float interruptFadeSeconds = 0.12f;

        private readonly List<float> failureTimes = new List<float>();
        private readonly List<string> playedLog = new List<string>();
        private CommentaryScheduler scheduler;
        private List<CommentaryLibrary.ExchangeEntry> entries;
        private AudioSource johnSource;
        private AudioSource alexSource;
        private float interruptFade = 1f;
        private bool interruptFading;
        private AudioSource fadingOutSource;

        private CourseManager trackedCourse;
        private KnockdownController trackedKnockdown;
        private PlayerRespawner trackedRespawner;
        private CheckpointManager trackedCheckpoints;

        public static CommentaryDirector Instance { get; private set; }

        public CommentaryScheduler Scheduler => scheduler;

        /// <summary>Chronological log of lines started: "exchangeId:setup|response:John|Alex".</summary>
        public IReadOnlyList<string> PlayedLog => playedLog;

        public bool IsReady => scheduler != null;

        /// <summary>Raised when a line starts: exchange id, 0 = setup / 1 = response, speaker, clip.</summary>
        public event Action<string, int, CommentarySpeaker, AudioClip> LineStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            // A duplicate AudioManager object from a later scene is about to be destroyed; do not come alive on it.
            if (GetComponent<AudioManager>() != AudioManager.Instance || Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            johnSource = CreateVoice("Announcer_John");
            alexSource = CreateVoice("Announcer_Alex");
            Rebuild();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                AudioManager.SetAnnouncerSpeaking(false);
            }

            Unbind();
        }

        /// <summary>(Re)creates the scheduler from the library. Called on Awake; call again after editing the library at runtime.</summary>
        public void Rebuild()
        {
            if (library == null)
            {
                scheduler = null;
                entries = null;
                Debug.LogWarning("[CommentaryDirector] No CommentaryLibrary assigned. Commentary is disabled.", this);
                return;
            }

            StopVoices();
            playedLog.Clear();
            entries = library.exchanges;
            scheduler = new CommentaryScheduler(library.BuildSchedulerExchanges(), library.settings);
            scheduler.LineStarted += OnLineStarted;
            scheduler.ExchangeInterrupted += OnExchangeInterrupted;
            scheduler.Enabled = trackedCourse != null;
        }

        /// <summary>Asks for commentary on a topic. Used by the gameplay bindings below and available to future systems.</summary>
        public CommentaryRequestResult Request(CommentaryTopic topic, CommentaryPriority priority)
        {
            return scheduler != null ? scheduler.Request(topic, priority) : CommentaryRequestResult.Dropped;
        }

        private void Update()
        {
            if (scheduler == null)
            {
                return;
            }

            if (CourseManager.Current != trackedCourse)
            {
                Bind(CourseManager.Current);
            }

            if (trackedCourse != null)
            {
                CourseSession session = trackedCourse.Session;
                bool running = session != null && session.Phase == CoursePhase.Running && !session.IsPaused;
                bool down = trackedKnockdown != null && trackedKnockdown.IsDown;
                scheduler.AllowBackground = running && !down;
            }
            else
            {
                scheduler.AllowBackground = false;
            }

            scheduler.Tick(Time.unscaledDeltaTime);
            AudioManager.SetAnnouncerSpeaking(scheduler.IsActive);
            ApplyVolume();
        }

        // ---------------------------------------------------------------- binding to gameplay events

        private void Bind(CourseManager course)
        {
            Unbind();
            trackedCourse = course;
            failureTimes.Clear();
            StopVoices();
            scheduler.Reset();
            scheduler.Enabled = course != null;
            if (course == null)
            {
                return;
            }

            PlayerCharacter player = course.Player;
            if (player != null)
            {
                trackedKnockdown = player.Knockdown;
                trackedRespawner = player.Respawner;
                if (trackedKnockdown != null)
                {
                    trackedKnockdown.KnockedDown += OnKnockedDown;
                    trackedKnockdown.Recovered += OnRecovered;
                }

                if (trackedRespawner != null)
                {
                    trackedRespawner.FellInHazard += OnFell;
                    trackedRespawner.Respawned += OnRecovered;
                }
            }

            trackedCheckpoints = course.Checkpoints;
            if (trackedCheckpoints != null)
            {
                trackedCheckpoints.CheckpointActivated += OnCheckpoint;
            }

            course.Finished += OnFinished;
        }

        private void Unbind()
        {
            if (trackedKnockdown != null)
            {
                trackedKnockdown.KnockedDown -= OnKnockedDown;
                trackedKnockdown.Recovered -= OnRecovered;
            }

            if (trackedRespawner != null)
            {
                trackedRespawner.FellInHazard -= OnFell;
                trackedRespawner.Respawned -= OnRecovered;
            }

            if (trackedCheckpoints != null)
            {
                trackedCheckpoints.CheckpointActivated -= OnCheckpoint;
            }

            if (trackedCourse != null)
            {
                trackedCourse.Finished -= OnFinished;
            }

            trackedCourse = null;
            trackedKnockdown = null;
            trackedRespawner = null;
            trackedCheckpoints = null;
        }

        private void OnKnockedDown(ImpactData impact)
        {
            float speed = impact.SourceVelocity.magnitude * Mathf.Max(0.01f, impact.ForceMultiplier);
            CommentaryTopic topic;
            CommentaryPriority priority = CommentaryPriority.Major;
            if (speed >= launchSourceSpeed)
            {
                topic = CommentaryTopic.Launch;
            }
            else if (impact.Kind == ImpactSourceKind.Ball)
            {
                topic = CommentaryTopic.BallHit;
            }
            else if (impact.Kind == ImpactSourceKind.Sweeper)
            {
                topic = CommentaryTopic.SweeperHit;
            }
            else
            {
                topic = CommentaryTopic.HardImpact;
                priority = CommentaryPriority.Notable;
            }

            ReportFailure(topic, priority);
        }

        private void OnFell(Vector3 point)
        {
            ReportFailure(CommentaryTopic.WaterFall, CommentaryPriority.Notable);
        }

        private void ReportFailure(CommentaryTopic topic, CommentaryPriority priority)
        {
            float now = Time.unscaledTime;
            failureTimes.RemoveAll(t => now - t > repeatedFailureWindow);
            failureTimes.Add(now);

            if (failureTimes.Count >= repeatedFailureCount)
            {
                failureTimes.Clear();
                if (Request(CommentaryTopic.RepeatedFailure, CommentaryPriority.Major) != CommentaryRequestResult.Dropped)
                {
                    return;
                }
            }

            Request(topic, priority);
        }

        private void OnRecovered()
        {
            Request(CommentaryTopic.Recovery, CommentaryPriority.Routine);
        }

        private void OnCheckpoint(Checkpoint checkpoint)
        {
            failureTimes.Clear();
            Request(CommentaryTopic.Checkpoint, CommentaryPriority.Routine);
        }

        private void OnFinished(CourseResult result)
        {
            Request(CommentaryTopic.Finish, CommentaryPriority.Major);
        }

        // ---------------------------------------------------------------- playback

        private void OnLineStarted(int exchangeIndex, int step)
        {
            CommentaryLibrary.ExchangeEntry entry = entries[exchangeIndex];
            CommentaryLibrary.Line line = step == 0 ? entry.setup : entry.response;

            // Hard guarantee against overlap, whatever the scheduler thinks.
            StopVoices();
            AudioSource source = line.speaker == CommentarySpeaker.John ? johnSource : alexSource;
            source.clip = line.clip;
            source.volume = CurrentGain();
            source.Play();
            interruptFading = false;
            interruptFade = 1f;

            string log = entry.id + ":" + (step == 0 ? "setup" : "response") + ":" + line.speaker;
            playedLog.Add(log);
            if (playedLog.Count > 200)
            {
                playedLog.RemoveAt(0);
            }

            LineStarted?.Invoke(entry.id, step, line.speaker, line.clip);
        }

        private void OnExchangeInterrupted(int exchangeIndex)
        {
            // Fade the cut-off line out instead of clipping it.
            fadingOutSource = johnSource.isPlaying ? johnSource : (alexSource.isPlaying ? alexSource : null);
            interruptFading = fadingOutSource != null;
            interruptFade = 1f;
            playedLog.Add(entries[exchangeIndex].id + ":interrupted");
        }

        private void ApplyVolume()
        {
            float gain = CurrentGain();
            if (interruptFading && fadingOutSource != null)
            {
                interruptFade = Mathf.MoveTowards(interruptFade, 0f, Time.unscaledDeltaTime / interruptFadeSeconds);
                fadingOutSource.volume = gain * interruptFade;
                if (interruptFade <= 0f)
                {
                    fadingOutSource.Stop();
                    interruptFading = false;
                }

                AudioSource other = fadingOutSource == johnSource ? alexSource : johnSource;
                other.volume = gain;
                return;
            }

            johnSource.volume = gain;
            alexSource.volume = gain;
        }

        private float CurrentGain()
        {
            AudioManager manager = AudioManager.Instance;
            return manager != null && manager.Volumes != null ? manager.Volumes.AnnouncerGain : 1f;
        }

        private void StopVoices()
        {
            if (johnSource != null) johnSource.Stop();
            if (alexSource != null) alexSource.Stop();
            interruptFading = false;
        }

        private AudioSource CreateVoice(string objectName)
        {
            var voice = new GameObject(objectName);
            voice.transform.SetParent(transform, false);
            AudioSource source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
