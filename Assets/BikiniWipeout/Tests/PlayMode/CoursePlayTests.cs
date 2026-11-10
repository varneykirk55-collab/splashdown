using System.Collections;
using BikiniWipeout.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BikiniWipeout.Tests
{
    /// <summary>Scripted stand-in for the keyboard and mouse.</summary>
    internal sealed class ScriptedInput : ICharacterInput
    {
        public Vector2 Move { get; set; }
        public Vector2 Look { get; set; }
        public bool LookIsPointerDelta => true;
        public float Zoom { get; set; }
        public bool ZoomIsScroll { get; set; }
        public bool RunHeld { get; set; }
        public bool JumpHeld { get; set; }
        public bool JumpPressedThisFrame { get; set; }
        public bool PausePressedThisFrame { get; set; }
    }

    /// <summary>
    /// End-to-end checks that load the generated course scene and drive it with scripted input.
    /// They need the generated content: run "Bikini Wipeout > Build Foundation 1 Content" first
    /// (the scene must be in Build Settings).
    /// </summary>
    public class CoursePlayTests
    {
        private CourseManager course;
        private PlayerCharacter player;
        private ScriptedInput input;

        [UnitySetUp]
        public IEnumerator LoadCourse()
        {
            if (SceneUtility.GetBuildIndexByScenePath(SceneNames.CourseFoundation1) < 0)
            {
                Assert.Ignore("Course scene is not in Build Settings. Run: Bikini Wipeout > Build Foundation 1 Content.");
            }

            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneNames.CourseFoundation1, LoadSceneMode.Single);
            yield return null;

            course = CourseManager.Current;
            Assert.IsNotNull(course, "CourseManager");
            player = course.Player;
            Assert.IsNotNull(player, "Player");

            input = new ScriptedInput();
            player.Motor.InputSource = input;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("Timed out after " + timeoutSeconds + " s waiting for: " + what);
                }

                yield return null;
            }
        }

        private IEnumerator WaitForRunStart()
        {
            yield return WaitUntil(() => course.Session.Phase == CoursePhase.Running, 8f, "countdown to finish");
            yield return WaitUntil(() => player.Motor.IsGrounded, 3f, "player to stand on the start platform");
        }

        private IEnumerator PressJump()
        {
            input.JumpPressedThisFrame = true;
            input.JumpHeld = true;
            yield return null;
            input.JumpPressedThisFrame = false;
        }

        // ------------------------------------------------------------------ scene and flow

        [UnityTest]
        public IEnumerator Scene_Loads_WithEverythingWired()
        {
            Assert.IsNotNull(course.Checkpoints, "checkpoints");
            Assert.AreEqual(3, course.Checkpoints.Count);
            Assert.IsNotNull(Camera.main, "main camera");
            Assert.IsNotNull(Camera.main.GetComponent<ThirdPersonCamera>(), "third-person camera");
            Assert.IsNotNull(player.Motor);
            Assert.IsNotNull(player.Knockdown);
            Assert.IsNotNull(player.Ragdoll);
            Assert.IsNotNull(player.Respawner);
            Assert.AreEqual(1, Object.FindObjectsByType<RotatingSweeper>().Length);
            Assert.AreEqual(3, Object.FindObjectsByType<BouncingBall>().Length);
            Assert.AreEqual(3, Object.FindObjectsByType<MovingPlatform>().Length);
            Assert.IsNotNull(Object.FindAnyObjectByType<WaterHazard>());
            Assert.IsNotNull(Object.FindAnyObjectByType<FinishLine>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator AudioSystem_Initializes_AndHooksFire()
        {
            Assert.IsNotNull(AudioManager.Instance, "AudioManager instance");
            Assert.IsTrue(AudioManager.Instance.IsInitialized);
            Assert.IsNotNull(AudioManager.Instance.Library, "audio library");
            Assert.IsNotNull(AudioManager.Instance.Volumes);

            int before = AudioManager.GetRequestCount(AudioEventId.CountdownGo);
            yield return WaitForRunStart();
            Assert.Greater(AudioManager.GetRequestCount(AudioEventId.CountdownGo), before, "countdown 'go' hook");
            Assert.AreEqual(MusicTrackId.Course, AudioManager.Instance.CurrentMusicTrack);
        }

        [UnityTest]
        public IEnumerator Countdown_LocksControl_ThenTimerRuns()
        {
            Assert.AreEqual(CoursePhase.Countdown, course.Session.Phase);
            Assert.IsFalse(player.Motor.ControlEnabled, "No steering during the countdown.");
            Assert.AreEqual(0f, course.Session.ElapsedSeconds);

            yield return WaitForRunStart();
            Assert.IsTrue(player.Motor.ControlEnabled);

            float t0 = course.Session.ElapsedSeconds;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(course.Session.ElapsedSeconds, t0 + 0.3f);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTheGame_AndResumeContinues()
        {
            yield return WaitForRunStart();

            course.SetPaused(true);
            Assert.AreEqual(0f, Time.timeScale);
            float frozen = course.Session.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(frozen, course.Session.ElapsedSeconds, 0.0001f);
            Assert.IsFalse(player.Motor.ControlEnabled);

            course.SetPaused(false);
            Assert.AreEqual(1f, Time.timeScale);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(course.Session.ElapsedSeconds, frozen);
            Assert.IsTrue(player.Motor.ControlEnabled);
        }

        // ------------------------------------------------------------------ movement and camera

        [UnityTest]
        public IEnumerator Player_Walks_Runs_AndCameraFollows()
        {
            yield return WaitForRunStart();
            Vector3 start = player.transform.position;

            input.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(1f);
            float walked = player.transform.position.z - start.z;
            Assert.Greater(walked, 1.5f, "Should walk forward (camera-relative +Z).");
            Assert.Less(walked, 4.5f, "Walking should be slower than running.");

            Vector3 mid = player.transform.position;
            input.RunHeld = true;
            yield return new WaitForSeconds(0.8f);
            float ran = player.transform.position.z - mid.z;
            Assert.Greater(ran, walked * 0.8f * 1.3f, "Running should cover clearly more ground per second.");

            input.Move = Vector2.zero;
            input.RunHeld = false;
            yield return new WaitForSeconds(0.6f);
            Assert.Less(player.Motor.PlanarSpeed, 0.2f, "Should decelerate to a stop.");

            Transform cam = Camera.main.transform;
            float distance = Vector3.Distance(cam.position, player.FocusPosition);
            Assert.That(distance, Is.InRange(1f, 9f), "Camera should stay near the player.");
            Assert.Greater(Vector3.Dot(cam.forward, (player.FocusPosition - cam.position).normalized), 0.8f,
                "Camera should be looking at the player.");
        }

        [UnityTest]
        public IEnumerator Player_Jumps_AndLands()
        {
            yield return WaitForRunStart();
            float groundY = player.transform.position.y;

            yield return PressJump();
            float peak = groundY;
            float deadline = Time.time + 2f;
            bool leftGround = false;
            while (Time.time < deadline)
            {
                peak = Mathf.Max(peak, player.transform.position.y);
                leftGround |= !player.Motor.IsGrounded;
                if (leftGround && player.Motor.IsGrounded)
                {
                    break;
                }

                yield return null;
            }

            input.JumpHeld = false;
            Assert.IsTrue(leftGround, "Player should leave the ground.");
            Assert.Greater(peak - groundY, 0.9f, "Jump height");
            Assert.IsTrue(player.Motor.IsGrounded, "Player should land again.");
            Assert.AreEqual(groundY, player.transform.position.y, 0.1f);
        }

        [UnityTest]
        public IEnumerator MovingPlatform_CarriesThePlayer()
        {
            yield return WaitForRunStart();
            MovingPlatform platform = null;
            foreach (MovingPlatform candidate in Object.FindObjectsByType<MovingPlatform>())
            {
                if (candidate.name == "MovingPlatform_1")
                {
                    platform = candidate;
                }
            }

            Assert.IsNotNull(platform, "MovingPlatform_1");
            player.Motor.Teleport(platform.transform.position + Vector3.up * 0.1f, Quaternion.identity);
            yield return WaitUntil(() => player.Motor.IsGrounded, 2f, "player to land on the platform");

            Vector3 offset = player.transform.position - platform.transform.position;
            float platformStartX = platform.transform.position.x;
            yield return new WaitForSeconds(2.5f);

            Assert.Greater(Mathf.Abs(platform.transform.position.x - platformStartX), 1f, "Platform should have moved.");
            Vector3 newOffset = player.transform.position - platform.transform.position;
            Assert.Less((newOffset - offset).magnitude, 0.35f, "Player should ride along with the platform.");
            Assert.IsTrue(player.Motor.IsGrounded);
        }

        [UnityTest]
        public IEnumerator Zoom_ChangesCameraDistance_WithinLimits()
        {
            yield return WaitForRunStart();
            ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
            CameraConfig config = cam.config;
            float start = cam.ZoomDistance;
            Assert.AreEqual(config.distance, start, 0.05f);

            // Mouse wheel: three notches in (raw Windows units).
            input.ZoomIsScroll = true;
            input.Zoom = 360f;
            yield return null;
            input.Zoom = 0f;
            yield return new WaitForSeconds(0.6f);
            Assert.Less(cam.ZoomDistance, start - 1.5f, "Wheel should zoom in.");
            Assert.GreaterOrEqual(cam.ZoomDistance, config.zoomMinDistance - 0.001f);

            // Held d-pad: zoom out for long enough to hit the limit, which must clamp.
            input.ZoomIsScroll = false;
            input.Zoom = -1f;
            yield return new WaitForSeconds(3f);
            input.Zoom = 0f;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(config.zoomMaxDistance, cam.ZoomDistance, 0.05f);

            // Zoomed all the way out: the camera must still be looking at the player.
            Transform t = Camera.main.transform;
            Assert.Greater(Vector3.Dot(t.forward, (player.FocusPosition - t.position).normalized), 0.8f);

            // Zoom is ignored while paused.
            float before = cam.ZoomTargetDistance;
            course.SetPaused(true);
            input.ZoomIsScroll = true;
            input.Zoom = 480f;
            yield return new WaitForSecondsRealtime(0.2f);
            input.Zoom = 0f;
            Assert.AreEqual(before, cam.ZoomTargetDistance, 0.0001f);
            course.SetPaused(false);

            cam.ResetZoom();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(config.distance, cam.ZoomDistance, 0.05f);
        }

        // ------------------------------------------------------------------ knockdown and ragdoll

        [UnityTest]
        public IEnumerator Knockdown_Ragdolls_Settles_AndReturnsControl()
        {
            yield return WaitForRunStart();

            Assert.IsTrue(player.Knockdown.ForceKnockdown(new Vector3(0f, 2f, -4f), player.FocusPosition));
            Assert.IsTrue(player.Ragdoll.IsActive, "Ragdoll should be simulated.");
            Assert.IsFalse(player.Motor.ControlEnabled, "Input is disabled while down.");
            Assert.IsFalse(player.Knockdown.ForceKnockdown(Vector3.forward * 5f, player.FocusPosition),
                "A second knockdown while down must be rejected.");

            // Physics must stay sane the whole time the body is down.
            float maxSpeed = player.Ragdoll.Config.maxLinearVelocity + 1f;
            float deadline = Time.time + 12f;
            while (player.Knockdown.IsDown && Time.time < deadline)
            {
                foreach (Rigidbody body in player.Ragdoll.Bodies)
                {
                    Vector3 p = body.position;
                    Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), body.name + " position is NaN");
                    Assert.Less(p.magnitude, 500f, body.name + " flew away");
                    if (player.Ragdoll.IsActive)
                    {
                        Assert.LessOrEqual(body.linearVelocity.magnitude, maxSpeed, body.name + " exceeds the speed cap");
                    }
                }

                yield return null;
            }

            Assert.IsFalse(player.Knockdown.IsDown, "Player should recover.");
            Assert.IsFalse(player.Ragdoll.IsActive);
            Assert.IsTrue(player.Motor.ControlEnabled, "Control returns after recovery.");
            Assert.IsTrue(player.Knockdown.IsImmune, "Brief immunity prevents chain knockdowns.");
            Assert.AreEqual(1, course.Session.Wipeouts);

            yield return WaitUntil(() => player.Motor.IsGrounded, 2f, "player to stand after recovery");
            Assert.Greater(Vector3.Dot(player.transform.up, Vector3.up), 0.99f, "Player should be upright.");
        }

        [UnityTest]
        public IEnumerator Sweeper_KnocksThePlayerDown()
        {
            yield return WaitForRunStart();
            RotatingSweeper sweeper = Object.FindAnyObjectByType<RotatingSweeper>();
            Assert.IsNotNull(sweeper);

            // Stand in the path of the arm, 3.5 m from the hub, and wait for it to come around.
            player.Motor.Teleport(sweeper.transform.position + new Vector3(0f, 0.05f, -3.5f), Quaternion.identity);
            yield return WaitUntil(() => player.Knockdown.IsDown || player.Respawner.IsRespawning, 8f, "sweeper to hit the player");

            Assert.GreaterOrEqual(course.Session.Wipeouts, 1, "The hit should count as a wipeout.");

            // Whether the body stays on the island or is swept into the water, control must come back.
            yield return WaitUntil(
                () => !player.Knockdown.IsDown && !player.Respawner.IsRespawning && player.Motor.ControlEnabled,
                15f, "player to recover or respawn");
        }

        // ------------------------------------------------------------------ water, checkpoints, finish

        [UnityTest]
        public IEnumerator Water_RespawnsAtStart_AndCountsAFall()
        {
            yield return WaitForRunStart();
            Checkpoint start = course.Checkpoints.GetCheckpoint(0);

            player.Motor.Teleport(new Vector3(9f, 1f, 0f), Quaternion.identity); // beside the start platform, over water
            yield return WaitUntil(() => player.Respawner.IsRespawning, 5f, "player to fall into the water");
            Assert.AreEqual(1, course.Session.Falls);
            Assert.IsFalse(player.IsVisible, "Hidden while waiting to respawn.");

            yield return WaitUntil(() => !player.Respawner.IsRespawning, 5f, "respawn");
            Vector3 flatOffset = player.transform.position - start.SpawnPosition;
            flatOffset.y = 0f;
            Assert.Less(flatOffset.magnitude, 0.5f, "Should reappear at the start checkpoint.");
            Assert.IsTrue(player.IsVisible);
            Assert.Less(player.Motor.Velocity.magnitude, 1f, "Velocity is reset.");
            Assert.IsFalse(player.Ragdoll.IsActive, "Physics state is reset.");

            // No respawn loop: the player stays put and keeps control.
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(1, course.Session.Falls);
            Assert.IsTrue(player.Motor.IsGrounded);
            Assert.IsTrue(player.Motor.ControlEnabled);
        }

        [UnityTest]
        public IEnumerator Checkpoint_Activates_AndBecomesTheRespawnPoint()
        {
            yield return WaitForRunStart();
            Checkpoint second = course.Checkpoints.GetCheckpoint(1);
            Assert.AreEqual(0, course.Checkpoints.CurrentIndex);

            player.Motor.Teleport(second.transform.position + Vector3.up * 0.1f, Quaternion.identity);
            yield return WaitUntil(() => course.Checkpoints.CurrentIndex == 1, 3f, "checkpoint 1 to activate");
            Assert.IsTrue(second.IsActivated);

            // Checkpoints never move backwards.
            player.Motor.Teleport(course.Checkpoints.GetCheckpoint(0).SpawnPosition + Vector3.up * 0.1f, Quaternion.identity);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, course.Checkpoints.CurrentIndex);

            player.Motor.Teleport(new Vector3(9f, 1f, 0f), Quaternion.identity);
            yield return WaitUntil(() => player.Respawner.IsRespawning, 5f, "player to fall into the water");
            yield return WaitUntil(() => !player.Respawner.IsRespawning, 5f, "respawn");

            Vector3 flatOffset = player.transform.position - second.SpawnPosition;
            flatOffset.y = 0f;
            Assert.Less(flatOffset.magnitude, 0.5f, "Should reappear at checkpoint 1.");
        }

        [UnityTest]
        public IEnumerator FinishLine_StopsTimer_AndShowsResults()
        {
            yield return WaitForRunStart();
            yield return new WaitForSeconds(0.3f);

            FinishLine finish = Object.FindAnyObjectByType<FinishLine>();
            player.Motor.Teleport(finish.transform.position + Vector3.up * 0.1f, Quaternion.identity);
            yield return WaitUntil(() => course.Session.Phase == CoursePhase.Finished, 3f, "finish line to trigger");

            float finalTime = course.Session.ElapsedSeconds;
            Assert.Greater(finalTime, 0.2f);
            Assert.AreEqual(finalTime, course.LastResult.TimeSeconds, 0.0001f);

            ResultsScreenController results = Object.FindAnyObjectByType<ResultsScreenController>();
            Assert.IsNotNull(results);
            Assert.IsTrue(results.IsShowing, "Completion screen should be visible.");
            Assert.IsFalse(player.Motor.ControlEnabled);

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(finalTime, course.Session.ElapsedSeconds, 0.0001f, "Timer stays stopped.");
            Assert.Greater(course.BestSeconds, 0f, "A time is recorded.");
        }

        [UnityTest]
        public IEnumerator Restart_ReloadsTheCourse_FromTheCountdown()
        {
            yield return WaitForRunStart();
            CourseManager before = course;

            course.RestartCourse();
            yield return WaitUntil(() => CourseManager.Current != null && CourseManager.Current != before, 5f, "scene reload");
            yield return null;

            CourseManager after = CourseManager.Current;
            Assert.That(after.Session.Phase, Is.EqualTo(CoursePhase.Countdown).Or.EqualTo(CoursePhase.Running));
            Assert.Less(after.Session.ElapsedSeconds, 1f);
            Assert.AreEqual(0, after.Session.Wipeouts);
            Assert.AreEqual(0, after.Checkpoints.CurrentIndex);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
