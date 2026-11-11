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
    /// <summary>Independent charD assets using the established cowgirl gameplay and animation architecture.
    /// Cowgirl assets are read-only dependencies; this builder never reimports or changes them.</summary>
    public static class StarsStripesIntegration
    {
        private const string Root = "Assets/BikiniWipeout";
        private const string CharacterFolder = Root + "/Characters/StarsStripes";
        private const string TextureFolder = CharacterFolder + "/Textures";
        private const string MaterialFolder = CharacterFolder + "/Materials";
        public const string ModelPath = CharacterFolder + "/charD_starsstripes.fbx";
        public const string ControllerPath = Root + "/Animations/StarsStripes/StarsStripes.controller";
        public const string KnockdownConfigPath = Root + "/Settings/KnockdownConfig_StarsStripes.asset";
        public const string PrefabPath = Root + "/Prefabs/Characters/Player_StarsStripes.prefab";
        public const string ScenePath = Root + "/Scenes/Course_Foundation1_StarsStripes.unity";
        private const string SourceScenePath = Root + "/Scenes/Course_Foundation1.unity";
        public const string CharacterId = "charD";
        public const string DisplayName = "Stars & Stripes";
        private const string Bone = "mixamorig:";
        private const float CapsuleHeight = 1.65f;
        private const float FocusHeight = 1.0f;

        [MenuItem("Bikini Wipeout/Characters/Integrate Stars & Stripes")]
        public static void Integrate()
        {
            AssetDatabase.Refresh();
            ConfigureTextures();
            Avatar avatar = ConfigureModel(CreateMaterials());
            // Preserve all existing transition, jump-root and recovery timing corrections.
            var controller = CopyAsset<AnimatorController>(CowgirlIntegration.ControllerPath, ControllerPath);
            var config = CopyAsset<KnockdownConfig>(CowgirlIntegration.KnockdownConfigPath, KnockdownConfigPath);
            config.timings.RecoveryDuration = CowgirlIntegration.RecoveryDuration;
            EditorUtility.SetDirty(config);
            GameObject prefab = BuildPrefab(avatar, controller, config);
            BuildScene(prefab, SourceScenePath, ScenePath);
            BuildCourseVariants(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[charD] Integration complete: " + PrefabPath);
        }

        /// <summary>Scene suffix the main menu appends to a course scene name when charD is selected.</summary>
        public const string SceneSuffix = "_StarsStripes";

        /// <summary>Stars &amp; Stripes copies of Course 1 (Gauntlet) and Course 2 (Chaos Cove), made from the
        /// generated course scenes (which hold the cowgirl) by swapping the player and re-pointing references.</summary>
        public static void BuildCourseVariants(GameObject prefab = null)
        {
            prefab = prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (string course in new[] { "Course_SplashdownGauntlet", "Course_ChaosCove" })
            {
                string source = Root + "/Scenes/" + course + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null) { Debug.LogWarning("[charD] Missing course scene, no variant: " + source); continue; }
                BuildScene(prefab, source, Root + "/Scenes/" + course + SceneSuffix + ".unity");
            }

            RegisterInBuildSettings();
            RegisterInMainMenu();
        }

        private static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { ScenePath, Root + "/Scenes/Course_SplashdownGauntlet" + SceneSuffix + ".unity", Root + "/Scenes/Course_ChaosCove" + SceneSuffix + ".unity" })
            {
                if (!File.Exists(path) || scenes.Any(e => e.path == path)) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Makes sure the generated MainMenu lists both playable characters.</summary>
        private static void RegisterInMainMenu()
        {
            const string menuPath = Root + "/Scenes/MainMenu.unity";
            if (!File.Exists(menuPath)) return;
            Scene scene = EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MainMenuController menu = root.GetComponentInChildren<MainMenuController>(true);
                if (menu == null) continue;
                var serialized = new SerializedObject(menu);
                SerializedProperty list = serialized.FindProperty("characters");
                int index = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == CharacterId) index = i;
                if (index < 0) { index = list.arraySize; list.arraySize++; }
                SerializedProperty entry = list.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("id").stringValue = CharacterId;
                entry.FindPropertyRelative("displayName").stringValue = DisplayName.ToUpperInvariant();
                entry.FindPropertyRelative("ready").boolValue = true;
                entry.FindPropertyRelative("sceneSuffix").stringValue = SceneSuffix;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[charD] Registered in the main menu character list.");
                return;
            }
        }

        [MenuItem("Bikini Wipeout/Characters/Build Stars & Stripes Course Variants")]
        public static void BuildCourseVariantsMenu() { BuildCourseVariants(); AssetDatabase.SaveAssets(); }

        public static void BuildCourseVariantsBatch() { BuildCourseVariants(); AssetDatabase.SaveAssets(); }

        private static T CopyAsset<T>(string source, string destination) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(source) == null) throw new FileNotFoundException(source);
            // Copy the entire serialized asset (including embedded state machines), retaining the
            // destination .meta and local file IDs so Terra's registry references survive regeneration.
            File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
            T result = AssetDatabase.LoadAssetAtPath<T>(destination);
            result.name = Path.GetFileNameWithoutExtension(destination);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static void ConfigureTextures()
        {
            foreach (string path in Directory.GetFiles(TextureFolder, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                bool normal = path.EndsWith("_Normal.png");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal && !path.EndsWith("_MetallicSmoothness.png");
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.maxTextureSize = path.Contains("_hands_") ? 1024 : 2048;
                importer.SaveAndReimport();
            }
        }

        private static Material[] CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP Lit missing");
            var result = new List<Material>();
            foreach (bool hands in new[] { false, true })
            {
                string name = hands ? "charD_starsstripes_hands_mat" : "charD_starsstripes_body_mat";
                string prefix = hands ? "charD_starsstripes_hands_" : "charD_starsstripes_";
                string path = MaterialFolder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.name = name;
                material.shader = shader;
                material.SetTexture("_BaseMap", Tex(prefix + "BaseColor"));
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_MetallicGlossMap", Tex(prefix + "MetallicSmoothness"));
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (!hands) { material.SetTexture("_BumpMap", Tex(prefix + "Normal")); material.EnableKeyword("_NORMALMAP"); }
                material.SetFloat("_Cull", 2f);
                EditorUtility.SetDirty(material);
                result.Add(material);
            }
            return result.ToArray();
        }

        private static Texture2D Tex(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + name + ".png");
            if (texture == null) throw new FileNotFoundException("Missing texture " + name);
            return texture;
        }

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
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach (var source in modelAsset.GetComponentsInChildren<Renderer>(true)
                         .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name);
                Material material = source.name.ToLowerInvariant().Contains("hands") ? materials[1] : materials[0];
                importer.AddRemap(id, material);
            }

            importer.SaveAndReimport();

            // Reuse the cowgirl reference-pose correction on charD's own leg proportions.
            // Only the Avatar reference changes; mesh bind poses and skin weights are preserved.
            ApplyTPoseReference(ModelPath, legsOnly: true);

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new System.InvalidOperationException("Stars & Stripes Humanoid Avatar is not valid.");
            return avatar;
        }

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

        private static GameObject BuildPrefab(Avatar avatar, RuntimeAnimatorController controller, KnockdownConfig knockdownConfig)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Foundation1Builder.PlayerPrefabPath);
            try
            {
                contents.name = "Player_StarsStripes";
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
                        Debug.LogWarning($"[charD] Broken reference {component.GetType().Name}.{property.propertyPath} on {component.name}");
                    }
                }
            }
        }

        private static void BuildScene(GameObject prefab, string sourceScenePath, string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScenePath) == null)
                throw new FileNotFoundException(sourceScenePath);
            CopyAsset<SceneAsset>(sourceScenePath, scenePath);

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            PlayerCharacter old = scene.GetRootGameObjects().Select(g => g.GetComponent<PlayerCharacter>()).FirstOrDefault(p => p != null);
            if (old == null) throw new System.InvalidOperationException("No PlayerCharacter in " + scenePath);
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
                            Debug.LogWarning($"[charD] Cannot remap {component.GetType().Name}.{property.propertyPath} (pointed into the placeholder model).");
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
            Debug.Log($"[charD] Scene copy saved with {remapped} player references re-pointed: {scenePath}");
        }
    }
}
