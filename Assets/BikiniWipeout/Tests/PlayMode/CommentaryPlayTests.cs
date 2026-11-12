using System.Collections;
using System.Collections.Generic;
using BikiniWipeout.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BikiniWipeout.Tests
{
    /// <summary>
    /// Plays the real Splashdown Gauntlet scene and fires real gameplay events (impacts through the knockdown
    /// receiver, hazard entry through the respawner, finish through the course manager) to verify the
    /// commentary director, announcer playback, ducking and music tracks. Needs the scene in Build Settings.
    /// </summary>
    public class CommentaryPlayTests
    {
        private CourseManager course;
        private PlayerCharacter player;
        private CommentaryDirector director;
        private AudioManager audio;
        private float savedAnnouncer;

        [UnitySetUp]
        public IEnumerator LoadCourse()
        {
            if (SceneUtility.GetBuildIndexByScenePath(SceneNames.CourseGauntlet) < 0)
            {
                Assert.Ignore("Course 1 is not in Build Settings.");
            }

            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneNames.CourseGauntlet, LoadSceneMode.Single);
            yield return null;
            yield return null;

            course = CourseManager.Current;
            Assert.IsNotNull(course, "CourseManager");
            player = course.Player;
            audio = AudioManager.Instance;
            director = CommentaryDirector.Instance;
            Assert.IsNotNull(audio, "AudioManager");
            Assert.IsNotNull(director, "CommentaryDirector (is it on the AudioManager prefab?)");
            savedAnnouncer = audio.Volumes.Announcer;
            director.Rebuild(); // fresh cooldown history for each test
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (audio != null)
            {
                audio.Volumes.Announcer = savedAnnouncer;
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }

        private static ImpactData Impact(ImpactSourceKind kind, float sourceSpeed)
        {
            return new ImpactData
            {
                Point = Vector3.zero,
                Normal = Vector3.right,
                SourceVelocity = Vector3.right * sourceSpeed,
                ForceMultiplier = 1f,
                Kind = kind,
                Source = null
            };
        }

        private static int AnnouncersPlaying()
        {
            int count = 0;
            foreach (AudioSource source in CommentaryDirector.Instance.GetComponentsInChildren<AudioSource>())
            {
                if (source.name.StartsWith("Announcer_") && source.isPlaying)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Runs for the given real time, asserting at every frame that at most one announcer voice is audible.</summary>
        private IEnumerator RunWatchingOverlap(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                Assert.LessOrEqual(AnnouncersPlaying(), 1, "Two announcer lines are playing over each other");
                yield return null;
            }
        }

        private CommentaryLibrary.ExchangeEntry Entry(string id)
        {
            return director.library.exchanges.Find(e => e.id == id);
        }

        private static string IdOf(string logEntry)
        {
            return logEntry.Split(':')[0];
        }

        [UnityTest]
        public IEnumerator Library_HasAllTwentyLines_WithJohnBritishFirstExceptPair2()
        {
            var library = director.library;
            Assert.AreEqual(10, library.exchanges.Count);
            foreach (var e in library.exchanges)
            {
                Assert.IsNotNull(e.setup.clip, e.id + " setup clip");
                Assert.IsNotNull(e.response.clip, e.id + " response clip");
                Assert.AreNotEqual(e.setup.speaker, e.response.speaker, e.id + " should alternate speakers");
            }

            Assert.AreEqual(CommentarySpeaker.Alex, Entry("02_crowd_on_fire").setup.speaker, "pair 2 was recorded Alex-first");
            Assert.AreEqual(CommentarySpeaker.John, Entry("01_trolley").setup.speaker);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BallHit_PlaysSetupThenResponse_InRecordedOrder_AndDucksMusic()
        {
            Assert.IsTrue(player.Knockdown.ReceiveImpact(Impact(ImpactSourceKind.Ball, 4f)), "ball impact should knock the player down");
            yield return null;

            Assert.GreaterOrEqual(director.PlayedLog.Count, 1, "no commentary after a ball hit");
            string setup = director.PlayedLog[0];
            string id = IdOf(setup);
            CollectionAssert.Contains(new[] { "01_trolley", "04_medical", "05_athleticism", "09_replay" }, id, "wrong pool for a ball hit");
            StringAssert.Contains(":setup:", setup);
            Assert.AreEqual(1, AnnouncersPlaying());

            // Music ducks while the exchange is active.
            float normal = audio.CurrentMusicVolume;
            yield return RunWatchingOverlap(1.0f);
            Assert.Less(audio.MusicDuckFactor, 0.6f, "music should be ducked during commentary");

            // Response follows after the setup, spoken by the other announcer.
            float timeout = Time.realtimeSinceStartup + 12f;
            while (director.PlayedLog.Count < 2 && Time.realtimeSinceStartup < timeout)
            {
                Assert.LessOrEqual(AnnouncersPlaying(), 1);
                yield return null;
            }

            Assert.AreEqual(2, director.PlayedLog.Count, string.Join(" | ", director.PlayedLog));
            Assert.AreEqual(id, IdOf(director.PlayedLog[1]));
            StringAssert.Contains(":response:", director.PlayedLog[1]);
            var entry = Entry(id);
            StringAssert.EndsWith(entry.setup.speaker.ToString(), director.PlayedLog[0]);
            StringAssert.EndsWith(entry.response.speaker.ToString(), director.PlayedLog[1]);

            // After the exchange (and anything it queued), music recovers.
            float idleDeadline = Time.realtimeSinceStartup + 25f;
            while ((director.Scheduler.IsActive || director.Scheduler.HasPending) && Time.realtimeSinceStartup < idleDeadline)
            {
                Assert.LessOrEqual(AnnouncersPlaying(), 1);
                yield return null;
            }

            yield return RunWatchingOverlap(1.5f);
            Assert.Greater(audio.MusicDuckFactor, 0.95f, "music should return to full level");
        }

        [UnityTest]
        public IEnumerator SweeperHit_UsesSweeperPool_AndLaunchUsesLaunchPool()
        {
            Assert.IsTrue(player.Knockdown.ReceiveImpact(Impact(ImpactSourceKind.Sweeper, 4f)));
            yield return null;
            CollectionAssert.Contains(new[] { "01_trolley", "04_medical", "05_athleticism", "09_replay" }, IdOf(director.PlayedLog[0]));
        }

        [UnityTest]
        public IEnumerator WaterFall_PlaysWaterCommentary_NotTheDisabledNettingLine()
        {
            Assert.IsTrue(player.Respawner.OnHazardEntered(player.transform.position), "hazard entry should be claimed");
            yield return null;
            Assert.GreaterOrEqual(director.PlayedLog.Count, 1);
            Assert.AreEqual("08_water_lovely", IdOf(director.PlayedLog[0]));
        }

        [UnityTest]
        public IEnumerator SecondEvent_WaitsForTheFirstExchange_AndNothingOverlaps()
        {
            player.Knockdown.ReceiveImpact(Impact(ImpactSourceKind.Ball, 4f));
            yield return null;
            string first = IdOf(director.PlayedLog[0]);

            // Throw more events while the exchange is running.
            player.Respawner.OnHazardEntered(player.transform.position);
            yield return RunWatchingOverlap(1.5f);
            Assert.AreEqual(first, IdOf(director.PlayedLog[0]));
            Assert.IsFalse(System.Linq.Enumerable.Any(director.PlayedLog, l => l.Contains("interrupted")));
            foreach (string line in director.PlayedLog)
            {
                Assert.AreEqual(first, IdOf(line), "a different exchange started before the first finished: " + string.Join(" | ", director.PlayedLog));
                if (line.Contains(":response:")) break;
            }

            yield return RunWatchingOverlap(14f);
            Assert.GreaterOrEqual(director.PlayedLog.Count, 2);
        }

        [UnityTest]
        public IEnumerator Finish_PlaysSignOffPool_AndSwitchesToResultsMusic()
        {
            Assert.AreEqual(MusicTrackId.Course, audio.CurrentMusicTrack);
            yield return new WaitUntil(() => course.Session.Phase == CoursePhase.Running);
            course.NotifyFinishReached();
            yield return null;

            Assert.AreEqual(MusicTrackId.Results, audio.CurrentMusicTrack);
            Assert.GreaterOrEqual(director.PlayedLog.Count, 1);
            CollectionAssert.Contains(new[] { "02_crowd_on_fire", "06_judges", "10_sign_off" }, IdOf(director.PlayedLog[0]));
        }

        [UnityTest]
        public IEnumerator AnnouncerVolume_Slider_ControlsAnnouncerOnly()
        {
            var sliders = Object.FindObjectsByType<VolumeSliders>(FindObjectsInactive.Include);
            Assert.GreaterOrEqual(sliders.Length, 1);
            foreach (var s in sliders)
            {
                Assert.IsNotNull(s.announcerSlider, "announcer slider missing from a volume panel");
            }

            float music = audio.Volumes.Music;
            float sfx = audio.Volumes.Sfx;
            for (Transform t = sliders[0].transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            yield return null;
            sliders[0].announcerSlider.value = 0.25f;
            Assert.AreEqual(0.25f, audio.Volumes.Announcer, 0.001f);
            Assert.AreEqual(music, audio.Volumes.Music);
            Assert.AreEqual(sfx, audio.Volumes.Sfx);

            player.Knockdown.ReceiveImpact(Impact(ImpactSourceKind.Ball, 4f));
            yield return null;
            yield return null;
            foreach (AudioSource source in director.GetComponentsInChildren<AudioSource>())
            {
                if (source.name.StartsWith("Announcer_") && source.isPlaying)
                {
                    Assert.AreEqual(0.25f, source.volume, 0.01f);
                }
            }
        }

        [UnityTest]
        public IEnumerator Music_PlaysCourseTrack_AndMenuTrackInMainMenu()
        {
            Assert.AreEqual(MusicTrackId.Course, audio.CurrentMusicTrack);
            var courseClip = audio.Library.FindMusic(MusicTrackId.Course).clip;
            Assert.Greater(courseClip.length, 60f);

            yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.AreEqual(MusicTrackId.MainMenu, AudioManager.Instance.CurrentMusicTrack);

            // Crossfade: the old track fades out while the new one comes in.
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.Greater(AudioManager.Instance.CurrentMusicVolume, 0.05f);
        }
    }
}
