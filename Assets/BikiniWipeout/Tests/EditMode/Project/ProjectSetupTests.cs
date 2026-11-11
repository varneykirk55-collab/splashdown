using BikiniWipeout.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BikiniWipeout.Tests
{
    /// <summary>Project configuration checks that need the Unity editor but no generated content.</summary>
    public class ProjectSetupTests
    {
        [Test]
        public void CollisionRules_AreSymmetric()
        {
            for (int a = 0; a < 32; a++)
            {
                for (int b = 0; b < 32; b++)
                {
                    Assert.AreEqual(GameLayers.Collides(a, b), GameLayers.Collides(b, a), $"layers {a} / {b}");
                }
            }
        }

        [Test]
        public void CollisionRules_MatchTheDesign()
        {
            Assert.IsFalse(GameLayers.Collides(GameLayers.Player, GameLayers.Ragdoll), "The capsule must not push its own ragdoll.");
            Assert.IsFalse(GameLayers.Collides(GameLayers.Ragdoll, GameLayers.Ragdoll), "Ragdoll self-collision is off for stability.");
            Assert.IsTrue(GameLayers.Collides(GameLayers.Player, GameLayers.Obstacle));
            Assert.IsTrue(GameLayers.Collides(GameLayers.Ragdoll, GameLayers.Obstacle));
            Assert.IsTrue(GameLayers.Collides(GameLayers.Player, GameLayers.MovingPlatform));
            Assert.IsTrue(GameLayers.Collides(GameLayers.Ragdoll, GameLayers.Ground));
            Assert.IsTrue(GameLayers.Collides(GameLayers.Player, GameLayers.Trigger));
            Assert.IsTrue(GameLayers.Collides(GameLayers.Ragdoll, GameLayers.Trigger));
            Assert.IsFalse(GameLayers.Collides(GameLayers.Obstacle, GameLayers.Trigger), "Balls must not set off water or checkpoints.");
            Assert.IsFalse(GameLayers.Collides(GameLayers.Default, GameLayers.Trigger));
            Assert.IsTrue(GameLayers.Collides(GameLayers.Obstacle, GameLayers.Boundary));
        }

        [Test]
        public void LayerNames_InProjectSettings_MatchGameLayers()
        {
            foreach (int layer in GameLayers.GameplayLayers)
            {
                Assert.AreEqual(GameLayers.NameOf(layer), LayerMask.LayerToName(layer), "layer " + layer);
            }
        }

        [Test]
        public void PhysicsMatrix_MatchesRules_AfterApply()
        {
            GameLayers.ApplyCollisionMatrix();
            foreach (int a in GameLayers.GameplayLayers)
            {
                for (int b = 0; b < 32; b++)
                {
                    Assert.AreEqual(!GameLayers.Collides(a, b), Physics.GetIgnoreLayerCollision(a, b), $"layers {a} / {b}");
                }
            }
        }

        [Test]
        public void Masks_ContainTheExpectedLayers()
        {
            Assert.AreNotEqual(0, GameLayers.StandableMask & (1 << GameLayers.MovingPlatform));
            Assert.AreEqual(0, GameLayers.SafeGroundMask & (1 << GameLayers.Obstacle), "Never recover on top of an obstacle.");
            Assert.AreEqual(0, GameLayers.SolidMask & (1 << GameLayers.Ragdoll));
            Assert.AreEqual(0, GameLayers.SolidMask & (1 << GameLayers.Trigger));
        }

        [Test]
        public void DefaultInputMap_HasAllGameplayActions()
        {
            InputActionMap map = PlayerInputReader.CreateDefaultMap();
            try
            {
                AssertPlayerMap(map);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void InputActionsAsset_HasAllGameplayActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Foundation1Builder.InputActionsPath);
            Assert.IsNotNull(asset, "Missing " + Foundation1Builder.InputActionsPath);
            AssertPlayerMap(asset.FindActionMap(PlayerInputReader.ActionMapName));
            Assert.IsNotNull(asset.FindActionMap("UI"), "The asset also carries the UI map used by the event system.");
        }

        [Test]
        public void ZoomAndGamepad_BindingsExistInBothInputSources()
        {
            InputActionMap defaults = PlayerInputReader.CreateDefaultMap();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Foundation1Builder.InputActionsPath);
            try
            {
                foreach (InputActionMap map in new[] { defaults, asset.FindActionMap(PlayerInputReader.ActionMapName) })
                {
                    var paths = new System.Collections.Generic.List<string>();
                    foreach (InputBinding binding in map.FindAction(PlayerInputReader.ZoomActionName).bindings)
                    {
                        paths.Add(binding.path);
                    }

                    CollectionAssert.Contains(paths, "<Mouse>/scroll/y");
                    CollectionAssert.Contains(paths, "<Gamepad>/dpad/up");
                    CollectionAssert.Contains(paths, "<Gamepad>/dpad/down");
                    CollectionAssert.Contains(paths, "<Keyboard>/minus");

                    var gamepadActions = new System.Collections.Generic.HashSet<string>();
                    foreach (InputAction action in map.actions)
                    {
                        foreach (InputBinding binding in action.bindings)
                        {
                            if (binding.path.StartsWith("<Gamepad>"))
                            {
                                gamepadActions.Add(action.name);
                            }
                        }
                    }

                    foreach (string name in new[] { "Move", "Look", "Jump", "Sprint", "Pause", "Zoom" })
                    {
                        Assert.IsTrue(gamepadActions.Contains(name), name + " needs a gamepad binding");
                    }
                }
            }
            finally
            {
                defaults.Dispose();
            }
        }

        [Test]
        public void PlaceholderAudioClips_AreImported()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Foundation1Builder.PlaceholderAudioFolder });
            Assert.GreaterOrEqual(guids.Length, 16, "Expected the synthesized placeholder clips. Run Tools/generate_placeholder_audio.py.");
        }

        private static void AssertPlayerMap(InputActionMap map)
        {
            Assert.IsNotNull(map, "Player action map");
            foreach (string name in new[]
                     {
                         PlayerInputReader.MoveActionName, PlayerInputReader.LookActionName, PlayerInputReader.JumpActionName,
                         PlayerInputReader.RunActionName, PlayerInputReader.PauseActionName, PlayerInputReader.ZoomActionName
                     })
            {
                InputAction action = map.FindAction(name);
                Assert.IsNotNull(action, "action " + name);
                Assert.Greater(action.bindings.Count, 0, "bindings of " + name);
            }
        }
    }
}
