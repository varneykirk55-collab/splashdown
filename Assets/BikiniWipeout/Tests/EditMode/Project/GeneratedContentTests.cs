using BikiniWipeout.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.Tests
{
    /// <summary>
    /// Checks the content produced by <see cref="Foundation1Builder"/>. Skipped (not failed) when the
    /// content has not been generated yet: run "Bikini Wipeout > Build Foundation 1 Content" first.
    /// </summary>
    public class GeneratedContentTests
    {
        [OneTimeSetUp]
        public void RequireGeneratedContent()
        {
            if (!Foundation1Builder.IsContentBuilt())
            {
                Assert.Ignore("Foundation 1 content has not been generated. Run: Bikini Wipeout > Build Foundation 1 Content.");
            }
        }

        [Test]
        public void Validator_ReportsNoErrors()
        {
            ProjectValidator.Report report = ProjectValidator.Run();
            Assert.IsTrue(report.Ok, report.ToString());
        }

        [Test]
        public void PlayerPrefab_HasAStableRagdollSetup()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Foundation1Builder.PlayerPrefabPath);
            Assert.IsNotNull(player);

            Rigidbody[] bodies = player.GetComponentsInChildren<Rigidbody>(true);
            Assert.AreEqual(11, bodies.Length);

            float totalMass = 0f;
            foreach (Rigidbody body in bodies)
            {
                Assert.IsTrue(body.isKinematic, body.name + " must start kinematic (animation-driven).");
                Assert.AreEqual(GameLayers.Ragdoll, body.gameObject.layer, body.name + " layer");
                Assert.IsNotNull(body.GetComponent<Collider>(), body.name + " needs a collider");
                totalMass += body.mass;
            }

            Assert.That(totalMass, Is.InRange(45f, 80f), "Total ragdoll mass should be human-like.");

            foreach (CharacterJoint joint in player.GetComponentsInChildren<CharacterJoint>(true))
            {
                Assert.IsNotNull(joint.connectedBody, joint.name + " joint is not connected");
                Assert.LessOrEqual(joint.lowTwistLimit.limit, joint.highTwistLimit.limit, joint.name + " twist limits");
                Assert.IsTrue(joint.enableProjection, joint.name + " should use projection");
                Assert.IsFalse(joint.enableCollision, joint.name + " should not collide with its parent");

                // Light bodies hanging off heavy ones are fine; the reverse destabilises the solver.
                float ratio = joint.GetComponent<Rigidbody>().mass / joint.connectedBody.mass;
                Assert.Less(ratio, 3f, joint.name + " is much heavier than the body it hangs from");
            }
        }

        [Test]
        public void ObstaclePrefabs_ImplementResettable()
        {
            foreach (string path in new[]
                     {
                         Foundation1Builder.SweeperPrefabPath, Foundation1Builder.BallPrefabPath,
                         Foundation1Builder.MovingPlatformPrefabPath
                     })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                Assert.IsNotNull(prefab.GetComponent<IResettable>(), path + " should expose IResettable on its root");
            }
        }

        [Test]
        public void ObstaclePrefabs_DoNotReferenceTheRagdoll()
        {
            // Obstacles report impacts through ImpactSource only; ragdoll physics must stay decoupled.
            foreach (string path in new[]
                     {
                         Foundation1Builder.SweeperPrefabPath, Foundation1Builder.BallPrefabPath,
                         Foundation1Builder.MovingPlatformPrefabPath
                     })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNull(prefab.GetComponentInChildren<RagdollController>(true), path);
                Assert.IsNull(prefab.GetComponentInChildren<KnockdownController>(true), path);
            }
        }

        [Test]
        public void Phase2APlaceholder_JointsAccommodateProceduralPoses()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Foundation1Builder.PlayerPrefabPath);
            float mass = 0f;
            foreach (Rigidbody body in player.GetComponentsInChildren<Rigidbody>(true)) mass += body.mass;
            Assert.AreEqual(61f, mass, 0.01f);
            foreach (CharacterJoint joint in player.GetComponentsInChildren<CharacterJoint>(true))
            {
                if (joint.name.EndsWith("LowerLeg"))
                    Assert.LessOrEqual(joint.lowTwistLimit.limit, -120f, "Run Apply Phase 2A Placeholder Physics.");
                if (joint.name.EndsWith("UpperLeg")) Assert.GreaterOrEqual(joint.highTwistLimit.limit, 80f);
                if (joint.name.EndsWith("UpperArm")) Assert.GreaterOrEqual(joint.highTwistLimit.limit, 125f);
            }
        }

    }
}
