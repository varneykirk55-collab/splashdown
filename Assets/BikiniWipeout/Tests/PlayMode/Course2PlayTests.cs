using System.Collections;
using System.Linq;
using BikiniWipeout.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BikiniWipeout.Tests
{
    /// <summary>
    /// Scripted checks on the generated Course 2 scene (Chaos Cove). They verify wiring, collision and
    /// rules, not feel: a human playtest is still required. Needs the generated scene in Build Settings.
    /// </summary>
    public class Course2PlayTests
    {
        private CourseManager course;
        private PlayerCharacter player;
        private ScriptedInput input;

        [UnitySetUp]
        public IEnumerator LoadCourse()
        {
            if (SceneUtility.GetBuildIndexByScenePath(SceneNames.CourseChaosCove) < 0)
            {
                Assert.Ignore("Course 2 is not in Build Settings. Run: Bikini Wipeout > Course 2 > Build Chaos Cove.");
            }

            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneNames.CourseChaosCove, LoadSceneMode.Single);
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

        private static Vector3 LegPoint(int leg, float x, float y, float z)
        {
            Course2Layout.Legs[leg].ToWorld(x, z, out float worldX, out float worldZ);
            return new Vector3(worldX, y, worldZ);
        }

        private static Vector3 SurfaceTop(CourseSurface surface)
        {
            return LegPoint(surface.Leg, surface.X, surface.Height, surface.Z);
        }

        private static T Named<T>(string name) where T : Component
        {
            T found = Object.FindObjectsByType<T>().FirstOrDefault(c => c.name == name);
            Assert.IsNotNull(found, typeof(T).Name + " '" + name + "'");
            return found;
        }

        private bool PlayerIsBackInControl()
        {
            return !player.Knockdown.IsDown && !player.Respawner.IsRespawning && player.Motor.ControlEnabled;
        }

        [UnityTest]
        public IEnumerator Scene_HasEveryObstacleInTheLayout_Wired()
        {
            int Hazards(params ObstacleKind[] kinds) => Course2Layout.Obstacles.Count(o => kinds.Contains(o.Kind));
            int Kind(SurfaceKind kind) => Course2Layout.Surfaces.Count(s => s.Kind == kind);

            Assert.AreEqual(Course2Layout.Checkpoints.Count, course.Checkpoints.Count, "checkpoints");
            Assert.AreEqual(Course2Layout.Gates.Count, Object.FindObjectsByType<SectionAnnouncer>().Length, "section gates");
            Assert.AreEqual(Hazards(ObstacleKind.Sweeper, ObstacleKind.SpinningArms, ObstacleKind.GiantArms, ObstacleKind.DoubleSweeper),
                Object.FindObjectsByType<RotatingSweeper>().Length, "sweepers and spinning arms");
            Assert.AreEqual(Kind(SurfaceKind.MovingPlatform), Object.FindObjectsByType<MovingPlatform>().Length, "moving platforms");
            Assert.AreEqual(Kind(SurfaceKind.Seesaw) + Kind(SurfaceKind.FloatingArena), Object.FindObjectsByType<TiltingPlatform>().Length, "seesaws and arena");
            Assert.AreEqual(Kind(SurfaceKind.BouncePad), Object.FindObjectsByType<BouncePad>().Length, "bounce pads");
            Assert.AreEqual(Hazards(ObstacleKind.Geyser, ObstacleKind.SideJet), Object.FindObjectsByType<WaterJet>().Length, "water jets");
            Assert.AreEqual(Hazards(ObstacleKind.TippingBucket), Object.FindObjectsByType<TippingBucket>().Length, "tipping buckets");
            Assert.AreEqual(Hazards(ObstacleKind.SwingingBall), Object.FindObjectsByType<Pendulum>().Length, "swinging balls");
            Assert.AreEqual(Hazards(ObstacleKind.BouncingBall), Object.FindObjectsByType<BouncingBall>().Length, "bouncing balls");
            Assert.GreaterOrEqual(Object.FindObjectsByType<CollapsingPlatform>().Length, 5, "collapsing bridge planks");
            // The water slide plus the four Big Balls.
            SlipperySurface[] slick = Object.FindObjectsByType<SlipperySurface>();
            Assert.AreEqual(1, slick.Count(s => s.shape == SlipShape.Collider), "water slide");
            Assert.AreEqual(4, slick.Count(s => s.shape == SlipShape.Sphere), "big balls");

            Assert.IsNotNull(Object.FindAnyObjectByType<WaterHazard>());
            Assert.IsNotNull(Object.FindAnyObjectByType<FinishLine>());
            Assert.IsNotNull(Object.FindAnyObjectByType<FinishCelebration>());
            Assert.IsNotNull(Object.FindAnyObjectByType<ObstacleResetter>());
            Assert.IsNotNull(Object.FindAnyObjectByType<HudController>());
            Assert.IsNotNull(Camera.main.GetComponent<ThirdPersonCamera>());
            Assert.IsNotNull(AudioManager.Instance);
            foreach (AudioEventId id in new[]
                     {
                         AudioEventId.SpringBounce, AudioEventId.WaterJet, AudioEventId.BucketWarning, AudioEventId.BucketDump,
                         AudioEventId.SlideWhoosh, AudioEventId.BridgeCrack, AudioEventId.BridgeCollapse, AudioEventId.InflatableBoing,
                         AudioEventId.TropicalAmbience
                     })
            {
                Assert.IsNotNull(AudioManager.Instance.Library.FindSfx(id), "audio entry " + id);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_SpawnsOnTheStartPlatform_AndCountdownRuns()
        {
            Assert.AreEqual(CoursePhase.Countdown, course.Session.Phase);
            yield return WaitForRunStart();
            Vector3 start = SurfaceTop(Course2Layout.Surfaces[0]);
            Assert.AreEqual(start.y, player.transform.position.y, 0.15f);
            Assert.Less(Mathf.Abs(player.transform.position.x - start.x), 6f);
            Assert.IsTrue(player.Motor.ControlEnabled);
        }

        /// <summary>Every surface in the layout must physically exist where the layout says, at the right height.</summary>
        [UnityTest]
        public IEnumerator EveryLayoutSurface_HasSolidGround()
        {
            yield return new WaitForFixedUpdate();
            foreach (CourseSurface surface in Course2Layout.Surfaces)
            {
                if (surface.IsMoving)
                {
                    continue;
                }

                // Inclines are probed at their middle, half way down.
                Vector3 top = SurfaceTop(surface) + Vector3.down * (surface.Drop * 0.5f);
                float reach = surface.Shape == SurfaceShape.Disc ? surface.Radius * 0.6f : Mathf.Min(surface.Width, surface.Length) * 0.3f;
                bool sloped = surface.Drop != 0f || surface.Sag > 0f || surface.Kind == SurfaceKind.BigBall;
                Vector3[] offsets = sloped
                    ? new[] { Vector3.zero }
                    : new[] { Vector3.zero, Vector3.right * reach, Vector3.left * reach, Vector3.forward * reach, Vector3.back * reach };
                bool found = false;
                foreach (Vector3 offset in offsets)
                {
                    if (Physics.Raycast(top + offset + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f,
                            GameLayers.SafeGroundMask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - top.y) < 0.12f)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, surface.Name + ": no walkable ground at the layout position.");
            }
        }

        [UnityTest]
        public IEnumerator Checkpoints_Activate_AndRespawnOnSolidGround()
        {
            yield return WaitForRunStart();
            for (int index = 1; index < course.Checkpoints.Count; index++)
            {
                Checkpoint checkpoint = course.Checkpoints.GetCheckpoint(index);
                player.Motor.Teleport(checkpoint.SpawnPosition + Vector3.up * 0.1f, checkpoint.SpawnRotation);
                int expected = index;
                yield return WaitUntil(() => course.Checkpoints.CurrentIndex == expected, 3f, "checkpoint " + index + " to activate");
                Assert.IsTrue(checkpoint.IsActivated);
                yield return WaitUntil(() => player.Motor.IsGrounded, 2f, "player to stand on checkpoint " + index);

                int fallsBefore = course.Session.Falls;
                Assert.IsTrue(player.Respawner.OnHazardEntered(player.transform.position), "respawn should start");
                yield return WaitUntil(() => !player.Respawner.IsRespawning, 4f, "respawn at checkpoint " + index);
                yield return WaitUntil(() => player.Motor.IsGrounded, 2f, "player to land after respawning at checkpoint " + index);
                Assert.Less(Vector3.Distance(player.transform.position, checkpoint.SpawnPosition), 1.5f, "respawned at checkpoint " + index);
                Assert.AreEqual(fallsBefore + 1, course.Session.Falls);

                yield return new WaitForSeconds(1.6f);
                Assert.AreEqual(fallsBefore + 1, course.Session.Falls, "No respawn loop at checkpoint " + index);
            }
        }

        [UnityTest]
        public IEnumerator BouncePad_LaunchesThePlayer_HigherThanAJump()
        {
            yield return WaitForRunStart();
            BouncePad pad = Named<BouncePad>("D_Bounce_1");
            player.Motor.Teleport(pad.transform.position + Vector3.up * 0.3f, pad.transform.rotation);
            yield return WaitUntil(() => pad.LaunchCount >= 1, 3f, "bounce pad to fire");

            float peak = 0f;
            float end = Time.time + 1.2f;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, player.transform.position.y - pad.transform.position.y);
                yield return null;
            }

            float expected = SlipMath.LaunchApex(pad.LaunchSpeed, player.Motor.Config.gravity);
            Assert.Greater(peak, player.Motor.Config.jumpHeight + 0.3f, "A bounce should beat a jump.");
            Assert.AreEqual(expected, peak, 0.45f, "Launch height should be predictable.");
            Assert.GreaterOrEqual(AudioManager.GetRequestCount(AudioEventId.SpringBounce), 1, "spring sound hook");
            Assert.IsFalse(player.Knockdown.IsDown, "A bounce is not a knockdown.");
        }

        [UnityTest]
        public IEnumerator BigBalls_NeverMove_AndTheCrownCanBeStoodOn()
        {
            yield return WaitForRunStart();
            SlipperySurface[] balls = Object.FindObjectsByType<SlipperySurface>().Where(s => s.shape == SlipShape.Sphere).ToArray();
            Assert.AreEqual(4, balls.Length);
            var poses = balls.Select(b => (b.transform.position, b.transform.rotation)).ToArray();
            foreach (SlipperySurface ball in balls)
            {
                Assert.IsNull(ball.GetComponentInParent<Rigidbody>(), ball.name + " must not be a physics body.");
                Assert.AreEqual(2f, ball.SphereRadius, 0.05f);
            }

            CourseSurface second = Course2Layout.Find("BigBall_2");
            Vector3 crown = SurfaceTop(second);
            player.Motor.Teleport(crown + Vector3.up * 0.05f, Quaternion.identity);
            yield return WaitUntil(() => player.Motor.IsGrounded, 2f, "player to stand on the ball");
            yield return new WaitForSeconds(2f);

            Vector3 offset = player.transform.position - crown;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 0.35f, "The very top of a ball must hold a standing character.");
            Assert.IsTrue(player.Motor.IsGrounded);
            Assert.AreEqual(0, course.Session.Falls);

            for (int i = 0; i < balls.Length; i++)
            {
                Assert.AreEqual(poses[i].position, balls[i].transform.position, balls[i].name + " moved.");
                Assert.AreEqual(poses[i].rotation, balls[i].transform.rotation, balls[i].name + " rotated.");
            }
        }

        [UnityTest]
        public IEnumerator BigBalls_SlideAnOffCentreCharacterIntoTheWater()
        {
            yield return WaitForRunStart();
            CourseSurface first = Course2Layout.Find("BigBall_1");
            Vector3 centre = SurfaceTop(first) + Vector3.down * first.BodyRadius;
            // 35 degrees down the side of the ball, across the course: far beyond what the surface can hold.
            Vector3 direction = Quaternion.Euler(0f, 0f, 35f) * Vector3.up;
            direction = Quaternion.Euler(0f, Course2Layout.Legs[first.Leg].YawDegrees, 0f) * direction;
            player.Motor.Teleport(centre + direction * (first.BodyRadius + 0.05f), Quaternion.identity);

            int fallsBefore = course.Session.Falls;
            yield return WaitUntil(() => course.Session.Falls > fallsBefore, 8f, "the character to slide off the ball into the water");
            yield return WaitUntil(PlayerIsBackInControl, 6f, "respawn after falling off the ball");
            Checkpoint deck = course.Checkpoints.Current;
            Assert.Less(Vector3.Distance(player.transform.position, deck.SpawnPosition), 2f);
        }

        [UnityTest]
        public IEnumerator WaterSlide_CarriesThePlayerDown_AndHandsControlBack()
        {
            yield return WaitForRunStart();
            CourseSurface slide = Course2Layout.Find("D_Slide");
            CourseSurface island = Course2Layout.Find("D_SplashIsland");
            // Just below the lip of the slide, with no input at all.
            Vector3 start = LegPoint(slide.Leg, 0f, slide.Height - 0.25f, slide.Z - slide.Length * 0.5f + 1.2f);
            player.Motor.Teleport(start + Vector3.up * 0.2f, Quaternion.identity);

            SlipperySurface surface = Object.FindObjectsByType<SlipperySurface>().First(s => s.shape == SlipShape.Collider);
            yield return WaitUntil(() => surface.InContact && surface.SlideSpeed > 1f, 3f, "the slide to take hold");

            float previousY = player.transform.position.y;
            float largestStep = 0f;
            float deadline = Time.time + 10f;
            while (player.transform.position.y > island.Height + 0.3f && Time.time < deadline)
            {
                yield return null;
                largestStep = Mathf.Max(largestStep, Mathf.Abs(player.transform.position.y - previousY));
                previousY = player.transform.position.y;
            }

            Assert.Less(player.transform.position.y, island.Height + 0.3f, "The character should reach the bottom of the slide without input.");
            Assert.Less(largestStep, 0.6f, "The descent must be continuous: no teleporting.");
            Assert.AreEqual(0, course.Session.Falls, "The rails should keep a passive rider on the slide.");

            // The run-out is inside the big sweeper's reach, so a wipeout here is legitimate; what matters is
            // that the slide lets go and the player ends up back in control.
            yield return WaitUntil(() => !surface.InContact && surface.SlideSpeed <= 0f, 3f, "the slide to let go of the player");
            yield return WaitUntil(PlayerIsBackInControl, 20f, "control to return after the slide");
        }

        [UnityTest]
        public IEnumerator CollapsingBridge_Falls_ThenIsWholeAgainAfterARespawn()
        {
            yield return WaitForRunStart();
            CollapsingPlatform plank = Named<CollapsingPlatform>("C_CollapseBridge_Plank_3");
            Vector3 home = plank.transform.position;
            player.Motor.Teleport(home + Vector3.up * 0.1f, plank.transform.rotation);

            yield return WaitUntil(() => plank.State == CollapseState.Shaking, 2f, "the plank to start shaking");
            Assert.Less(Vector3.Distance(plank.transform.position, home), 0.01f, "The warning shake must not move the solid plank.");
            yield return WaitUntil(() => plank.State == CollapseState.Falling || plank.State == CollapseState.Down, 2f, "the plank to let go");

            int fallsBefore = course.Session.Falls;
            yield return WaitUntil(() => course.Session.Falls > fallsBefore, 6f, "the character to drop into the water");
            yield return WaitUntil(PlayerIsBackInControl, 6f, "respawn");
            yield return null;

            foreach (CollapsingPlatform segment in Object.FindObjectsByType<CollapsingPlatform>())
            {
                Assert.AreEqual(CollapseState.Idle, segment.State, segment.name + " should be restored after the respawn.");
            }

            Assert.Less(Vector3.Distance(plank.transform.position, home), 0.01f);
            Assert.GreaterOrEqual(Object.FindAnyObjectByType<ObstacleResetter>().ResetCount, 1);
        }

        [UnityTest]
        public IEnumerator TippingBucket_Warns_ThenKnocksThePlayerDown()
        {
            yield return WaitForRunStart();
            TippingBucket bucket = Named<TippingBucket>("C_Bucket_1");
            player.Motor.Teleport(bucket.transform.position + Vector3.up * 0.1f, bucket.transform.rotation);

            bool warned = false;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (bucket.HitCount == 0 && Time.realtimeSinceStartup < deadline)
            {
                warned |= bucket.Phase == HazardPhase.Warning;
                yield return null;
            }

            Assert.GreaterOrEqual(bucket.HitCount, 1, "The bucket should hit a character standing under it.");
            Assert.IsTrue(warned, "The bucket must warn before it dumps.");
            Assert.IsTrue(player.Knockdown.IsDown || player.Knockdown.IsImmune, "A full dump is a knockdown (or a shove while immune).");
            Assert.GreaterOrEqual(AudioManager.GetRequestCount(AudioEventId.BucketDump), 1);
            yield return WaitUntil(PlayerIsBackInControl, 18f, "player to recover or respawn after the bucket");
        }

        [UnityTest]
        public IEnumerator Geyser_ThrowsThePlayerIntoTheAir()
        {
            yield return WaitForRunStart();
            WaterJet geyser = Named<WaterJet>("B_Geyser_5");
            player.Motor.Teleport(geyser.transform.position + Vector3.up * 0.1f, geyser.transform.rotation);
            yield return WaitUntil(() => geyser.HitCount >= 1, 8f, "the geyser to fire under the player");

            float peak = 0f;
            float end = Time.time + 0.9f;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, player.transform.position.y - geyser.transform.position.y);
                yield return null;
            }

            Assert.Greater(peak, 1.5f);
            yield return WaitUntil(() => PlayerIsBackInControl() && player.Motor.IsGrounded, 12f, "player to land or respawn");
        }

        [UnityTest]
        public IEnumerator Seesaw_TiltsUnderThePlayer_WithinItsLimit()
        {
            yield return WaitForRunStart();
            TiltingPlatform seesaw = Named<TiltingPlatform>("C_Seesaw_1");
            CourseSurface surface = Course2Layout.Find("C_Seesaw_1");
            Vector3 farEnd = LegPoint(surface.Leg, surface.X, surface.Height + 0.1f, surface.Z + surface.Length * 0.5f - 1.2f);
            player.Motor.Teleport(farEnd, Quaternion.identity);

            float largest = 0f;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                largest = Mathf.Max(largest, Mathf.Abs(seesaw.Tilt.x));
                Assert.LessOrEqual(Mathf.Abs(seesaw.Tilt.x), seesaw.MaxTilt + 0.01f);
                yield return null;
            }

            Assert.Greater(largest, seesaw.MaxTilt * 0.4f, "The seesaw should tip towards the end the player stands on.");
            Assert.IsTrue(player.Motor.IsGrounded, "The player should still be standing on the seesaw.");
            Assert.AreEqual(0, course.Session.Falls);
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
            Assert.Greater(course.BestSeconds, 0f, "time recorded");
        }
    }
}
