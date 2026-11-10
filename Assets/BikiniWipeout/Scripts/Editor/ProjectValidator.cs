using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Static checks on project configuration and generated content. It verifies wiring, not feel:
    /// it cannot tell whether the game plays well, only whether everything needed to play is in place.
    ///
    /// Menu: Bikini Wipeout > Validate Project
    /// Batch: -executeMethod BikiniWipeout.EditorTools.ProjectValidator.ValidateBatch
    /// </summary>
    public static class ProjectValidator
    {
        public const string ExpectedEditorVersion = "6000.6";

        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Passed = new List<string>();

            public bool Ok => Errors.Count == 0;

            public override string ToString()
            {
                var lines = new List<string>
                {
                    $"Bikini Wipeout validation: {Passed.Count} passed, {Warnings.Count} warnings, {Errors.Count} errors"
                };
                foreach (string error in Errors) lines.Add("  ERROR   " + error);
                foreach (string warning in Warnings) lines.Add("  WARNING " + warning);
                foreach (string pass in Passed) lines.Add("  ok      " + pass);
                return string.Join("\n", lines);
            }
        }

        [MenuItem("Bikini Wipeout/Validate Project", priority = 40)]
        public static void ValidateMenu()
        {
            Report report = Run();
            if (report.Ok)
            {
                Debug.Log(report.ToString());
            }
            else
            {
                Debug.LogError(report.ToString());
            }

            EditorUtility.DisplayDialog("Bikini Wipeout validation",
                $"{report.Passed.Count} checks passed\n{report.Warnings.Count} warnings\n{report.Errors.Count} errors\n\nDetails are in the Console.",
                "OK");
        }

        /// <summary>Command-line entry point. Exit code 0 = no errors, 1 = errors.</summary>
        public static void ValidateBatch()
        {
            Report report = Run();
            Debug.Log(report.ToString());
            EditorApplication.Exit(report.Ok ? 0 : 1);
        }

        public static Report Run()
        {
            var report = new Report();
            CheckEditorAndPipeline(report);
            CheckLayers(report);
            CheckInput(report);
            CheckAssets(report);
            CheckPrefabs(report);
            CheckScenes(report);
            return report;
        }

        private static void Check(Report report, bool condition, string description)
        {
            if (condition)
            {
                report.Passed.Add(description);
            }
            else
            {
                report.Errors.Add(description);
            }
        }

        private static void CheckEditorAndPipeline(Report report)
        {
            if (Application.unityVersion.StartsWith(ExpectedEditorVersion, StringComparison.Ordinal))
            {
                report.Passed.Add("Editor version " + Application.unityVersion);
            }
            else
            {
                report.Warnings.Add($"Editor is {Application.unityVersion}; the project targets {ExpectedEditorVersion}.x");
            }

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            Check(report, pipeline != null && pipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"),
                "Universal Render Pipeline is the active render pipeline");
            Check(report, Shader.Find("Universal Render Pipeline/Lit") != null, "URP Lit shader is available");
        }

        private static void CheckLayers(Report report)
        {
            bool namesOk = true;
            foreach (int layer in GameLayers.GameplayLayers)
            {
                if (LayerMask.LayerToName(layer) != GameLayers.NameOf(layer))
                {
                    namesOk = false;
                    report.Errors.Add($"Layer {layer} should be named '{GameLayers.NameOf(layer)}' but is '{LayerMask.LayerToName(layer)}'");
                }
            }

            if (namesOk)
            {
                report.Passed.Add("Physics layer names match GameLayers");
            }

            int mismatches = 0;
            foreach (int a in GameLayers.GameplayLayers)
            {
                for (int b = 0; b < 32; b++)
                {
                    if (Physics.GetIgnoreLayerCollision(a, b) == GameLayers.Collides(a, b))
                    {
                        mismatches++;
                    }
                }
            }

            Check(report, mismatches == 0, "Layer collision matrix matches GameLayers rules" +
                                            (mismatches == 0 ? string.Empty : $" ({mismatches} pairs differ)"));
        }

        private static void CheckInput(Report report)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Foundation1Builder.InputActionsPath);
            Check(report, asset != null, "Input actions asset exists");
            if (asset == null)
            {
                return;
            }

            InputActionMap map = asset.FindActionMap(PlayerInputReader.ActionMapName);
            Check(report, map != null, "Input actions contain the 'Player' map");
            if (map == null)
            {
                return;
            }

            foreach (string action in new[]
                     {
                         PlayerInputReader.MoveActionName, PlayerInputReader.LookActionName, PlayerInputReader.JumpActionName,
                         PlayerInputReader.RunActionName, PlayerInputReader.PauseActionName
                     })
            {
                InputAction found = map.FindAction(action);
                Check(report, found != null && found.bindings.Count > 0, $"Input action 'Player/{action}' has bindings");
            }
        }

        private static void CheckAssets(Report report)
        {
            foreach (string path in new[]
                     {
                         Foundation1Builder.MovementConfigPath, Foundation1Builder.KnockdownConfigPath,
                         Foundation1Builder.RagdollConfigPath, Foundation1Builder.CameraConfigPath
                     })
            {
                Check(report, AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null, "Config asset " + Path.GetFileName(path));
            }

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(Foundation1Builder.AudioLibraryPath);
            Check(report, library != null, "Audio library asset exists");
            if (library != null)
            {
                List<string> missing = library.GetMissingClipReport();
                if (missing.Count == 0)
                {
                    report.Passed.Add("Every audio event and music track has a clip");
                }
                else
                {
                    report.Warnings.Add("Audio events without clips (hooks stay active but silent): " + string.Join(", ", missing));
                }
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Foundation1Builder.AnimatorControllerPath);
            Check(report, controller != null, "Animator controller exists");
            if (controller != null)
            {
                var stateNames = new HashSet<string>();
                foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                {
                    stateNames.Add(child.state.name);
                }

                foreach (string name in Enum.GetNames(typeof(CharacterAnimState)))
                {
                    Check(report, stateNames.Contains(name), $"Animator has state '{name}'");
                }

                report.Warnings.Add("Animator states use empty placeholder clips; placeholder limb motion is procedural (expected for Foundation 1).");
            }
        }

        private static void CheckPrefabs(Report report)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Foundation1Builder.PlayerPrefabPath);
            Check(report, player != null, "Player prefab exists");
            if (player != null)
            {
                Check(report, player.GetComponent<CharacterController>() != null, "Player has a CharacterController");
                Check(report, player.GetComponent<CharacterMotor>() != null && player.GetComponent<CharacterMotor>().config != null,
                    "Player motor has a movement config");
                Check(report, player.GetComponent<PlayerInputReader>() != null, "Player has an input reader");
                var knockdown = player.GetComponent<KnockdownController>();
                Check(report, knockdown != null && knockdown.config != null && knockdown.ragdoll != null && knockdown.motor != null,
                    "Player knockdown controller is wired");
                var ragdoll = player.GetComponent<RagdollController>();
                Check(report, ragdoll != null && ragdoll.config != null && ragdoll.ragdollRoot != null && ragdoll.rootBody != null,
                    "Player ragdoll controller is wired");
                Check(report, player.GetComponent<PlayerRespawner>() != null, "Player has a respawner");
                Check(report, player.GetComponentInChildren<Animator>(true) != null &&
                              player.GetComponentInChildren<Animator>(true).runtimeAnimatorController != null,
                    "Player model has an Animator with a controller");

                int bodies = ragdoll != null && ragdoll.ragdollRoot != null
                    ? ragdoll.ragdollRoot.GetComponentsInChildren<Rigidbody>(true).Length
                    : 0;
                int joints = player.GetComponentsInChildren<CharacterJoint>(true).Length;
                Check(report, bodies >= 11, $"Ragdoll has at least 11 rigidbodies (found {bodies})");
                Check(report, joints == bodies - 1, $"Every ragdoll body except the root has a joint (bodies {bodies}, joints {joints})");
                Check(report, player.layer == GameLayers.Player, "Player root is on the Player layer");
            }

            var sweeper = AssetDatabase.LoadAssetAtPath<GameObject>(Foundation1Builder.SweeperPrefabPath);
            Check(report, sweeper != null && sweeper.GetComponent<RotatingSweeper>() != null &&
                          sweeper.GetComponentInChildren<ImpactSource>(true) != null, "Rotating sweeper prefab is complete");

            var ball = AssetDatabase.LoadAssetAtPath<GameObject>(Foundation1Builder.BallPrefabPath);
            Check(report, ball != null && ball.GetComponent<BouncingBall>() != null && ball.GetComponent<Rigidbody>() != null &&
                          ball.GetComponent<ImpactSource>() != null, "Bouncing ball prefab is complete");

            var platform = AssetDatabase.LoadAssetAtPath<GameObject>(Foundation1Builder.MovingPlatformPrefabPath);
            Check(report, platform != null && platform.GetComponent<MovingPlatform>() != null &&
                          platform.GetComponent<Rigidbody>() != null && platform.GetComponent<Rigidbody>().isKinematic,
                "Moving platform prefab is complete");

            foreach (string path in new[]
                     {
                         Foundation1Builder.CheckpointPrefabPath, Foundation1Builder.FinishLinePrefabPath,
                         Foundation1Builder.WaterPrefabPath, Foundation1Builder.SplashPrefabPath,
                         Foundation1Builder.AudioManagerPrefabPath
                     })
            {
                Check(report, AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, "Prefab " + Path.GetFileName(path));
            }
        }

        private static void CheckScenes(Report report)
        {
            Check(report, File.Exists(Foundation1Builder.MainMenuScenePath), "Main menu scene exists");
            Check(report, File.Exists(Foundation1Builder.CourseScenePath), "Course scene exists");

            var enabled = new HashSet<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    enabled.Add(scene.path);
                }
            }

            Check(report, enabled.Contains(Foundation1Builder.MainMenuScenePath), "Main menu scene is in Build Settings");
            Check(report, enabled.Contains(Foundation1Builder.CourseScenePath), "Course scene is in Build Settings");
        }
    }
}
