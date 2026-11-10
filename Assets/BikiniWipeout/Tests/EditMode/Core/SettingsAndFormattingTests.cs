using System.Collections.Generic;
using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    internal sealed class MemoryStore : IKeyValueStore
    {
        public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
        public int SaveCount;

        public float GetFloat(string key, float defaultValue)
        {
            return Values.TryGetValue(key, out float value) ? value : defaultValue;
        }

        public void SetFloat(string key, float value)
        {
            Values[key] = value;
        }

        public void Save()
        {
            SaveCount++;
        }
    }

    public class VolumeSettingsTests
    {
        [Test]
        public void Defaults_AreAudible()
        {
            var volumes = new VolumeSettings();
            Assert.AreEqual(VolumeSettings.DefaultMaster, volumes.Master);
            Assert.Greater(volumes.EffectiveMusic, 0f);
            Assert.Greater(volumes.EffectiveSfx, 0f);
        }

        [Test]
        public void Values_AreClampedToZeroOne()
        {
            var volumes = new VolumeSettings { Master = 4f, Music = -2f, Sfx = float.NaN };
            Assert.AreEqual(1f, volumes.Master);
            Assert.AreEqual(0f, volumes.Music);
            Assert.AreEqual(0f, volumes.Sfx);
        }

        [Test]
        public void ChannelsAreIndependent_AndMasterScalesBoth()
        {
            var volumes = new VolumeSettings { Master = 0.5f, Music = 0.4f, Sfx = 0.8f };
            Assert.AreEqual(0.2f, volumes.EffectiveMusic, 0.0001f);
            Assert.AreEqual(0.4f, volumes.EffectiveSfx, 0.0001f);
            Assert.AreEqual(0.4f, volumes.MusicGain, 0.0001f);
            Assert.AreEqual(0.8f, volumes.SfxGain, 0.0001f);
        }

        [Test]
        public void Changed_FiresOnlyWhenAValueActuallyChanges()
        {
            var volumes = new VolumeSettings();
            int changes = 0;
            volumes.Changed += () => changes++;

            volumes.Music = 0.3f;
            volumes.Music = 0.3f;
            volumes.Sfx = 0.1f;

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var store = new MemoryStore();
            var saved = new VolumeSettings { Master = 0.7f, Music = 0.2f, Sfx = 0.55f };
            saved.Save(store);
            Assert.AreEqual(1, store.SaveCount);

            var loaded = new VolumeSettings();
            loaded.Load(store);
            Assert.AreEqual(0.7f, loaded.Master, 0.0001f);
            Assert.AreEqual(0.2f, loaded.Music, 0.0001f);
            Assert.AreEqual(0.55f, loaded.Sfx, 0.0001f);
        }

        [Test]
        public void Load_FromEmptyStore_KeepsDefaults_AndClampsCorruptValues()
        {
            var volumes = new VolumeSettings();
            volumes.Load(new MemoryStore());
            Assert.AreEqual(VolumeSettings.DefaultMusic, volumes.Music);

            var corrupt = new MemoryStore();
            corrupt.Values[VolumeSettings.MasterKey] = 37f;
            volumes.Load(corrupt);
            Assert.AreEqual(1f, volumes.Master);
        }

        [Test]
        public void LinearToDecibels_MatchesKnownPoints()
        {
            Assert.AreEqual(0f, VolumeSettings.LinearToDecibels(1f), 0.001f);
            Assert.AreEqual(-6.02f, VolumeSettings.LinearToDecibels(0.5f), 0.01f);
            Assert.AreEqual(-80f, VolumeSettings.LinearToDecibels(0f));
        }
    }

    public class TimeFormatterTests
    {
        [TestCase(0f, "0:00.00")]
        [TestCase(7.5f, "0:07.50")]
        [TestCase(67.42f, "1:07.42")]
        [TestCase(59.999f, "1:00.00")]
        [TestCase(600f, "10:00.00")]
        [TestCase(3599.5f, "59:59.50")]
        public void Format_ProducesMinutesSecondsCentiseconds(float seconds, string expected)
        {
            Assert.AreEqual(expected, TimeFormatter.Format(seconds));
        }

        [Test]
        public void Format_TreatsInvalidInputAsZero()
        {
            Assert.AreEqual("0:00.00", TimeFormatter.Format(-3f));
            Assert.AreEqual("0:00.00", TimeFormatter.Format(float.NaN));
            Assert.AreEqual("0:00.00", TimeFormatter.Format(float.PositiveInfinity));
        }
    }

    public class BestTimeRecordTests
    {
        [Test]
        public void FirstRun_IsABest()
        {
            var record = new BestTimeRecord(new MemoryStore(), "course");
            Assert.IsFalse(record.HasRecord);
            Assert.IsTrue(record.Submit(42.5f));
            Assert.IsTrue(record.HasRecord);
            Assert.AreEqual(42.5f, record.BestSeconds, 0.0001f);
        }

        [Test]
        public void OnlyFasterRuns_ReplaceTheBest()
        {
            var record = new BestTimeRecord(new MemoryStore(), "course");
            record.Submit(40f);

            Assert.IsFalse(record.Submit(45f));
            Assert.IsFalse(record.Submit(40f));
            Assert.IsTrue(record.Submit(38.2f));
            Assert.AreEqual(38.2f, record.BestSeconds, 0.0001f);
        }

        [Test]
        public void InvalidTimes_AreRejected()
        {
            var record = new BestTimeRecord(new MemoryStore(), "course");
            Assert.IsFalse(record.Submit(0f));
            Assert.IsFalse(record.Submit(-1f));
            Assert.IsFalse(record.Submit(float.NaN));
            Assert.IsFalse(record.HasRecord);
        }

        [Test]
        public void Courses_HaveSeparateRecords()
        {
            var store = new MemoryStore();
            new BestTimeRecord(store, "a").Submit(10f);
            Assert.IsFalse(new BestTimeRecord(store, "b").HasRecord);
        }
    }

    public class MovementMathTests
    {
        [Test]
        public void Approach_StepsTowardsTarget_WithoutOvershooting()
        {
            Assert.AreEqual(2f, MovementMath.Approach(0f, 10f, 2f));
            Assert.AreEqual(-2f, MovementMath.Approach(0f, -10f, 2f));
            Assert.AreEqual(10f, MovementMath.Approach(9.5f, 10f, 2f));
            Assert.AreEqual(3f, MovementMath.Approach(3f, 10f, 0f));
        }

        [Test]
        public void JumpSpeed_ReachesTheRequestedHeight()
        {
            const float gravity = -24f;
            const float height = 1.4f;
            float speed = MovementMath.JumpSpeedForHeight(height, gravity);

            // Peak height of a ballistic jump: v^2 / (2 g)
            Assert.AreEqual(height, speed * speed / (2f * 24f), 0.0001f);
            Assert.AreEqual(0f, MovementMath.JumpSpeedForHeight(0f, gravity));
        }

        [Test]
        public void GraceTimer_StaysActiveForItsDuration()
        {
            var timer = new GraceTimer();
            Assert.IsFalse(timer.IsActive);

            timer.Start(0.12f);
            timer.Tick(0.1f);
            Assert.IsTrue(timer.IsActive);

            timer.Tick(0.05f);
            Assert.IsFalse(timer.IsActive);

            timer.Start(1f);
            timer.Clear();
            Assert.IsFalse(timer.IsActive);
        }
    }
}
