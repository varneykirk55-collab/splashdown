using System.Collections.Generic;
using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    public class CommentarySchedulerTests
    {
        private sealed class Harness
        {
            public readonly CommentaryScheduler Scheduler;
            public readonly List<string> Log = new List<string>();
            public int OverlapViolations;
            private int speakingLines;

            public Harness(IEnumerable<CommentaryExchange> exchanges, CommentarySettings settings = null)
            {
                Scheduler = new CommentaryScheduler(exchanges, settings ?? Quick(), () => 0f);
                Scheduler.LineStarted += (e, step) => Log.Add(Scheduler.GetExchange(e).Id + (step == 0 ? ":setup" : ":response") + "@" + Scheduler.Now.ToString("0.00"));
                Scheduler.ExchangeInterrupted += e => Log.Add(Scheduler.GetExchange(e).Id + ":interrupted");
            }

            public void Run(float seconds, float step = 0.05f)
            {
                for (float t = 0f; t < seconds; t += step)
                {
                    Scheduler.Tick(step);
                    speakingLines = Scheduler.IsSpeaking ? 1 : 0;
                    if (speakingLines > 1)
                    {
                        OverlapViolations++;
                    }
                }
            }
        }

        private static CommentarySettings Quick()
        {
            return new CommentarySettings
            {
                silenceBetweenExchanges = 1f,
                responseDelayMin = 0.3f,
                responseDelayMax = 0.3f,
                exchangeRepeatCooldown = 60f,
                pendingMaxAge = 5f,
                backgroundAfterQuiet = 20f,
                backgroundCooldown = 30f
            };
        }

        private static CommentaryExchange Ex(string id, float setup, float response, params CommentaryTopic[] topics)
        {
            return new CommentaryExchange(id, topics, setup, response);
        }

        [Test]
        public void Exchange_PlaysSetupThenResponse_WithConfiguredDelay()
        {
            var h = new Harness(new[] { Ex("a", 2f, 1f, CommentaryTopic.BallHit) });
            Assert.AreEqual(CommentaryRequestResult.Started, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major));
            h.Run(5f);
            Assert.AreEqual(new[] { "a:setup@0.00", "a:response@2.35" }, h.Log.ToArray());
            Assert.IsFalse(h.Scheduler.IsActive);
        }

        [Test]
        public void ResponseDelay_IsRandomisedInsideRange()
        {
            var settings = Quick();
            settings.responseDelayMin = 0.2f;
            settings.responseDelayMax = 0.8f;
            var scheduler = new CommentaryScheduler(new[] { Ex("a", 1f, 1f, CommentaryTopic.BallHit) }, settings, () => 0.5f);
            float responseAt = -1f;
            scheduler.LineStarted += (e, step) => { if (step == 1) responseAt = scheduler.Now; };
            scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            for (int i = 0; i < 100; i++) scheduler.Tick(0.05f);
            Assert.AreEqual(1.5f, responseAt, 0.06f);
        }

        [Test]
        public void MajorEvent_WhileRoutineExchangePlays_WaitsAndNeverInterrupts()
        {
            var h = new Harness(new[]
            {
                Ex("routine", 2f, 2f, CommentaryTopic.Checkpoint),
                Ex("big", 2f, 2f, CommentaryTopic.BallHit)
            });
            h.Scheduler.Request(CommentaryTopic.Checkpoint, CommentaryPriority.Routine);
            h.Run(1f);
            Assert.AreEqual(CommentaryRequestResult.Queued, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major));
            h.Run(10f);

            Assert.AreEqual(4, h.Log.Count, string.Join(",", h.Log));
            StringAssert.StartsWith("routine:setup", h.Log[0]);
            StringAssert.StartsWith("routine:response", h.Log[1]);
            StringAssert.StartsWith("big:setup", h.Log[2]);
            StringAssert.StartsWith("big:response", h.Log[3]);
        }

        [Test]
        public void InsignificantEvent_DuringPunchline_IsDropped()
        {
            var h = new Harness(new[] { Ex("a", 1f, 3f, CommentaryTopic.BallHit), Ex("b", 1f, 1f, CommentaryTopic.Recovery) });
            h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            h.Run(2f); // inside the response line
            Assert.IsTrue(h.Scheduler.IsSpeaking);
            Assert.AreEqual(CommentaryRequestResult.Dropped, h.Scheduler.Request(CommentaryTopic.Background, CommentaryPriority.Background));
            h.Run(10f);
            CollectionAssert.DoesNotContain(h.Log, "b:setup@2.00");
            Assert.AreEqual(2, h.Log.Count);
        }

        [Test]
        public void LowerPriorityQueuedEvent_DoesNotReplaceHigher()
        {
            var h = new Harness(new[] { Ex("a", 1f, 1f, CommentaryTopic.BallHit), Ex("b", 1f, 1f, CommentaryTopic.Checkpoint), Ex("c", 1f, 1f, CommentaryTopic.WaterFall) });
            h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            h.Run(0.5f);
            Assert.AreEqual(CommentaryRequestResult.Queued, h.Scheduler.Request(CommentaryTopic.WaterFall, CommentaryPriority.Notable));
            Assert.AreEqual(CommentaryRequestResult.Dropped, h.Scheduler.Request(CommentaryTopic.Checkpoint, CommentaryPriority.Routine));
            h.Run(12f);
            Assert.IsTrue(h.Log.Exists(l => l.StartsWith("c:setup")));
            Assert.IsFalse(h.Log.Exists(l => l.StartsWith("b:")));
        }

        [Test]
        public void StaleQueuedEvent_IsDropped()
        {
            var h = new Harness(new[] { Ex("a", 4f, 4f, CommentaryTopic.BallHit), Ex("b", 1f, 1f, CommentaryTopic.WaterFall) });
            h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            h.Run(0.1f);
            h.Scheduler.Request(CommentaryTopic.WaterFall, CommentaryPriority.Notable); // would start at ~9.4 s, older than 5 s
            h.Run(15f);
            Assert.IsFalse(h.Log.Exists(l => l.StartsWith("b:")));
        }

        [Test]
        public void BackgroundExchange_IsCancelledBySetupOrGap_ByMajorEvent()
        {
            var h = new Harness(new[] { Ex("chat", 2f, 2f, CommentaryTopic.Background), Ex("big", 1f, 1f, CommentaryTopic.BallHit) });
            h.Scheduler.Request(CommentaryTopic.Background, CommentaryPriority.Background);
            h.Run(0.5f);
            Assert.AreEqual(CommentaryRequestResult.Interrupted, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major));
            h.Run(5f);
            Assert.AreEqual(4, h.Log.Count, string.Join(",", h.Log));
            StringAssert.StartsWith("chat:setup", h.Log[0]);
            Assert.AreEqual("chat:interrupted", h.Log[1]);
            StringAssert.StartsWith("big:setup", h.Log[2]);
            StringAssert.StartsWith("big:response", h.Log[3]);
        }

        [Test]
        public void BackgroundPunchline_IsNeverCut()
        {
            var h = new Harness(new[] { Ex("chat", 1f, 3f, CommentaryTopic.Background), Ex("big", 1f, 1f, CommentaryTopic.BallHit) });
            h.Scheduler.Request(CommentaryTopic.Background, CommentaryPriority.Background);
            h.Run(2f); // 1.0 setup + 0.3 gap -> response is playing
            Assert.AreEqual(CommentaryRequestResult.Queued, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major));
            h.Run(10f);
            Assert.IsFalse(h.Log.Contains("chat:interrupted"));
            Assert.IsTrue(h.Log.Exists(l => l.StartsWith("big:setup")));
        }

        [Test]
        public void SameExchange_DoesNotRepeatWithinCooldown_AndMajorFallsBackToLeastRecent()
        {
            var h = new Harness(new[] { Ex("only", 1f, 1f, CommentaryTopic.BallHit) });
            h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            h.Run(20f);
            Assert.AreEqual(CommentaryRequestResult.Dropped, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Notable));
            Assert.AreEqual(CommentaryRequestResult.Started, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major));
        }

        [Test]
        public void Variety_PicksDifferentExchangesWhileOthersAreFresh()
        {
            var h = new Harness(new[]
            {
                Ex("a", 1f, 1f, CommentaryTopic.BallHit), Ex("b", 1f, 1f, CommentaryTopic.BallHit), Ex("c", 1f, 1f, CommentaryTopic.BallHit)
            });
            for (int i = 0; i < 3; i++)
            {
                h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
                h.Run(10f); // beyond the 8 s topic cooldown, inside the 60 s exchange cooldown
            }

            var setups = h.Log.FindAll(l => l.Contains(":setup"));
            Assert.AreEqual(3, setups.Count);
            Assert.AreEqual(3, new HashSet<string>(setups.ConvertAll(s => s.Split(':')[0])).Count);
        }

        [Test]
        public void TopicCooldown_BlocksRapidRepeatsOfTheSameTopic()
        {
            var h = new Harness(new[] { Ex("a", 1f, 1f, CommentaryTopic.BallHit), Ex("b", 1f, 1f, CommentaryTopic.BallHit) });
            h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            h.Run(4f);
            Assert.AreEqual(CommentaryRequestResult.Dropped, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major));
        }

        [Test]
        public void DisabledExchange_IsNeverPicked()
        {
            var disabled = new CommentaryExchange("off", new[] { CommentaryTopic.WaterFall }, 1f, 1f, 1f, false);
            var h = new Harness(new[] { disabled });
            Assert.AreEqual(CommentaryRequestResult.Dropped, h.Scheduler.Request(CommentaryTopic.WaterFall, CommentaryPriority.Major));
        }

        [Test]
        public void Background_StartsAfterQuietTime_OnlyWhenAllowed()
        {
            var h = new Harness(new[] { Ex("chat", 1f, 1f, CommentaryTopic.Background) });
            h.Run(40f);
            Assert.AreEqual(0, h.Log.Count, "Not allowed yet");

            h.Scheduler.AllowBackground = true;
            h.Run(2f);
            Assert.IsTrue(h.Log.Exists(l => l.StartsWith("chat:setup")));
        }

        [Test]
        public void Background_WaitsForQuietAfterRealActivity()
        {
            var h = new Harness(new[] { Ex("hit", 1f, 1f, CommentaryTopic.BallHit), Ex("chat", 1f, 1f, CommentaryTopic.Background) });
            h.Scheduler.AllowBackground = true;
            h.Run(15f);
            h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            h.Run(15f); // 13 s of silence after the exchange: still below the 20 s quiet threshold
            Assert.IsFalse(h.Log.Exists(l => l.StartsWith("chat:")));
            h.Run(10f);
            Assert.IsTrue(h.Log.Exists(l => l.StartsWith("chat:setup")));
        }

        [Test]
        public void SilenceBetweenExchanges_IsRespected()
        {
            var h = new Harness(new[] { Ex("a", 1f, 1f, CommentaryTopic.BallHit), Ex("b", 1f, 1f, CommentaryTopic.WaterFall) });
            h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major);
            h.Run(2.35f); // exchange ends at 2.3 s
            Assert.AreEqual(CommentaryRequestResult.Dropped, h.Scheduler.Request(CommentaryTopic.Checkpoint, CommentaryPriority.Routine), "routine chatter is not queued");
            Assert.AreEqual(CommentaryRequestResult.Queued, h.Scheduler.Request(CommentaryTopic.WaterFall, CommentaryPriority.Notable));
            h.Run(3f);
            Assert.IsTrue(h.Log.Exists(l => l.StartsWith("b:setup@3.")), string.Join(",", h.Log));
        }

        [Test]
        public void Disabled_DropsEverything()
        {
            var h = new Harness(new[] { Ex("a", 1f, 1f, CommentaryTopic.BallHit) });
            h.Scheduler.Enabled = false;
            Assert.AreEqual(CommentaryRequestResult.Dropped, h.Scheduler.Request(CommentaryTopic.BallHit, CommentaryPriority.Major));
        }

        [Test]
        public void NoOverlap_UnderEventStorm()
        {
            var exchanges = new List<CommentaryExchange>();
            foreach (CommentaryTopic topic in System.Enum.GetValues(typeof(CommentaryTopic)))
            {
                exchanges.Add(Ex("x" + topic, 1.5f, 1.5f, topic));
                exchanges.Add(Ex("y" + topic, 2f, 2f, topic));
            }

            var settings = Quick();
            settings.exchangeRepeatCooldown = 5f;
            var h = new Harness(exchanges, settings);
            var rng = new System.Random(7);
            var topics = (CommentaryTopic[])System.Enum.GetValues(typeof(CommentaryTopic));
            var priorities = (CommentaryPriority[])System.Enum.GetValues(typeof(CommentaryPriority));
            int linesStarted = 0;
            int speaking = 0;
            h.Scheduler.LineStarted += (e, s) => { linesStarted++; speaking++; if (speaking > 1) h.OverlapViolations++; };
            h.Scheduler.ExchangeInterrupted += e => speaking--;
            for (int i = 0; i < 4000; i++)
            {
                if (rng.NextDouble() < 0.03)
                {
                    h.Scheduler.Request(topics[rng.Next(topics.Length)], priorities[rng.Next(priorities.Length)]);
                }

                h.Scheduler.Tick(0.05f);
                if (!h.Scheduler.IsSpeaking && speaking > 0)
                {
                    speaking = 0;
                }
            }

            Assert.Greater(linesStarted, 20);
            Assert.AreEqual(0, h.OverlapViolations);
        }
    }

    public class VolumeSettingsAnnouncerTests
    {
        [Test]
        public void Announcer_IsIndependent_PersistsAndIsClamped()
        {
            var store = new MemoryStore();
            var volumes = new VolumeSettings { Master = 0.5f, Music = 0.2f, Sfx = 0.3f, Announcer = 0.8f };
            volumes.Save(store);

            var loaded = new VolumeSettings();
            loaded.Load(store);
            Assert.AreEqual(0.8f, loaded.Announcer, 0.0001f);
            Assert.AreEqual(0.4f, loaded.EffectiveAnnouncer, 0.0001f);
            Assert.AreEqual(0.2f, loaded.Music, 0.0001f);

            loaded.Announcer = 7f;
            Assert.AreEqual(1f, loaded.Announcer);
        }

        [Test]
        public void Announcer_DefaultsToAudibleWhenNeverSaved()
        {
            var volumes = new VolumeSettings();
            volumes.Load(new MemoryStore());
            Assert.AreEqual(VolumeSettings.DefaultAnnouncer, volumes.Announcer);
            Assert.Greater(volumes.Announcer, 0f);
        }
    }
}
