using System;
using System.Collections.Generic;

namespace BikiniWipeout.Core
{
    /// <summary>What just happened in the run, from the commentators' point of view.</summary>
    public enum CommentaryTopic
    {
        /// <summary>Knockdown with a violent launch (fast obstacle).</summary>
        Launch = 0,
        BallHit = 1,
        SweeperHit = 2,
        /// <summary>Any other knockdown (moving platform, scripted).</summary>
        HardImpact = 3,
        WaterFall = 4,
        /// <summary>Back on her feet after a wipeout or a respawn.</summary>
        Recovery = 5,
        RepeatedFailure = 6,
        Checkpoint = 7,
        Finish = 8,
        /// <summary>Filler during quiet gameplay.</summary>
        Background = 9
    }

    /// <summary>Higher priorities are never dropped in favour of lower ones and can wait in the queue.</summary>
    public enum CommentaryPriority
    {
        Background = 0,
        Routine = 1,
        Notable = 2,
        Major = 3
    }

    public enum CommentarySpeaker
    {
        John = 0,
        Alex = 1
    }

    public enum CommentaryRequestResult
    {
        /// <summary>Started immediately.</summary>
        Started,
        /// <summary>Waiting for the current exchange (or the gap after it) to finish.</summary>
        Queued,
        /// <summary>Cut a background exchange short and started immediately.</summary>
        Interrupted,
        /// <summary>Ignored: cooldown, insignificant while busy, nothing suitable, or disabled.</summary>
        Dropped
    }

    /// <summary>One recorded two-line conversation (setup, then response). Engine-free description.</summary>
    public sealed class CommentaryExchange
    {
        public CommentaryExchange(string id, IReadOnlyList<CommentaryTopic> topics, float setupSeconds, float responseSeconds,
            float weight = 1f, bool enabled = true)
        {
            Id = id;
            Topics = topics;
            SetupSeconds = Math.Max(0.01f, setupSeconds);
            ResponseSeconds = Math.Max(0.01f, responseSeconds);
            Weight = Math.Max(0.0001f, weight);
            Enabled = enabled;
        }

        public string Id { get; }
        public IReadOnlyList<CommentaryTopic> Topics { get; }
        public float SetupSeconds { get; }
        public float ResponseSeconds { get; }
        public float Weight { get; }
        public bool Enabled { get; }

