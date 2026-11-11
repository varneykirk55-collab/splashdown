using System.Collections;
using BikiniWipeout.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BikiniWipeout.Tests
{
    /// <summary>
    /// Scripted checks on the generated Course 1 scene. They verify wiring, collision and rules, not
    /// feel: a human playtest is still required. Needs the generated scene in Build Settings.
    /// </summary>
    public class Course1PlayTests
    {
        private CourseManager course;
        private PlayerCharacter player;
        private ScriptedInput input;

        [UnitySetUp]
        public IEnumerator LoadCourse()
        {
            if (SceneUtility.GetBuildIndexByScenePath(SceneNames.CourseGauntlet) < 0)
            {
                Assert.Ignore("Course 1 is not in Build Settings. Run: Bikini Wipeout > Course 1 > Build Splashdown Gauntlet.");
            }

            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneNames.CourseGauntlet, LoadSceneMode.Single);
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

        private static Vector3 SurfaceTop(CourseSurface surface)
        {
            Course1Layout.WorldCenter(surface, 0f, out float x, out float z);
            return new Vector3(x, surface.Height, z);
        }

        [UnityTest]
        public IEnumerator Scene_HasAllFiveSections_Wired()
        {
            Assert.AreEqual(5, course.Checkpoints.Count, "checkpoints");
            Assert.AreEqual(5, Object.FindObjectsByType<SectionAnnouncer>().Length, "section gates");
            Assert.AreEqual(5, Object.FindObjectsByType<BouncingBall>().Length, "bouncing balls");
            Assert.AreEqual(5, Object.FindObjectsByType<Pendulum>().Length, "pendulums");
            // 2 movers in the Launch Pad + 3 piston walls.
            Assert.AreEqual(5, Object.FindObjectsByType<MovingPlatform>().Length, "moving platforms and pistons");
            // 2 small + 1 large + 3 beams + 1 double sweeper + 2 spin discs.
            Assert.AreEqual(9, Object.FindObjectsByType<RotatingSweeper>().Length, "rotating obstacles");
            Assert.IsNotNull(Object.FindAnyObjectByType<WaterHazard>());
            Assert.IsNotNull(Object.FindAnyObjectByType<FinishLine>());
            Assert.IsNotNull(Object.FindAnyObjectByType<FinishCelebration>());
            Assert.IsNotNull(Object.FindAnyObjectByType<ArenaAmbience>());
            Assert.IsNotNull(Object.FindAnyObjectByType<HudController>());
            Assert.IsNotNull(Camera.main.GetComponent<ThirdPersonCamera>());
            Assert.IsNotNull(AudioManager.Instance);
            Assert.IsNotNull(AudioManager.Instance.Library.FindSfx(AudioEventId.ArenaAmbience), "arena audio entries");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_SpawnsOnTheStartPlatform_AndCountdownRuns()
        {
            Assert.AreEqual(CoursePhase.Countdown, course.Session.Phase);
            yield return WaitForRunStart();
            Vector3 start = SurfaceTop(Course1Layout.Surfaces[0]);
            Assert.AreEqual(start.y, player.transform.position.y, 0.15f);
            Assert.Less(Mathf.Abs(player.transform.position.x - start.x), 6f);
            Assert.IsTrue(player.Motor.ControlEnabled);
        }

        /// <summary>Every static surface in the layout must physically exist where the layout says, at the right height.</summary>
        [UnityTest]
        public IEnumerator EveryLayoutSurface_HasSolidGround()
        {
            yield return new WaitForFixedUpdate();
            foreach (CourseSurface surface in Course1Layout.Surfaces)
            {
                if (surface.IsMoving)
                {
                    continue;
                }

                Vector3 top = SurfaceTop(surface);
                // Probe a little off-centre so obstacle hubs standing in the middle are not in the way.
                float reach = surface.Shape == SurfaceShape.Disc ? surface.Radius * 0.8f : Mathf.Min(surface.Width, surface.Length) * 0.4f;
                bool found = false;
                foreach (Vector3 offset in new[] { Vector3.zero, Vector3.right * reach, Vector3.left * reach, Vector3.forward * reach, Vector3.back * reach })
                {
                    if (Physics.Raycast(top + offset + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f,
                            GameLayers.SafeGroundMask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - surface.Height) < 0.08f)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, surface.Name + ": no walkable ground at the layout position.");
            }

            foreach (MovingPlatform platform in Object.FindObjectsByType<MovingPlatform>())
            {
                Assert.IsNotNull(platform.GetComponentInChildren<Collider>(), platform.name + " has no collider");
            }
        }

        [UnityTest]
        public IEnumerator Obstacles_AreMoving()
        {
            yield return WaitForRunStart();
            RotatingSweeper[] rotators = Object.FindObjectsByType<RotatingSweeper>();
            Pendulum[] pendulums = Object.FindObjectsByType<Pendulum>();
            MovingPlatform[] movers = Object.FindObjectsByType<MovingPlatform>();
            var angles = new float[rotators.Length];
            var swings = new float[pendulums.Length];
            var positions = new Vector3[movers.Length];
            for (int i = 0; i < rotators.Length; i++) angles[i] = rotators[i].CurrentAngle;
            for (int i = 0; i < pendulums.Length; i++) swings[i] = pendulums[i].CurrentAngle;
            for (int i = 0; i < movers.Length; i++) positions[i] = movers[i].transform.position;

            yield return new WaitForSeconds(1.7f);

            for (int i = 0; i < rotators.Length; i++)
            {
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(angles[i], rotators[i].CurrentAngle)), 5f, rotators[i].name + " is not rotating");
            }

            for (int i = 0; i < pendulums.Length; i++)
            {
                Assert.Greater(Mathf.Abs(swings[i] - pendulums[i].CurrentAngle), 2f, pendulums[i].name + " is not swinging");
            }

            int moved = 0;
            for (int i = 0; i < movers.Length; i++)
            {
                if ((movers[i].transform.position - positions[i]).magnitude > 0.3f) moved++;
            }

            // Platforms and pistons pause at each end, so not every one is mid-travel in a given window.
            Assert.GreaterOrEqual(moved, 2, "moving platforms / pistons should be travelling");
        }

        [UnityTest]
        public IEnumerator EveryCheckpoint_Activates_AndRespawnsThePlayerThere_WithoutALoop()
        {
            yield return WaitForRunStart();
            for (int index = 1; index < course.Checkpoints.Count; index++)
            {
                Checkpoint checkpoint = course.Checkpoints.GetCheckpoint(index);
                player.Motor.Teleport(checkpoint.SpawnPosition + Vector3.up * 0.1f, checkpoint.SpawnRotation);
                yield return WaitUntil(() => course.Checkpoints.CurrentIndex == index, 3f, "checkpoint " + index + " to activate");

                int fallsBefore = course.Session.Falls;
                // Drop into open water well away from the course.
                player.Motor.Teleport(new Vector3(38f, 1f, 20f), Quaternion.identity);
                yield return WaitUntil(() => player.Respawner.IsRespawning, 5f, "fall into water (checkpoint " + index + ")");
                yield return WaitUntil(() => !player.Respawner.IsRespawning, 5f, "respawn (checkpoint " + index + ")");

                Vector3 offset = player.transform.position - checkpoint.SpawnPosition;
                offset.y = 0f;
                Assert.Less(offset.magnitude, 0.6f, "Respawn position for checkpoint " + index);
                Assert.AreEqual(fallsBefore + 1, course.Session.Falls);
                Assert.Greater(Vector3.Dot(player.transform.forward, checkpoint.SpawnRotation * Vector3.forward), 0.95f,
                    "Player should face the next section after respawning at checkpoint " + index);

                yield return new WaitForSeconds(1.6f);
                Assert.AreEqual(fallsBefore + 1, course.Session.Falls, "No respawn loop at checkpoint " + index);
                Assert.IsTrue(player.Motor.IsGrounded, "Standing on the checkpoint platform " + index);
            }
        }

        [UnityTest]
        public IEnumerator Pendulum_KnocksThePlayerDown_AndControlReturns()
        {
            yield return WaitForRunStart();
            Pendulum pendulum = null;
            foreach (Pendulum candidate in Object.FindObjectsByType<Pendulum>())
            {
                if (candidate.name == "D_Pendulum_1") pendulum = candidate;
            }

            Assert.IsNotNull(pendulum, "D_Pendulum_1");
            // Directly below the pivot, on the bridge: the weight passes through here every half swing.
            Vector3 below = pendulum.transform.position;
            below.y = 0.1f;
            player.Motor.Teleport(below, Quaternion.identity);

            yield return WaitUntil(() => player.Knockdown.IsDown || player.Respawner.IsRespawning, 8f, "pendulum to hit the player");
            Assert.GreaterOrEqual(course.Session.Wipeouts, 1);
            yield return WaitUntil(
                () => !player.Knockdown.IsDown && !player.Respawner.IsRespawning && player.Motor.ControlEnabled,
                18f, "player to recover or respawn after the pendulum hit");
        }

        [UnityTest]
        public IEnumerator SpinDisc_CarriesThePlayerAround()
        {
            yield return WaitForRunStart();
            RotatingSweeper disc = null;
            foreach (RotatingSweeper candidate in Object.FindObjectsByType<RotatingSweeper>())
            {
                if (candidate.name == "SpinDisc_1") disc = candidate;
            }

            Assert.IsNotNull(disc, "SpinDisc_1");
            Vector3 centre = disc.transform.position;
            player.Motor.Teleport(centre + new Vector3(2.5f, 0.1f, 0f), Quaternion.identity);
            yield return WaitUntil(() => player.Motor.IsGrounded, 2f, "player to land on the disc");

            Vector3 before = player.transform.position - centre;
            yield return new WaitForSeconds(2f);
            Vector3 after = player.transform.position - centre;
            before.y = 0f;
            after.y = 0f;

            Assert.Greater(Vector3.Angle(before, after), 20f, "The disc should carry the player around its centre.");
            Assert.AreEqual(before.magnitude, after.magnitude, 0.4f, "The player should stay at the same radius, not slide off.");
            Assert.IsTrue(player.Motor.IsGrounded);
        }

        [UnityTest]
        public IEnumerator Finish_StopsTheTimer_ShowsResults_AndCelebrates()
        {
            yield return WaitForRunStart();
            yield return new WaitForSeconds(0.3f);

            FinishLine finish = Object.FindAnyObjectByType<FinishLine>();
            player.Motor.Teleport(finish.transform.position + Vector3.up * 0.1f, finish.transform.rotation);
            yield return WaitUntil(() => course.Session.Phase == CoursePhase.Finished, 3f, "finish line to trigger");

            Assert.Greater(course.LastResult.TimeSeconds, 0.2f);
            Assert.IsTrue(Object.FindAnyObjectByType<ResultsScreenController>().IsShowing, "results screen");
            Assert.IsTrue(Object.FindAnyObjectByType<FinishCelebration>().HasFired, "confetti");
            Assert.GreaterOrEqual(AudioManager.GetRequestCount(AudioEventId.CrowdCheer), 1, "crowd cheer hook");
            Assert.Greater(course.BestSeconds, 0f, "time recorded");

            float frozen = course.Session.ElapsedSeconds;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(frozen, course.Session.ElapsedSeconds, 0.0001f);
        }

        [UnityTest]
        public IEnumerator SectionGate_AnnouncesItsSection()
        {
            yield return WaitForRunStart();
            SectionAnnouncer gate = null;
            foreach (SectionAnnouncer candidate in Object.FindObjectsByType<SectionAnnouncer>())
            {
                if (candidate.SectionName == Course1Layout.SectionNames[1]) gate = candidate;
            }

            Assert.IsNotNull(gate, "Big Bounce gate");
            Assert.IsFalse(gate.Announced);
            player.Motor.Teleport(gate.transform.position + Vector3.up * 0.1f, gate.transform.rotation);
            yield return WaitUntil(() => gate.Announced, 3f, "section announcement");
        }
    }
}
