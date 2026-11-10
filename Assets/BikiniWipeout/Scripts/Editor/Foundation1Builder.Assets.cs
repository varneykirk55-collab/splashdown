using System;
using System.Collections.Generic;
using BikiniWipeout.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BikiniWipeout.EditorTools
{
    public static partial class Foundation1Builder
    {
        public const string AnimatorControllerPath = AnimationsFolder + "/PlaceholderCharacter.controller";
        public const string AudioLibraryPath = SettingsFolder + "/AudioLibrary.asset";
        public const string MovementConfigPath = SettingsFolder + "/PlayerMovementConfig.asset";
        public const string KnockdownConfigPath = SettingsFolder + "/KnockdownConfig.asset";
        public const string RagdollConfigPath = SettingsFolder + "/RagdollConfig.asset";
        public const string CameraConfigPath = SettingsFolder + "/CameraConfig.asset";
        public const string PlaceholderAudioFolder = AudioFolder + "/Placeholder";

        /// <summary>Shared assets referenced by prefabs and scenes.</summary>
        internal sealed class ContentAssets
        {
            public Material Sand, Turquoise, HotPink, Yellow, Orange, Lime, Purple, White, Dark, Red;
            public Material Water, Skin, Suit, Hair, Splash;
            public PlayerMovementConfig Movement;
            public KnockdownConfig Knockdown;
            public RagdollConfig Ragdoll;
            public CameraConfig Camera;
            public AudioLibrary Audio;
            public RuntimeAnimatorController Animator;
            public InputActionAsset InputActions;
        }

        private static ContentAssets BuildSharedAssets()
        {
            var assets = new ContentAssets
            {
                // Temporary tropical palette: saturated inflatable colours with clear contrast between surfaces.
                Sand = LitMaterial("Sand", new Color(0.98f, 0.85f, 0.55f), 0.25f),
                Turquoise = LitMaterial("Turquoise", new Color(0.13f, 0.80f, 0.76f), 0.5f),
                HotPink = LitMaterial("HotPink", new Color(1.00f, 0.24f, 0.58f), 0.6f),
                Yellow = LitMaterial("SunYellow", new Color(1.00f, 0.84f, 0.12f), 0.6f),
                Orange = LitMaterial("Orange", new Color(1.00f, 0.52f, 0.10f), 0.6f),
                Lime = LitMaterial("Lime", new Color(0.52f, 0.90f, 0.22f), 0.55f),
                Purple = LitMaterial("Purple", new Color(0.60f, 0.34f, 0.95f), 0.6f),
                White = LitMaterial("White", new Color(0.96f, 0.96f, 0.96f), 0.4f),
                Dark = LitMaterial("DarkSupport", new Color(0.20f, 0.27f, 0.36f), 0.2f),
                Red = LitMaterial("FinishRed", new Color(0.95f, 0.18f, 0.22f), 0.55f),
                Skin = LitMaterial("PlaceholderSkin", new Color(0.87f, 0.63f, 0.48f), 0.3f),
                Suit = LitMaterial("PlaceholderSuit", new Color(1.00f, 0.20f, 0.52f), 0.45f),
                Hair = LitMaterial("PlaceholderHair", new Color(0.33f, 0.19f, 0.11f), 0.35f),
                Water = WaterMaterial(),
                Splash = SplashMaterial(),

                Movement = LoadOrCreate(MovementConfigPath, ScriptableObject.CreateInstance<PlayerMovementConfig>),
                Knockdown = LoadOrCreate(KnockdownConfigPath, ScriptableObject.CreateInstance<KnockdownConfig>),
                Ragdoll = LoadOrCreate(RagdollConfigPath, ScriptableObject.CreateInstance<RagdollConfig>),
                Camera = LoadOrCreate(CameraConfigPath, ScriptableObject.CreateInstance<CameraConfig>),
                InputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath)
            };

            if (assets.InputActions == null)
            {
                Debug.LogWarning("[Bikini Wipeout] Input actions asset not found at " + InputActionsPath +
                                 ". The player will fall back to built-in default bindings.");
            }

            assets.Audio = BuildAudioLibrary();
            assets.Animator = BuildAnimatorController();
            AssetDatabase.SaveAssets();
            return assets;
        }

        // ------------------------------------------------------------------ materials

        private static Material LitMaterial(string name, Color color, float smoothness)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            return LoadOrCreate(path, () =>
            {
                var material = new Material(Shader.Find(LitShaderName)) { name = name };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
                return material;
            });
        }

        private static Material WaterMaterial()
        {
            return LoadOrCreate(MaterialsFolder + "/Water.mat", () =>
            {
                // URP Lit set up as a transparent surface (the same values the material inspector writes).
                var material = new Material(Shader.Find(LitShaderName)) { name = "Water" };
                material.SetColor("_BaseColor", new Color(0.06f, 0.52f, 0.86f, 0.86f));
                material.SetFloat("_Smoothness", 0.93f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("DepthOnly", false);
                material.SetShaderPassEnabled("ShadowCaster", false);
                return material;
            });
        }

        private static Material SplashMaterial()
        {
            return LoadOrCreate(MaterialsFolder + "/Splash.mat", () =>
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find(LitShaderName);
                }

                var material = new Material(shader) { name = "Splash" };
                material.SetColor("_BaseColor", new Color(0.88f, 0.96f, 1f, 1f));
                return material;
            });
        }

        // ------------------------------------------------------------------ audio

        private static AudioLibrary BuildAudioLibrary()
        {
            return LoadOrCreate(AudioLibraryPath, () =>
            {
                var library = ScriptableObject.CreateInstance<AudioLibrary>();
                library.sfx = new List<AudioLibrary.SfxEntry>
                {
                    Sfx(AudioEventId.Footstep, 0.5f, 0.9f, 1.1f, 0.3f, 0.08f, "sfx_footstep_01", "sfx_footstep_02"),
                    Sfx(AudioEventId.Jump, 0.6f, 0.95f, 1.05f, 0.2f, 0f, "sfx_jump"),
                    Sfx(AudioEventId.Land, 0.8f, 0.92f, 1.06f, 0.3f, 0.05f, "sfx_land"),
                    Sfx(AudioEventId.ObstacleImpact, 1f, 0.9f, 1.1f, 0.3f, 0.1f, "sfx_obstacle_impact"),
                    Sfx(AudioEventId.RagdollCollision, 0.7f, 0.85f, 1.15f, 0.35f, 0.08f, "sfx_body_thud_01", "sfx_body_thud_02"),
                    Sfx(AudioEventId.WaterSplash, 1f, 0.94f, 1.06f, 0.3f, 0.2f, "sfx_water_splash"),
                    Sfx(AudioEventId.CheckpointActivated, 0.8f, 1f, 1f, 0f, 0f, "sfx_checkpoint"),
                    Sfx(AudioEventId.FinishCelebration, 0.9f, 1f, 1f, 0f, 0f, "sfx_finish"),
                    Sfx(AudioEventId.UiClick, 0.7f, 1f, 1f, 0f, 0f, "sfx_ui_click"),
                    Sfx(AudioEventId.UiHover, 0.4f, 1f, 1f, 0f, 0.06f, "sfx_ui_hover"),
                    Sfx(AudioEventId.CountdownTick, 0.7f, 1f, 1f, 0f, 0f, "sfx_countdown_tick"),
                    Sfx(AudioEventId.CountdownGo, 0.8f, 1f, 1f, 0f, 0f, "sfx_countdown_go")
                };

                library.music = new List<AudioLibrary.MusicEntry>
                {
                    new AudioLibrary.MusicEntry { id = MusicTrackId.MainMenu, clip = LoadClip("music_menu_loop"), volume = 0.8f, loop = true },
                    new AudioLibrary.MusicEntry { id = MusicTrackId.Course, clip = LoadClip("music_course_loop"), volume = 0.8f, loop = true }
                };

                return library;
            });
        }

        private static AudioLibrary.SfxEntry Sfx(AudioEventId id, float volume, float pitchMin, float pitchMax,
            float spatialBlend, float minInterval, params string[] clipNames)
        {
            var clips = new List<AudioClip>();
            foreach (string clipName in clipNames)
            {
                AudioClip clip = LoadClip(clipName);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            return new AudioLibrary.SfxEntry
            {
                id = id,
                clips = clips.ToArray(),
                volume = volume,
                pitchRange = new Vector2(pitchMin, pitchMax),
                spatialBlend = spatialBlend,
                minInterval = minInterval
            };
        }

        private static AudioClip LoadClip(string fileName)
        {
            string path = PlaceholderAudioFolder + "/" + fileName + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning("[Bikini Wipeout] Placeholder audio clip missing: " + path);
            }

            return clip;
        }

        // ------------------------------------------------------------------ animator

        /// <summary>
        /// Builds the Animator controller with all Foundation 1 states, parameters and transitions.
        /// Every state holds an EMPTY placeholder clip: this is the state machine only, not animation
        /// coverage. Assign real clips to the states (or use an Animator Override Controller) later.
        /// </summary>
        private static RuntimeAnimatorController BuildAnimatorController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            if (existing != null)
            {
                return existing;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MotionBlend", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("KnockedDown", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Recovering", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);

            // Grounded defaults to true so the character does not start in the Fall state.
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "Grounded")
                {
                    parameters[i].defaultBool = true;
                }
            }

            controller.parameters = parameters;

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = AddState(machine, CharacterAnimState.Idle, true, new Vector3(260, 0));
            AnimatorState walk = AddState(machine, CharacterAnimState.Walk, true, new Vector3(520, -60));
            AnimatorState run = AddState(machine, CharacterAnimState.Run, true, new Vector3(780, 0));
            AnimatorState jump = AddState(machine, CharacterAnimState.Jump, false, new Vector3(260, 160));
            AnimatorState fall = AddState(machine, CharacterAnimState.Fall, true, new Vector3(520, 220));
            AnimatorState land = AddState(machine, CharacterAnimState.Land, false, new Vector3(780, 160));
            AnimatorState knockdown = AddState(machine, CharacterAnimState.Knockdown, true, new Vector3(260, 340));
            AnimatorState recover = AddState(machine, CharacterAnimState.Recover, false, new Vector3(520, 400));
            machine.defaultState = idle;

            // Thresholds are in m/s and mirror AnimationStateController (walk 3.2, run 6.5 by default).
            const float idleSpeed = 0.2f;
            const float runSpeed = 4.85f;

            // Knockdown has the highest priority, so it is added first.
            AnimatorStateTransition toKnockdown = machine.AddAnyStateTransition(knockdown);
            Configure(toKnockdown, 0.05f);
            toKnockdown.canTransitionToSelf = false;
            toKnockdown.AddCondition(AnimatorConditionMode.If, 0f, "KnockedDown");

            AnimatorStateTransition toJump = machine.AddAnyStateTransition(jump);
            Configure(toJump, 0.05f);
            toJump.canTransitionToSelf = false;
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "KnockedDown");
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Recovering");

            Link(idle, walk, 0.12f).AddCondition(AnimatorConditionMode.Greater, idleSpeed, "Speed");
            Link(walk, idle, 0.15f).AddCondition(AnimatorConditionMode.Less, idleSpeed, "Speed");
            Link(walk, run, 0.15f).AddCondition(AnimatorConditionMode.Greater, runSpeed, "Speed");
            Link(run, walk, 0.15f).AddCondition(AnimatorConditionMode.Less, runSpeed, "Speed");

            foreach (AnimatorState grounded in new[] { idle, walk, run, land })
            {
                Link(grounded, fall, 0.12f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            }

            Link(jump, fall, 0.15f).AddCondition(AnimatorConditionMode.Less, 0f, "VerticalSpeed");
            Link(jump, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            Link(fall, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            AnimatorStateTransition landToIdle = land.AddTransition(idle);
            landToIdle.hasExitTime = true;
            landToIdle.exitTime = 0.9f;
            landToIdle.duration = 0.1f;
            landToIdle.hasFixedDuration = true;

            Link(knockdown, recover, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Recovering");
            AnimatorStateTransition knockdownToIdle = Link(knockdown, idle, 0f);
            knockdownToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "KnockedDown");
            knockdownToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Recovering");
            Link(recover, idle, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Recovering");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, CharacterAnimState state, bool loop, Vector3 position)
        {
            AnimatorState animatorState = machine.AddState(state.ToString(), position);
            animatorState.motion = PlaceholderClip(state.ToString(), loop);
            animatorState.writeDefaultValues = true;
            return animatorState;
        }

        private static AnimationClip PlaceholderClip(string stateName, bool loop)
        {
            string path = AnimationsFolder + "/Placeholder/Placeholder_" + stateName + ".anim";
            return LoadOrCreate(path, () =>
            {
                // Intentionally empty. Replace with a real humanoid clip of the same purpose.
                var clip = new AnimationClip { name = "Placeholder_" + stateName, frameRate = 30f };
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                return clip;
            });
        }

        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            Configure(transition, duration);
            return transition;
        }

        private static void Configure(AnimatorStateTransition transition, float duration)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.offset = 0f;
        }
    }
}
