using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Phase 2B: imports the Grok-fixed charA_cowgirl (Mixamo-named humanoid) and the Mixamo "Without Skin"
    /// clips, then builds a separate Player_Cowgirl prefab and a Course_Foundation1_Cowgirl scene copy.
    ///
    /// The placeholder prefab, the original course scene and all gameplay code are left untouched.
    /// Safe to re-run: import settings are re-applied, materials/override controller are updated in place,
    /// and the cowgirl prefab and scene copy are regenerated.
    /// </summary>
    public static class CowgirlIntegration
    {
        private const string Root = "Assets/BikiniWipeout";
        private const string CharacterFolder = Root + "/Characters/Cowgirl";
        public const string ModelPath = CharacterFolder + "/charA_cowgirl.fbx";
        private const string TextureFolder = CharacterFolder + "/Textures";
        private const string MaterialFolder = CharacterFolder + "/Materials";
        private const string ClipFolder = Root + "/Animations/Cowgirl";
        public const string ControllerPath = ClipFolder + "/Cowgirl.controller";
        private const string LegacyOverrideControllerPath = ClipFolder + "/Cowgirl.overrideController";
        private const string BaseControllerPath = Root + "/Animations/PlaceholderCharacter.controller";
        private const string BaseKnockdownConfigPath = Root + "/Settings/KnockdownConfig.asset";
        public const string KnockdownConfigPath = Root + "/Settings/KnockdownConfig_Cowgirl.asset";

        /// <summary>
        /// Recovery length for the cowgirl. Both get-up clips are time-scaled to exactly this, and her own
        /// KnockdownConfig copy uses it so movement unlocks as she finishes standing.
        /// </summary>
        public const float RecoveryDuration = 2.1f;
        public const string PrefabPath = Root + "/Prefabs/Characters/Player_Cowgirl.prefab";
        private const string SourceScenePath = Root + "/Scenes/Course_Foundation1.unity";
        public const string ScenePath = Root + "/Scenes/Course_Foundation1_Cowgirl.unity";

        private const string Bone = "mixamorig:";

        // Capsule sized to her: head top 1.60 m, hat crown 1.65 m.
        private const float CapsuleHeight = 1.65f;
        private const float FocusHeight = 1.0f;

        /// <summary>One clip cut from a Mixamo file. Frames are Mixamo/Blender frame numbers (first key = 1).</summary>
        private readonly struct ClipDef
        {
            public readonly string File, Name;
            public readonly int First, Last; // 0 = whole take
            public readonly bool Loop, BakeY, YFromFeet, RotationFromBody;

            public ClipDef(string file, string name, bool loop, bool bakeY, bool yFromFeet = false, int first = 0, int last = 0,
                bool rotationFromBody = false)
            {
                File = file; Name = name; Loop = loop; BakeY = bakeY; YFromFeet = yFromFeet; First = first; Last = last;
                RotationFromBody = rotationFromBody;
            }
        }

        // Root XZ is never baked: the Animator has Apply Root Motion off, so forward travel is discarded and the
        // CharacterMotor stays in charge of movement. Root rotation is always baked. Y is baked where the body
        // genuinely lowers (crouch, falling to the floor) and left unbaked for airborne clips so the motor's
        // jump arc is not doubled.
        private static readonly ClipDef[] Clips =
        {
            new ClipDef("Female Walk", "Cowgirl_Walk", loop: true, bakeY: true),
            new ClipDef("Slow Run", "Cowgirl_Run", loop: true, bakeY: true),
            new ClipDef("Running Jump (1)", "Cowgirl_Jump", loop: false, bakeY: false),
            // The running jump's own touchdown (feet contact ~f21, settles by f28): a light landing that flows back
            // into running. Replaces the deep Jumping Down crouch stand-in.
            new ClipDef("Running Jump (1)", "Cowgirl_Land", loop: false, bakeY: true, yFromFeet: true, first: 19, last: 28),
            new ClipDef("Idle", "Cowgirl_Idle", loop: true, bakeY: true),
            // Falling Idle is authored with the body yawed ~43°; root rotation from the body keeps her facing forward.
            new ClipDef("Falling Idle", "Cowgirl_Fall", loop: true, bakeY: false, rotationFromBody: true),
            // Get-ups. Their lying direction matches KnockdownController.ComputeGetUpFacing: face up lies with the
            // feet towards the final facing, face down with the head towards it. Y is baked (lying on the floor),
            // XZ is not, so the body stays centred on the stand point the knockdown logic chose.
            new ClipDef("Getting Up", "Cowgirl_GetUp_FaceUp", loop: false, bakeY: true, first: 1, last: 64),
            // Standing Up lies still for ~4.5 s first; trimmed to the 4 s rise (time-scaled in the controller).
            new ClipDef("Standing Up", "Cowgirl_GetUp_FaceDown", loop: false, bakeY: true, first: 136, last: 256),
            new ClipDef("Falling Down", "Cowgirl_Knockdown", loop: false, bakeY: true),
            // Imported for later use; not assigned to a state yet.
            new ClipDef("Female Start Walking", "Cowgirl_StartWalking", loop: false, bakeY: true),
            new ClipDef("Female Stop Walking", "Cowgirl_StopWalking", loop: false, bakeY: true),
            new ClipDef("Jumping Down", "Cowgirl_JumpingDown", loop: false, bakeY: true, yFromFeet: true),
            new ClipDef("Falling From Losing Balance", "Cowgirl_LoseBalance", loop: false, bakeY: true),
            new ClipDef("Falling Into Pool", "Cowgirl_FallIntoPool_A", loop: false, bakeY: true),
            new ClipDef("Falling Into Pool (1)", "Cowgirl_FallIntoPool_B", loop: false, bakeY: true),
            new ClipDef("Catwalk Twist L To Twist R", "Cowgirl_CatwalkTwist", loop: false, bakeY: true),
        };

        /// <summary>
        /// Controller state name → cowgirl clip. "Walk"/"Run" are replaced by one Locomotion blend tree and
        /// "Recover" is split into face-up / face-down states (see CreateController).
        /// </summary>
        private static readonly Dictionary<string, string> StateClips = new Dictionary<string, string>
        {
            { "Idle", "Cowgirl_Idle" },
            { "Jump", "Cowgirl_Jump" },
            { "Fall", "Cowgirl_Fall" },
            { "Land", "Cowgirl_Land" },
            { "Knockdown", "Cowgirl_Knockdown" },
        };

        public const string RecoverFaceUpState = "RecoverFaceUp";
        public const string RecoverFaceDownState = "RecoverFaceDown";

        [MenuItem("Bikini Wipeout/Phase 2B/Integrate Cowgirl (prefab + scene copy)")]
        public static void IntegrateMenu() => Integrate();

        /// <summary>Batch-mode entry point: -executeMethod BikiniWipeout.EditorTools.CowgirlIntegration.Integrate</summary>
        public static void Integrate()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Foundation1Builder.PlayerPrefabPath) == null)
                throw new System.InvalidOperationException("Player_Placeholder prefab missing. Run Bikini Wipeout > Build Foundation 1 Content (Missing Only) first.");

            ConfigureTextures();
            Material[] materials = CreateMaterials();
            Avatar avatar = ConfigureModel(materials);
            Dictionary<string, AnimationClip> clips = ConfigureClips();
            AnimatorController controller = CreateController(clips, HumanScale(avatar));
            KnockdownConfig knockdownConfig = CreateKnockdownConfig();
            GameObject prefab = BuildPrefab(avatar, controller, knockdownConfig);
            BuildScene(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[Phase 2B] Cowgirl integrated: " + PrefabPath + ", " + ScenePath);
        }

        // ------------------------------------------------------------------ textures and materials

        private static void ConfigureTextures()
        {
            foreach (string path in Directory.GetFiles(TextureFolder, "*.png").Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("_normal.png");
                bool data = path.EndsWith("_metallicSmoothness.png");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !(normal || data);
                importer.alphaSource = data ? TextureImporterAlphaSource.FromInput : importer.alphaSource;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static Material[] CreateMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new System.InvalidOperationException("URP Lit shader not found.");
            var result = new List<Material>();
            foreach (string part in new[] { "body", "hands" })
            {
                string name = "charA_cowgirl_" + part;
                string path = MaterialFolder + "/" + name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = lit;
                material.SetTexture("_BaseMap", Tex(name + "_albedo"));
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", Tex(name + "_normal"));
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
                // R = metallic, A = smoothness (1 - roughness), as packed by the Grok pipeline.
                material.SetTexture("_MetallicGlossMap", Tex(name + "_metallicSmoothness"));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
                EditorUtility.SetDirty(material);
                result.Add(material);
            }

            AssetDatabase.SaveAssets();
            return result.ToArray();
        }

        private static Texture2D Tex(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + name + ".png");
            if (texture == null) throw new FileNotFoundException("Missing texture " + name);
            return texture;
        }

        // ------------------------------------------------------------------ model

        private static Avatar ConfigureModel(Material[] materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (Material material in materials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), material);
            }

            importer.SaveAndReimport();

            // Her bind pose splays each leg ~1.8° outward (ankles wider than hips). The Avatar treats the reference
            // pose as "zero", so that splay was added to every step. Straighten only thighs/shins in the Avatar's
            // reference pose. Mesh, bind pose, skin weights and feet (heeled boots, pitched ~45°) are untouched.
            ApplyTPoseReference(ModelPath, legsOnly: true);

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new System.InvalidOperationException("Cowgirl Humanoid Avatar is not valid.");
            return avatar;
        }

        /// <summary>
        /// Rebuilds a model's Humanoid Avatar reference skeleton as a T-pose of the same skeleton.
        ///
        /// Mixamo "Without Skin" FBX files store the first animation frame as the skeleton's default pose
        /// (Female Walk: knees bent 47°/24°, arms down; Running Jump: knee 145°). Unity builds the clip's source
        /// Avatar from that pose, so knee/hip reference frames are wrong and the error is re-applied to every
        /// character the clip plays on: knees pointing ~90° sideways when bent, bow-legged gait, doubled stance
        /// width. Here each segment is turned by the minimal rotation onto its canonical T-pose direction (which
        /// keeps hinge axes such as the knee intact); bone offsets/lengths are unchanged.
        /// Character space: +Z forward, +Y up, character left = -X.
        /// </summary>
        private static void ApplyTPoseReference(string path, bool legsOnly)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            // Start from the file's own default pose so re-runs do not compound.
            HumanDescription reset = importer.humanDescription;
            reset.skeleton = new SkeletonBone[0];
            importer.humanDescription = reset;
            importer.SaveAndReimport();

            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform[] all = model.GetComponentsInChildren<Transform>(true);
                Transform B(string name) => all.FirstOrDefault(t => t.name == Bone + name);

                var chain = new List<(string bone, string child, Vector3 dir)>();
                Vector3 down = Vector3.down, up = Vector3.up, forward = Vector3.forward;
                foreach (string side in new[] { "Left", "Right" })
                {
                    chain.Add((side + "UpLeg", side + "Leg", down));
                    chain.Add((side + "Leg", side + "Foot", down));
                }

                if (!legsOnly)
                {
                    // Pelvis upright and square: spine up, hip axis along +X.
                    Transform hips = B("Hips");
                    Vector3 spineUp = (B("Spine").position - hips.position).normalized;
                    Vector3 hipRight = (B("RightUpLeg").position - B("LeftUpLeg").position).normalized;
                    Quaternion current = Quaternion.LookRotation(Vector3.Cross(hipRight, spineUp), spineUp);
                    hips.rotation = Quaternion.LookRotation(forward, up) * Quaternion.Inverse(current) * hips.rotation;

                    var torso = new List<(string, string, Vector3)>
                    {
                        ("Spine", "Spine1", up), ("Spine1", "Spine2", up), ("Spine2", "Neck", up), ("Neck", "Head", up), ("Head", "HeadTop_End", up),
                    };
                    Vector3 footDir = new Vector3(0f, -0.6f, 0.8f).normalized;
                    foreach (string side in new[] { "Left", "Right" })
                    {
                        chain.Add((side + "Foot", side + "ToeBase", footDir));
                        chain.Add((side + "ToeBase", side + "Toe_End", forward));
                        Vector3 lateral = side == "Left" ? Vector3.left : Vector3.right;
                        torso.Add((side + "Shoulder", side + "Arm", lateral));
                        torso.Add((side + "Arm", side + "ForeArm", lateral));
                        torso.Add((side + "ForeArm", side + "Hand", lateral));
                        string handReference = B(side + "HandMiddle1") != null ? side + "HandMiddle1" : side + "HandIndex1";
                        torso.Add((side + "Hand", handReference, lateral));
                        foreach (string digit in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
                            for (int i = 1; i <= 3; i++)
                                torso.Add((side + "Hand" + digit + i, side + "Hand" + digit + (i + 1), digit == "Thumb" ? (lateral + forward).normalized : lateral));
                    }

                    chain.InsertRange(0, torso);
                }

                // Lists are ordered parent → child, so each rotation sees its parents already corrected.
                foreach ((string bone, string child, Vector3 dir) in chain)
                {
                    Transform b = B(bone), c = B(child);
                    if (b == null || c == null) continue;
                    Vector3 segment = c.position - b.position;
                    if (segment.sqrMagnitude < 1e-12f) continue;
                    b.rotation = Quaternion.FromToRotation(segment.normalized, dir) * b.rotation;
                }

                if (!legsOnly)
                {
                    // Stand the reference pose: hips centred over the root with the lowest foot point on the ground.
                    // Without this, clips whose first frame is lying down (the get-ups) kept the hips at floor height
                    // with the straightened legs through the ground, which corrupts Unity's body-height normalisation.
                    Transform hipsBone = B("Hips");
                    float lowest = all.Where(t => t.name.Contains("Foot") || t.name.Contains("Toe")).Min(t => t.position.y);
                    Vector3 p = hipsBone.position;
                    hipsBone.position = new Vector3(0f, p.y - lowest, 0f);
                }

                HumanDescription description = importer.humanDescription;
                Dictionary<string, Transform> byName = all.GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                description.skeleton = description.skeleton.Select(s =>
                {
                    if (byName.TryGetValue(s.name, out Transform t) && t != model.transform)
                    {
                        s.position = t.localPosition;
                        s.rotation = t.localRotation;
                        s.scale = t.localScale;
                    }

                    return s;
                }).ToArray();
                importer.humanDescription = description;
                importer.SaveAndReimport();
            }
            finally
            {
                Object.DestroyImmediate(model);
            }

            Avatar check = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (check == null || !check.isValid || !check.isHuman)
                throw new System.InvalidOperationException("Avatar invalid after T-pose reference correction: " + path);
        }

        // ------------------------------------------------------------------ clips

        private static Dictionary<string, AnimationClip> ConfigureClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (IGrouping<string, ClipDef> group in Clips.GroupBy(c => c.File))
            {
                string path = ClipFolder + "/" + group.Key + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new FileNotFoundException("Missing animation " + path);

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.clipAnimations = new ModelImporterClipAnimation[0];
                importer.SaveAndReimport();
                ApplyTPoseReference(path, legsOnly: false);
                importer = (ModelImporter)AssetImporter.GetAtPath(path);

                ModelImporterClipAnimation template = importer.defaultClipAnimations[0];
                // Mixamo frame 1 is the importer's first frame.
                float frameOffset = template.firstFrame - 1f;
                var definitions = new List<ModelImporterClipAnimation>();
                foreach (ClipDef def in group)
                {
                    definitions.Add(new ModelImporterClipAnimation
                    {
                        name = def.Name,
                        takeName = template.takeName,
                        firstFrame = def.First > 0 ? def.First + frameOffset : template.firstFrame,
                        lastFrame = def.Last > 0 ? def.Last + frameOffset : template.lastFrame,
                        loopTime = def.Loop,
                        loopPose = def.Loop,
                        lockRootRotation = true,
                        keepOriginalOrientation = !def.RotationFromBody,
                        lockRootHeightY = def.BakeY,
                        keepOriginalPositionY = !def.YFromFeet,
                        heightFromFeet = def.YFromFeet,
                        lockRootPositionXZ = false,
                        keepOriginalPositionXZ = true,
                    });
                }

                importer.clipAnimations = definitions.ToArray();
                importer.SaveAndReimport();

                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (!clip.name.StartsWith("__preview__")) clips[clip.name] = clip;
                }
            }

            foreach (ClipDef def in Clips)
            {
                if (!clips.ContainsKey(def.Name)) throw new System.InvalidOperationException("Clip not imported: " + def.Name);
                if (!clips[def.Name].isHumanMotion) throw new System.InvalidOperationException("Clip is not humanoid: " + def.Name);
            }

            return clips;
        }

        /// <summary>
        /// Builds Cowgirl.controller as a copy of the placeholder controller (same states, parameters and
        /// locomotion/jump/knockdown transitions), with her clips assigned and recovery reworked:
        ///
        /// The Animator is disabled while ragdolled and resets to its default state (Idle) when re-enabled, so the
        /// base controller's Knockdown → Recover transition can never fire. Recovery is instead entered from
        /// Any State, split by the RecoveryFaceUp bool that AnimationStateController already writes.
        /// Each get-up is time-scaled to exactly <see cref="RecoveryDuration"/>.
        /// </summary>
        private static AnimatorController CreateController(Dictionary<string, AnimationClip> clips, float humanScale)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath) == null)
                throw new FileNotFoundException(BaseControllerPath);
            AssetDatabase.DeleteAsset(LegacyOverrideControllerPath);
            AssetDatabase.DeleteAsset(ControllerPath);
            if (!AssetDatabase.CopyAsset(BaseControllerPath, ControllerPath))
                throw new System.InvalidOperationException("Could not copy " + BaseControllerPath);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState State(string name)
            {
                AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
                if (state == null) throw new System.InvalidOperationException("State not found in controller: " + name);
                return state;
            }

            foreach (KeyValuePair<string, string> pair in StateClips)
            {
                State(pair.Key).motion = clips[pair.Value];
            }

            if (controller.parameters.All(p => p.name != "RecoveryFaceUp"))
                controller.AddParameter("RecoveryFaceUp", AnimatorControllerParameterType.Bool);

            AnimatorState faceUp = State("Recover");
            faceUp.name = RecoverFaceUpState;
            AnimationClip faceUpClip = clips["Cowgirl_GetUp_FaceUp"];
            faceUp.motion = faceUpClip;
            faceUp.speed = faceUpClip.length / RecoveryDuration;

            Vector3 position = machine.states.First(s => s.state == faceUp).position;
            AnimatorState faceDown = machine.AddState(RecoverFaceDownState, position + new Vector3(0f, 70f, 0f));
            AnimationClip faceDownClip = clips["Cowgirl_GetUp_FaceDown"];
            faceDown.motion = faceDownClip;
            faceDown.speed = faceDownClip.length / RecoveryDuration;
            faceDown.writeDefaultValues = faceUp.writeDefaultValues;

            // Outgoing (Recovering = false → Idle) is shared by both get-ups.
            foreach (AnimatorStateTransition source in faceUp.transitions)
            {
                AnimatorStateTransition copy = faceDown.AddTransition(source.destinationState);
                copy.hasExitTime = source.hasExitTime;
                copy.exitTime = source.exitTime;
                copy.duration = source.duration;
                copy.conditions = source.conditions;
            }

            // Incoming: drop the unreachable Knockdown → Recover, enter from Any State instead.
            foreach (ChildAnimatorState child in machine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions.Where(t => t.destinationState == faceUp).ToArray())
                    child.state.RemoveTransition(transition);
            }

            AddRecoveryEntry(machine, faceUp, AnimatorConditionMode.If);
            AddRecoveryEntry(machine, faceDown, AnimatorConditionMode.IfNot);

            BuildLocomotion(controller, machine, State("Walk"), State("Run"), clips, humanScale);

            // Land: the base controller only exits to Idle, so landing while moving went Land → Idle → Walk
            // (a stop-start hitch after every running jump). Moving landings now flow straight back into locomotion.
            AnimatorState land = State("Land");
            AnimatorState locomotion = State(LocomotionState);
            AnimatorStateTransition toLocomotion = land.AddTransition(locomotion);
            toLocomotion.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
            toLocomotion.hasExitTime = true;
            toLocomotion.exitTime = 0.35f;
            toLocomotion.duration = 0.12f;
            land.transitions = new[] { toLocomotion }.Concat(land.transitions.Where(t => t != toLocomotion)).ToArray();

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        public const string LocomotionState = "Locomotion";

        /// <summary>
        /// Replaces the separate Walk and Run states with one 1D blend tree on Speed (m/s), so foot speed matches
        /// body speed everywhere and walk→run is a phase-synchronised blend instead of a cross-fade between two
        /// unsynchronised clips. Thresholds are each clip's native ground speed on her body; the top child is
        /// Slow Run time-scaled to the motor's run speed.
        /// The logical Walk/Run states in AnimationStateController are unaffected (they are computed in code).
        /// </summary>
        private static void BuildLocomotion(AnimatorController controller, AnimatorStateMachine machine,
            AnimatorState walk, AnimatorState run, Dictionary<string, AnimationClip> clips, float humanScale)
        {
            AnimationClip walkSource = clips["Cowgirl_Walk"], runClip = clips["Cowgirl_Run"];
            float walkSpeed = NativeSpeed(walkSource, humanScale), runSpeed = NativeSpeed(runClip, humanScale);
            AnimationClip walkClip = WalkWithWeightShift(walkSource);
            var movement = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(Root + "/Settings/PlayerMovementConfig.asset");
            float topSpeed = movement != null ? movement.runSpeed : 6.5f;
            if (topSpeed <= runSpeed) topSpeed = runSpeed * 1.5f;
            // Brisk walk: the walk clip itself covers speeds up to BriskWalkCadence × its native speed (or the motor's
            // walk speed if lower), so walking paces show a clean walk instead of a half-walk/half-jog mix.
            float briskSpeed = Mathf.Min(walkSpeed * BriskWalkCadence, movement != null ? movement.walkSpeed : float.MaxValue);
            bool brisk = briskSpeed > walkSpeed + 0.05f && briskSpeed < runSpeed - 0.3f;

            var tree = new BlendTree
            {
                name = LocomotionState,
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(walkClip, walkSpeed);
            if (brisk) tree.AddChild(walkClip, briskSpeed);
            tree.AddChild(runClip, runSpeed);
            tree.AddChild(runClip, topSpeed);
            ChildMotion[] children = tree.children;
            if (brisk) children[1].timeScale = briskSpeed / walkSpeed;
            children[children.Length - 1].timeScale = topSpeed / runSpeed;
            tree.children = children;

            walk.name = LocomotionState;
            walk.motion = tree;
            walk.speed = 1f;

            foreach (ChildAnimatorState child in machine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions.Where(t => t.destinationState == run).ToArray())
                {
                    if (child.state == walk) child.state.RemoveTransition(transition);
                    else transition.destinationState = walk;
                }
            }

            machine.RemoveState(run);
            Debug.Log($"[Phase 2B] Locomotion: walk {walkSpeed:F2} m/s" + (brisk ? $", brisk walk {briskSpeed:F2} m/s (×{briskSpeed / walkSpeed:F2})" : "") +
                      $", run {runSpeed:F2} m/s, top {topSpeed:F2} m/s (run ×{topSpeed / runSpeed:F2})");
        }

        /// <summary>Highest cadence the walk clip is played at before it hands over to the jog (×1.25 = brisk walk).</summary>
        public const float BriskWalkCadence = 1.25f;

        public const string WalkWeightShiftPath = ClipFolder + "/Cowgirl_Walk_WeightShift.anim";

        /// <summary>
        /// The walk with its horizontal weight shift kept. The motor moves the character and root motion is off, so
        /// the body's horizontal motion relative to the travel line (side-to-side shift over the stance foot, and the
        /// forward surge/settle each step) was extracted as root motion and discarded: in game the pelvis swayed
        /// 1.3 cm instead of the authored 4.7 cm, which reads as gliding. This copy of the imported clip removes only
        /// the steady forward travel from the body-position curves and bakes the remaining horizontal motion into the
        /// pose. Every muscle curve, the rotation and the vertical bob are copied unchanged; the source FBX is untouched.
        /// </summary>
        private static AnimationClip WalkWithWeightShift(AnimationClip source)
        {
            AssetDatabase.DeleteAsset(WalkWeightShiftPath);
            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(WalkWeightShiftPath), frameRate = source.frameRate };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (binding.type == typeof(Animator) && (binding.propertyName == "RootT.x" || binding.propertyName == "RootT.z"))
                    curve = Oscillation(curve, source.length);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopBlendPositionXZ = true; // bake horizontal body motion into the pose (nothing left to extract)
            settings.keepOriginalPositionXZ = true; // body centred over the character on average (curves are zero-mean)
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, WalkWeightShiftPath);
            if (!clip.isHumanMotion) throw new System.InvalidOperationException("Weight-shift walk is not humanoid.");
            return clip;
        }

        /// <summary>Removes the steady travel (linear trend across one loop) and the mean, leaving the per-step motion.</summary>
        private static AnimationCurve Oscillation(AnimationCurve curve, float length)
        {
            Keyframe[] keys = curve.keys;
            if (keys.Length < 2 || length <= 0f) return curve;
            float start = curve.Evaluate(0f), slope = (curve.Evaluate(length) - start) / length;
            const int samples = 200;
            float mean = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = length * (i + 0.5f) / samples;
                mean += curve.Evaluate(t) - start - slope * t;
            }

            mean /= samples;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value -= start + slope * keys[i].time + mean;
                keys[i].inTangent -= slope;
                keys[i].outTangent -= slope;
            }

            return new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
        }

        /// <summary>
        /// Ground speed of a humanoid clip on this character (m/s), measured directly: the clip is played on her
        /// model with root motion for whole cycles and the root's travel is timed. (clip.averageSpeed × humanScale
        /// proved unstable across re-imports of the corrected clip Avatars.)
        /// </summary>
        private static float NativeSpeed(AnimationClip clip, float humanScale)
        {
            const string probePath = ClipFolder + "/_SpeedProbe.controller";
            AssetDatabase.DeleteAsset(probePath);
            AnimatorController probe = AnimatorController.CreateAnimatorControllerAtPathWithClip(probePath, clip);
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            float speed;
            try
            {
                Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().First();
                animator.runtimeAnimatorController = probe;
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
                // Clips are imported looping; time exactly two cycles so phase does not bias the result.
                const float dt = 1f / 120f;
                int steps = Mathf.RoundToInt(clip.length * 2f / dt);
                Vector3 start = model.transform.position;
                for (int i = 0; i < steps; i++) animator.Update(dt);
                Vector3 travel = Vector3.ProjectOnPlane(model.transform.position - start, Vector3.up);
                speed = travel.magnitude / (steps * dt);
            }
            finally
            {
                Object.DestroyImmediate(model);
                AssetDatabase.DeleteAsset(probePath);
            }

            float reference = new Vector3(clip.averageSpeed.x, 0f, clip.averageSpeed.z).magnitude * humanScale;
            Debug.Log($"[Phase 2B] {clip.name}: measured {speed:F2} m/s on her body (averageSpeed estimate {reference:F2})");
            if (speed < 0.5f || speed > 8f)
                throw new System.InvalidOperationException($"Implausible native speed {speed:F2} m/s for {clip.name}");
            return speed;
        }

        private static float HumanScale(Avatar avatar)
        {
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try
            {
                Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.Rebind();
                return animator.humanScale;
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static void AddRecoveryEntry(AnimatorStateMachine machine, AnimatorState state, AnimatorConditionMode faceUpMode)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Recovering");
            transition.AddCondition(faceUpMode, 0f, "RecoveryFaceUp");
            // The ragdoll's own pose blend (RagdollConfig.poseBlendTime) smooths the hand-over; no Animator blend.
            transition.duration = 0f;
            transition.hasExitTime = false;
            transition.canTransitionToSelf = false;
        }

        /// <summary>Her own copy of KnockdownConfig so the longer get-up does not change the placeholder's timing.</summary>
        private static KnockdownConfig CreateKnockdownConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<KnockdownConfig>(KnockdownConfigPath);
            if (config == null)
            {
                if (!AssetDatabase.CopyAsset(BaseKnockdownConfigPath, KnockdownConfigPath))
                    throw new System.InvalidOperationException("Could not copy " + BaseKnockdownConfigPath);
                config = AssetDatabase.LoadAssetAtPath<KnockdownConfig>(KnockdownConfigPath);
            }

            // Only the recovery length is forced; any other tuning made on this asset is kept.
            config.timings.RecoveryDuration = RecoveryDuration;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        // ------------------------------------------------------------------ prefab

        private static GameObject BuildPrefab(Avatar avatar, RuntimeAnimatorController controller, KnockdownConfig knockdownConfig)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Foundation1Builder.PlayerPrefabPath);
            try
            {
                contents.name = "Player_Cowgirl";
                var motor = contents.GetComponent<CharacterMotor>();
                var knockdown = contents.GetComponent<KnockdownController>();
                var ragdoll = contents.GetComponent<RagdollController>();
                var character = contents.GetComponent<PlayerCharacter>();
                knockdown.config = knockdownConfig;

                // Replace the primitive mannequin (and its procedural animator) with the imported character.
                Object.DestroyImmediate(contents.transform.Find("Model").gameObject);
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, contents.transform);
                model.name = "Model";
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GameLayers.Ragdoll;

                Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var animationState = model.AddComponent<AnimationStateController>();
                animationState.animator = animator;
                animationState.motor = motor;
                animationState.knockdown = knockdown;

                Rigidbody hips = BuildRagdoll(model.transform);

                ragdoll.ragdollRoot = model.transform;
                ragdoll.rootBody = hips;
                ragdoll.animator = animator;
                ragdoll.disableWhileSimulated = new Behaviour[0];

                character.modelRoot = model.transform;
                character.animationState = animationState;
                character.focusHeight = FocusHeight;

                var capsule = contents.GetComponent<CharacterController>();
                capsule.height = CapsuleHeight;
                capsule.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);

                ReportMissingReferences(contents);
                return PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 11-body ragdoll in the layout of Unity's ragdoll wizard, with the Phase 2A placeholder's masses (61 kg)
        /// and joint limits. Joint axes are given in character space and converted to each bone's local space,
        /// because Mixamo bones are not aligned with the character like the placeholder's bones were.
        /// Convention (verified in PhysX): positive twist swings the limb forward.
        /// </summary>
        private static Rigidbody BuildRagdoll(Transform model)
        {
            Transform B(string name)
            {
                Transform t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == Bone + name);
                if (t == null) throw new System.InvalidOperationException("Bone not found: " + name);
                return t;
            }

            Transform hips = B("Hips"), spine1 = B("Spine1"), neck = B("Neck"), head = B("Head"), headTop = B("HeadTop_End");
            Transform lArm = B("LeftArm"), lFore = B("LeftForeArm"), lHand = B("LeftHand");
            Transform rArm = B("RightArm"), rFore = B("RightForeArm"), rHand = B("RightHand");
            Transform lUp = B("LeftUpLeg"), lLeg = B("LeftLeg"), lFoot = B("LeftFoot");
            Transform rUp = B("RightUpLeg"), rLeg = B("RightLeg"), rFoot = B("RightFoot");

            // Colliders, from bone positions in the bind (T) pose.
            Box(hips, new Vector3(0f, (lUp.position.y + spine1.position.y) * 0.5f - 0.04f, 0.01f),
                new Vector3(0.32f, spine1.position.y - lUp.position.y + 0.12f, 0.22f));
            Box(spine1, new Vector3(0f, (spine1.position.y + neck.position.y) * 0.5f, 0.02f),
                new Vector3(0.32f, neck.position.y - spine1.position.y, 0.22f));
            var headCollider = head.gameObject.AddComponent<SphereCollider>();
            headCollider.center = head.InverseTransformPoint((head.position + headTop.position) * 0.5f + Vector3.forward * 0.01f);
            headCollider.radius = 0.11f;
            Capsule(lArm, lArm.position, lFore.position, 0.05f);
            Capsule(rArm, rArm.position, rFore.position, 0.05f);
            Capsule(lFore, lFore.position, lHand.position + (lHand.position - lFore.position).normalized * 0.08f, 0.04f);
            Capsule(rFore, rFore.position, rHand.position + (rHand.position - rFore.position).normalized * 0.08f, 0.04f);
            Capsule(lUp, lUp.position, lLeg.position, 0.08f);
            Capsule(rUp, rUp.position, rLeg.position, 0.08f);
            Vector3 lSole = new Vector3(lFoot.position.x, 0.06f, lFoot.position.z), rSole = new Vector3(rFoot.position.x, 0.06f, rFoot.position.z);
            Capsule(lLeg, lLeg.position, lSole, 0.055f);
            Capsule(rLeg, rLeg.position, rSole, 0.055f);

            // Bodies: same masses as ApplyPhase2ARagdollSettings (61 kg).
            Rigidbody hipsBody = Body(hips, 16f), chestBody = Body(spine1, 18f);
            Body(head, 5f);
            Rigidbody lArmBody = Body(lArm, 2.5f), rArmBody = Body(rArm, 2.5f);
            Body(lFore, 1.5f); Body(rFore, 1.5f);
            Rigidbody lUpBody = Body(lUp, 5f), rUpBody = Body(rUp, 5f);
            Body(lLeg, 2f); Body(rLeg, 2f);

            Vector3 right = Vector3.right, up = Vector3.up, forward = Vector3.forward;
            Joint(spine1, hipsBody, right, forward, -20f, 20f, 10f, 10f);
            Joint(head, chestBody, right, forward, -40f, 25f, 25f, 20f);
            Joint(lUp, hipsBody, right, forward, -20f, 90f, 30f, 10f);
            Joint(rUp, hipsBody, right, forward, -20f, 90f, 30f, 10f);
            Joint(lLeg, lUpBody, right, forward, -130f, 0f, 0f, 0f);
            Joint(rLeg, rUpBody, right, forward, -130f, 0f, 0f, 0f);
            // T-pose arms point sideways, so forward/back swing is about the vertical axis (sign mirrored per side)
            // and raising/lowering the arm is the swing about the forward axis (80° covers arms-at-sides).
            Joint(lArm, chestBody, -up, forward, -50f, 110f, 80f, 30f);
            Joint(rArm, chestBody, up, forward, -50f, 110f, 80f, 30f);
            Joint(lFore, lArmBody, -up, forward, 0f, 140f, 0f, 0f);
            Joint(rFore, rArmBody, up, forward, 0f, 140f, 0f, 0f);
            return hipsBody;
        }

        private static void Box(Transform bone, Vector3 worldCenter, Vector3 worldSize)
        {
            var box = bone.gameObject.AddComponent<BoxCollider>();
            box.center = bone.InverseTransformPoint(worldCenter);
            Vector3 local = bone.InverseTransformVector(worldSize);
            box.size = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        }

        private static void Capsule(Transform bone, Vector3 worldStart, Vector3 worldEnd, float radius)
        {
            Vector3 a = bone.InverseTransformPoint(worldStart), b = bone.InverseTransformPoint(worldEnd);
            Vector3 d = b - a;
            int axis = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0 : 2) : (Mathf.Abs(d.y) > Mathf.Abs(d.z) ? 1 : 2);
            float scale = bone.lossyScale.x;
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = (a + b) * 0.5f;
            capsule.radius = radius / scale;
            capsule.height = Mathf.Abs(d[axis]) + capsule.radius;
        }

        private static Rigidbody Body(Transform bone, float mass)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = true;
            return body;
        }

        private static void Joint(Transform bone, Rigidbody parent, Vector3 worldTwistAxis, Vector3 worldSwingAxis,
            float lowTwist, float highTwist, float swing1, float swing2)
        {
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = true;
            joint.axis = bone.InverseTransformDirection(worldTwistAxis).normalized;
            joint.swingAxis = bone.InverseTransformDirection(worldSwingAxis).normalized;
            joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
            joint.swing1Limit = new SoftJointLimit { limit = swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = swing2 };
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 30f;
        }

        private static void ReportMissingReferences(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || !(component is MonoBehaviour)) continue;
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
                    {
                        Debug.LogWarning($"[Phase 2B] Broken reference {component.GetType().Name}.{property.propertyPath} on {component.name}");
                    }
                }
            }
        }

        // ------------------------------------------------------------------ scene copy

        private static void BuildScene(GameObject prefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new FileNotFoundException(SourceScenePath);
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                throw new System.InvalidOperationException("Could not copy " + SourceScenePath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PlayerCharacter old = scene.GetRootGameObjects().Select(g => g.GetComponent<PlayerCharacter>()).FirstOrDefault(p => p != null);
            if (old == null) throw new System.InvalidOperationException("No PlayerCharacter in " + ScenePath);
            GameObject oldObject = old.gameObject;

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "Player";
            player.transform.SetPositionAndRotation(oldObject.transform.position, oldObject.transform.rotation);
            player.transform.SetSiblingIndex(oldObject.transform.GetSiblingIndex());

            // Scene-specific settings made by Foundation1Builder on the placeholder instance.
            player.GetComponent<CharacterMotor>().viewTransform = oldObject.GetComponent<CharacterMotor>().viewTransform;
            var oldRespawner = oldObject.GetComponent<PlayerRespawner>();
            var respawner = player.GetComponent<PlayerRespawner>();
            respawner.checkpoints = oldRespawner.checkpoints;
            respawner.killHeight = oldRespawner.killHeight;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<CharacterMotor>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(respawner);

            // Re-point every reference to the old player (camera target, course, UI...) at the new one.
            int remapped = 0;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject == oldObject || rootObject == player) continue;
                foreach (Component component in rootObject.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
                        Object value = property.objectReferenceValue;
                        GameObject owner = value is GameObject g ? g : (value as Component)?.gameObject;
                        if (owner == null || !owner.transform.IsChildOf(oldObject.transform)) continue;
                        Object replacement = null;
                        if (owner == oldObject)
                            replacement = value is GameObject ? (Object)player : player.GetComponent(value.GetType());
                        if (replacement == null)
                            Debug.LogWarning($"[Phase 2B] Cannot remap {component.GetType().Name}.{property.propertyPath} (pointed into the placeholder model).");
                        property.objectReferenceValue = replacement;
                        changed = true;
                        remapped++;
                    }

                    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Object.DestroyImmediate(oldObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Phase 2B] Scene copy saved with {remapped} player references re-pointed: {ScenePath}");
        }
    }
}