        public bool Handles(CommentaryTopic topic)
        {
            for (int i = 0; i < Topics.Count; i++)
            {
                if (Topics[i] == topic)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Tunable timing and repetition rules. All values are seconds.</summary>
    [Serializable]
    public sealed class CommentarySettings
    {
        /// <summary>Silence after an exchange before the next one may start.</summary>
        public float silenceBetweenExchanges = 1.2f;

        /// <summary>Pause between the setup line and the response line (random in this range).</summary>
        public float responseDelayMin = 0.25f;
        public float responseDelayMax = 0.5f;

        /// <summary>The same exchange will not repeat within this time (unless nothing else fits a Major event).</summary>
        public float exchangeRepeatCooldown = 90f;

        /// <summary>A queued event that has waited longer than this is stale and dropped.</summary>
        public float pendingMaxAge = 5f;

        /// <summary>Quiet gameplay time before background banter is allowed.</summary>
        public float backgroundAfterQuiet = 35f;

        public float backgroundCooldown = 70f;

        /// <summary>Default minimum time between two exchanges about the same topic.</summary>
        public float defaultTopicCooldown = 8f;

        public float TopicCooldown(CommentaryTopic topic)
        {
            switch (topic)
            {
                case CommentaryTopic.Launch: return 6f;
                case CommentaryTopic.BallHit: return 8f;
                case CommentaryTopic.SweeperHit: return 8f;
                case CommentaryTopic.HardImpact: return 10f;
                case CommentaryTopic.WaterFall: return 12f;
                case CommentaryTopic.Recovery: return 25f;
                case CommentaryTopic.RepeatedFailure: return 30f;
                case CommentaryTopic.Checkpoint: return 25f;
                case CommentaryTopic.Finish: return 0f;
                case CommentaryTopic.Background: return backgroundCooldown;
                default: return defaultTopicCooldown;
            }
        }
    }

    /// <summary>
    /// Engine-free brain of the Commentary Director. Decides which recorded exchange plays, when, and what
    /// happens when events collide. Never has two lines playing at once.
    ///
    /// Rules:
    /// - An exchange is a setup line followed (after a short configurable delay) by the response line.
    /// - A new event while an exchange is running is never allowed to cut it off, except that a Background
    ///   exchange can be cancelled before its response line by a Notable or Major event.
    /// - Otherwise a Notable or Major event waits in a single pending slot (highest priority wins, stale after
    ///   <see cref="CommentarySettings.pendingMaxAge"/>); Routine and Background events are simply dropped.
    /// - Exchanges obey per-topic and per-exchange cooldowns. A Major event may reuse the least recently
    ///   played fitting exchange when every fitting exchange is cooling down.
    /// </summary>
    public sealed class CommentaryScheduler
    {
        private enum Phase
        {
            Idle,
            Setup,
            Gap,
            Response
        }

        private readonly List<CommentaryExchange> exchanges;
        private readonly CommentarySettings settings;
        private readonly Func<float> random01;
        private readonly Dictionary<CommentaryTopic, float> topicLastStart = new Dictionary<CommentaryTopic, float>();
        private readonly float[] exchangeLastStart;

        private Phase phase = Phase.Idle;
        private float phaseRemaining;
        private float now;
        private float lastActivity;
        private float lastEnd = float.NegativeInfinity;
        private int currentExchange = -1;
        private CommentaryPriority currentPriority;
        private bool hasPending;
        private CommentaryTopic pendingTopic;
        private CommentaryPriority pendingPriority;
        private float pendingAge;

        public CommentaryScheduler(IEnumerable<CommentaryExchange> exchanges, CommentarySettings settings = null, Func<float> random01 = null)
        {
            this.exchanges = new List<CommentaryExchange>(exchanges ?? new CommentaryExchange[0]);
            this.settings = settings ?? new CommentarySettings();
            if (random01 == null)
            {
                var rng = new Random();
                random01 = () => (float)rng.NextDouble();
            }

            this.random01 = random01;

            exchangeLastStart = new float[this.exchanges.Count];
            for (int i = 0; i < exchangeLastStart.Length; i++)
            {
                exchangeLastStart[i] = float.NegativeInfinity;
            }
        }

        /// <summary>The scheduler's own clock (sum of the deltas passed to <see cref="Tick"/>).</summary>
        public float Now => now;

        /// <summary>True from the start of the setup line until the end of the response line.</summary>
        public bool IsActive => phase != Phase.Idle;

        /// <summary>True only while a line is actually being spoken (not in the gap between lines).</summary>
        public bool IsSpeaking => phase == Phase.Setup || phase == Phase.Response;

        public bool HasPending => hasPending;
        public int CurrentExchangeIndex => currentExchange;
        public CommentaryPriority CurrentPriority => currentPriority;
        public string CurrentExchangeId => currentExchange >= 0 ? exchanges[currentExchange].Id : null;

        /// <summary>When false, no new background banter starts (set false during countdown, menus, finish).</summary>
        public bool AllowBackground { get; set; }

        /// <summary>When false every request is dropped.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>A line starts. Arguments: exchange index, 0 = setup / 1 = response.</summary>
        public event Action<int, int> LineStarted;

        /// <summary>The current exchange was cancelled before finishing (background only).</summary>
        public event Action<int> ExchangeInterrupted;

        public event Action<int> ExchangeFinished;

        public CommentaryExchange GetExchange(int index) => exchanges[index];

        public CommentaryRequestResult Request(CommentaryTopic topic, CommentaryPriority priority)
        {
            if (!Enabled)
            {
                return CommentaryRequestResult.Dropped;
            }

            if (priority != CommentaryPriority.Background)
            {
                lastActivity = now;
            }

            // Topic cooldown. Major events about a different thing are never muted by it, but the same
            // topic twice in a few seconds is still repetition.
            if (topicLastStart.TryGetValue(topic, out float topicLast) && now - topicLast < settings.TopicCooldown(topic))
            {
                return CommentaryRequestResult.Dropped;
            }

            if (phase == Phase.Idle)
            {
                bool silenceElapsed = now - lastEnd >= settings.silenceBetweenExchanges;
                if (silenceElapsed || priority == CommentaryPriority.Major && now - lastEnd >= settings.silenceBetweenExchanges * 0.25f)
                {
                    return TryStart(topic, priority) ? CommentaryRequestResult.Started : CommentaryRequestResult.Dropped;
                }

                return Enqueue(topic, priority);
            }

            // Busy. Only a background chat can be cancelled, and never during its response (the punchline).
            if (currentPriority == CommentaryPriority.Background && phase != Phase.Response && priority >= CommentaryPriority.Notable)
            {
                int cancelled = currentExchange;
                phase = Phase.Idle;
                currentExchange = -1;
                ExchangeInterrupted?.Invoke(cancelled);
                if (TryStart(topic, priority))
                {
                    return CommentaryRequestResult.Interrupted;
                }

                lastEnd = now;
                return CommentaryRequestResult.Dropped;
            }

            return Enqueue(topic, priority);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            now += deltaTime;

            if (hasPending)
            {
                pendingAge += deltaTime;
                if (pendingAge > settings.pendingMaxAge)
                {
                    hasPending = false;
                }
            }

            float remaining = deltaTime;
            while (phase != Phase.Idle)
            {
                if (phaseRemaining > remaining)
                {
                    phaseRemaining -= remaining;
                    return;
                }

                remaining -= phaseRemaining;
                AdvancePhase();
            }

            if (hasPending && now - lastEnd >= settings.silenceBetweenExchanges)
            {
                hasPending = false;
                TryStart(pendingTopic, pendingPriority);
                return;
            }

            if (AllowBackground && !hasPending && now - Math.Max(lastActivity, lastEnd) >= settings.backgroundAfterQuiet)
            {
                lastActivity = now;
                Request(CommentaryTopic.Background, CommentaryPriority.Background);
            }
        }

        /// <summary>Clears the queue and silences the scheduler's state (scene change). Keeps repetition history.</summary>
        public void Reset()
        {
            phase = Phase.Idle;
            currentExchange = -1;
            hasPending = false;
            lastActivity = now;
            lastEnd = now;
        }

        private CommentaryRequestResult Enqueue(CommentaryTopic topic, CommentaryPriority priority)
        {
            // Only Notable and Major events are worth waiting for; routine chatter is simply skipped when busy.
            if (priority < CommentaryPriority.Notable)
            {
                return CommentaryRequestResult.Dropped;
            }

            if (hasPending && pendingPriority >= priority)
            {
                return CommentaryRequestResult.Dropped;
            }

            hasPending = true;
            pendingTopic = topic;
            pendingPriority = priority;
            pendingAge = 0f;
            return CommentaryRequestResult.Queued;
        }

        private void AdvancePhase()
        {
            switch (phase)
            {
                case Phase.Setup:
                    phase = Phase.Gap;
                    phaseRemaining = settings.responseDelayMin + (settings.responseDelayMax - settings.responseDelayMin) * random01();
                    break;

                case Phase.Gap:
                    phase = Phase.Response;
                    phaseRemaining = exchanges[currentExchange].ResponseSeconds;
                    LineStarted?.Invoke(currentExchange, 1);
                    break;

                case Phase.Response:
                    int finished = currentExchange;
                    phase = Phase.Idle;
                    currentExchange = -1;
                    lastEnd = now;
                    ExchangeFinished?.Invoke(finished);
                    break;
            }
        }

        private bool TryStart(CommentaryTopic topic, CommentaryPriority priority)
        {
            int index = Pick(topic, priority);
            if (index < 0)
            {
                return false;
            }

            currentExchange = index;
            currentPriority = priority;
            phase = Phase.Setup;
            phaseRemaining = exchanges[index].SetupSeconds;
            topicLastStart[topic] = now;
            exchangeLastStart[index] = now;
            lastActivity = now;
            LineStarted?.Invoke(index, 0);
            return true;
        }

        private int Pick(CommentaryTopic topic, CommentaryPriority priority)
        {
            var fresh = new List<int>();
            int leastRecent = -1;
            for (int i = 0; i < exchanges.Count; i++)
            {
                if (!exchanges[i].Enabled || !exchanges[i].Handles(topic))
                {
                    continue;
                }

                if (now - exchangeLastStart[i] >= settings.exchangeRepeatCooldown)
                {
                    fresh.Add(i);
                }

                if (leastRecent < 0 || exchangeLastStart[i] < exchangeLastStart[leastRecent])
                {
                    leastRecent = i;
                }
            }

            if (fresh.Count == 0)
            {
                return priority == CommentaryPriority.Major ? leastRecent : -1;
            }

            float total = 0f;
            foreach (int i in fresh)
            {
                total += exchanges[i].Weight;
            }

            float roll = random01() * total;
            foreach (int i in fresh)
            {
                roll -= exchanges[i].Weight;
                if (roll <= 0f)
                {
                    return i;
                }
            }

            return fresh[fresh.Count - 1];
        }
    }
}
