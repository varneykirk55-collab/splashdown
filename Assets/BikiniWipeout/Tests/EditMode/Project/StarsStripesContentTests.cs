using System.Linq;
using BikiniWipeout.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BikiniWipeout.Tests
{
    public class StarsStripesContentTests
    {
        [Test]
        public void Prefab_HasCharDAvatarMaterialsAndCompleteRagdoll()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarsStripesIntegration.PrefabPath);
            Assert.IsNotNull(prefab, "Run StarsStripesIntegration.Integrate first.");
            Assert.IsNotNull(prefab.GetComponent<PlayerCharacter>());
            Assert.IsNotNull(prefab.GetComponent<CharacterMotor>());
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator.avatar.isValid && animator.avatar.isHuman);
            Assert.AreEqual(StarsStripesIntegration.ModelPath, AssetDatabase.GetAssetPath(animator.avatar));
            Assert.AreEqual(StarsStripesIntegration.ControllerPath, AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual(11, prefab.GetComponentsInChildren<Rigidbody>(true).Length);
            Assert.AreEqual(10, prefab.GetComponentsInChildren<CharacterJoint>(true).Length);
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Assert.IsNotNull(renderer.sharedMesh);
                Assert.IsTrue(renderer.bones.All(b => b != null));
                Assert.AreEqual(2, renderer.sharedMaterials.Length);
                foreach (var material in renderer.sharedMaterials)
                {
                    StringAssert.StartsWith("Assets/BikiniWipeout/Characters/StarsStripes/Materials/", AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    Assert.IsNotNull(material.GetTexture("_BaseMap"));
                    Assert.IsNotNull(material.GetTexture("_MetallicGlossMap"));
                }
            }
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        }

        [Test]
        public void Controller_PreservesJumpAndRecoveryTiming_AndOwnsItsStateMachine()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(StarsStripesIntegration.ControllerPath);
            Assert.IsNotNull(controller);
            var machine = controller.layers[0].stateMachine;
            Assert.AreEqual(StarsStripesIntegration.ControllerPath, AssetDatabase.GetAssetPath(machine));
            foreach (string name in new[] { "Idle", "Locomotion", "Jump", "Fall", "Land", "Knockdown", "RecoverFaceUp", "RecoverFaceDown" })
            {
                var state = machine.states.Single(s => s.state.name == name).state;
                Assert.IsNotNull(state.motion, name);
                if (name.StartsWith("Recover"))
                    Assert.That(((AnimationClip)state.motion).length / state.speed, Is.EqualTo(2.1f).Within(0.001f));
            }
            var jump = (AnimationClip)machine.states.Single(s => s.state.name == "Jump").state.motion;
            var settings = AnimationUtility.GetAnimationClipSettings(jump);
            Assert.IsFalse(settings.loopBlendPositionY, "Motor owns the jump arc.");
            Assert.IsFalse(settings.keepOriginalPositionXZ, "Discard authored travel offset.");
        }
    }
}
