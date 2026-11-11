using BikiniWipeout.Core;
using BikiniWipeout.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.Tests
{
    /// <summary>Checks that need the Unity editor: the real movement config against the Course 2 layout, and the generated prefabs.</summary>
    public class Course2ContentTests
    {
        /// <summary>
        /// Uses the project's ACTUAL PlayerMovementConfig asset, so retuning the character cannot silently
        /// make Course 2 (and in particular the Big Balls) impossible.
        /// </summary>
        [Test]
        public void Course2_IsCompletable_WithTheProjectsMovementConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(Foundation1Builder.MovementConfigPath);
            if (config == null)
            {
                Assert.Ignore("PlayerMovementConfig asset has not been generated yet.");
            }

            var jump = new JumpEnvelope(config.walkSpeed, config.runSpeed, config.jumpHeight, config.gravity, config.fallGravityMultiplier);
            for (int i = 0; i + 1 < Course2Layout.Surfaces.Count; i++)
            {
                CourseSurface from = Course2Layout.Surfaces[i];
                CourseSurface to = Course2Layout.Surfaces[i + 1];
                float gap = CourseLayoutMath.Gap(Course2Layout.Legs, from, to);
                if (gap <= 0f)
                {
                    continue;
                }

                float rise = to.Height - from.ExitHeight;
                float reach = from.LaunchSpeed > 0f
                    ? jump.RunSpeed * SlipMath.LaunchAirTime(from.LaunchSpeed, rise, jump.Gravity, jump.FallGravityMultiplier)
                    : jump.RunReach(rise);
                Assert.Less(gap, reach * 0.85f,
                    $"{from.Name} -> {to.Name}: gap {gap:0.00} m is too close to the limit of the current character ({reach:0.00} m). " +
                    "Adjust Course2Layout rather than the character.");
            }
        }

        [Test]
        public void Course2Prefabs_AreBuiltOnTheSharedSystems()
        {
            if (!Foundation1Builder.IsCourse2Built())
            {
                Assert.Ignore("Course 2 has not been generated. Run: Bikini Wipeout > Course 2 > Build Chaos Cove.");
            }

            string folder = Foundation1Builder.Course2PrefabFolder + "/";
            GameObject Load(string name)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
                Assert.IsNotNull(prefab, name);
                Assert.IsNull(prefab.GetComponentInChildren<RagdollController>(true), name + " must not reference the ragdoll");
                Assert.IsNull(prefab.GetComponentInChildren<PlayerCharacter>(true), name + " must not contain a character");
                return prefab;
            }

            foreach (string name in new[] { "CoveSweeper", "CoveDoubleSweeper", "SpinningArms", "GiantArms" })
            {
                GameObject prefab = Load(name);
                Assert.IsNotNull(prefab.GetComponent<RotatingSweeper>(), name);
                Assert.IsTrue(prefab.GetComponentInChildren<Rigidbody>(true).isKinematic, name + " arm must be kinematic");
                Assert.IsNotNull(prefab.GetComponentInChildren<ImpactSource>(true), name + " needs an ImpactSource");
            }

            Assert.IsNotNull(Load("CoveMovingPlatform").GetComponent<MovingPlatform>());
            Assert.IsNotNull(Load("SwingingBall").GetComponent<Pendulum>());
            Assert.IsNotNull(Load("SwingingBall").GetComponentInChildren<ImpactSource>(true));
            Assert.AreEqual(WaterJetMode.Geyser, Load("WaterGeyser").GetComponent<WaterJet>().mode);
            Assert.AreEqual(WaterJetMode.Push, Load("WaterSideJet").GetComponent<WaterJet>().mode);
            Assert.IsNotNull(Load("WaterGeyser").GetComponent<WaterJet>().activeEffect, "visible water");
            Assert.IsNotNull(Load("TippingBucket").GetComponent<TippingBucket>().bucket, "the bucket that visibly tips");
            Assert.AreEqual(TiltMode.Seesaw, Load("SeesawWide").GetComponent<TiltingPlatform>().mode);
            Assert.AreEqual(TiltMode.Raft, Load("FloatingArena").GetComponent<TiltingPlatform>().mode);
            Assert.IsNotNull(Load("FloatingArena").transform.Find("Disc"), "arena hazards are parented to Disc");
            Assert.IsNotNull(Load("BouncePad").GetComponent<BouncePad>());
            Assert.IsNotNull(Load("BouncePad").GetComponentInChildren<MeshCollider>(true), "a solid top to land on");
            Assert.IsNotNull(Load("CollapsingBridgeSegment").GetComponent<CollapsingPlatform>());
            Assert.IsNotNull(Load("CheckpointGate").GetComponent<Checkpoint>());

            GameObject ball = Load("BigBall");
            var slick = ball.GetComponentInChildren<SlipperySurface>(true);
            Assert.AreEqual(SlipShape.Sphere, slick.shape);
            Assert.AreEqual(2f, slick.SphereRadius, 0.01f, "enormous");
            Assert.Less(slick.traction, 0.2f, "very little traction");
            Assert.IsNull(ball.GetComponentInChildren<Rigidbody>(true), "stationary: no physics body");
            Assert.IsNull(ball.GetComponentInChildren<RotatingSweeper>(true), "no rotation");
            Assert.IsNull(ball.GetComponentInChildren<TiltingPlatform>(true), "no wobble");
            Assert.AreEqual(GameLayers.Ground, slick.gameObject.layer);

            ProjectValidator.Report report = ProjectValidator.Run();
            Assert.IsTrue(report.Ok, report.ToString());
        }
    }
}
