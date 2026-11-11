using BikiniWipeout.Core;
using BikiniWipeout.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.Tests
{
    /// <summary>Checks that need the Unity editor: the real movement config against the layout, and the generated Course 1 assets.</summary>
    public class Course1ContentTests
    {
        /// <summary>
        /// The layout tests in Core use the default movement values. This one uses the project's ACTUAL
        /// PlayerMovementConfig asset, so retuning the character cannot silently make the course impossible.
        /// </summary>
        [Test]
        public void Course_IsCompletable_WithTheProjectsMovementConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(Foundation1Builder.MovementConfigPath);
            if (config == null)
            {
                Assert.Ignore("PlayerMovementConfig asset has not been generated yet.");
            }

            var jump = new JumpEnvelope(config.walkSpeed, config.runSpeed, config.jumpHeight, config.gravity, config.fallGravityMultiplier);
            for (int i = 0; i + 1 < Course1Layout.Surfaces.Count; i++)
            {
                CourseSurface from = Course1Layout.Surfaces[i];
                CourseSurface to = Course1Layout.Surfaces[i + 1];
                float gap = Course1Layout.Gap(from, to);
                float reach = jump.RunReach(to.Height - from.Height);
                Assert.Less(gap, reach * 0.85f,
                    $"{from.Name} -> {to.Name}: gap {gap:0.00} m is too close to the limit of the current running jump ({reach:0.00} m). " +
                    "Adjust Course1Layout rather than the character.");
            }
        }

        [Test]
        public void Course1Prefabs_AreBuiltOnTheSharedObstacleSystems()
        {
            if (!Foundation1Builder.IsCourse1Built())
            {
                Assert.Ignore("Course 1 has not been generated. Run: Bikini Wipeout > Course 1 > Build Splashdown Gauntlet.");
            }

            string folder = Foundation1Builder.Course1PrefabFolder + "/";
            foreach (string name in new[] { "SweeperSmall", "SweeperLarge", "SweeperDouble", "RotatingBeam" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
                Assert.IsNotNull(prefab, name);
                Assert.IsNotNull(prefab.GetComponent<RotatingSweeper>(), name);
                var body = prefab.GetComponentInChildren<Rigidbody>(true);
                Assert.IsTrue(body != null && body.isKinematic, name + " arm must be kinematic");
                Assert.IsNotNull(prefab.GetComponentInChildren<ImpactSource>(true), name + " needs an ImpactSource");
                Assert.IsNull(prefab.GetComponentInChildren<RagdollController>(true), name + " must not reference the ragdoll");
            }

            var spin = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "SpinPlatform.prefab");
            Assert.IsNotNull(spin.GetComponent<RotatingSweeper>());
            Assert.IsNull(spin.GetComponentInChildren<ImpactSource>(true), "A platform you ride must not knock you down.");
            Assert.AreEqual(GameLayers.MovingPlatform, spin.GetComponentInChildren<MeshCollider>(true).gameObject.layer);

            var piston = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "PistonWall.prefab");
            Assert.IsNotNull(piston.GetComponent<MovingPlatform>());
            Assert.IsNotNull(piston.GetComponent<ImpactSource>());
            Assert.AreEqual(GameLayers.Obstacle, piston.layer);

            var finish = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "FinishGate.prefab");
            Assert.IsNotNull(finish.GetComponent<FinishLine>());
            Assert.IsNotNull(finish.GetComponentInChildren<FinishCelebration>(true));
            Assert.GreaterOrEqual(finish.GetComponentsInChildren<ParticleSystem>(true).Length, 2, "confetti cannons");

            ProjectValidator.Report report = ProjectValidator.Run();
            Assert.IsTrue(report.Ok, report.ToString());
        }
    }
}
