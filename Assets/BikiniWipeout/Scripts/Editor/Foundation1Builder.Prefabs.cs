using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.EditorTools
{
    public static partial class Foundation1Builder
    {
        public const string PlayerPrefabPath = PrefabsFolder + "/Characters/Player_Placeholder.prefab";
        public const string SweeperPrefabPath = PrefabsFolder + "/Obstacles/RotatingSweeper.prefab";
        public const string BallPrefabPath = PrefabsFolder + "/Obstacles/BouncingBall.prefab";
        public const string MovingPlatformPrefabPath = PrefabsFolder + "/Obstacles/MovingPlatform.prefab";
        public const string CheckpointPrefabPath = PrefabsFolder + "/Environment/Checkpoint.prefab";
        public const string FinishLinePrefabPath = PrefabsFolder + "/Environment/FinishLine.prefab";
        public const string WaterPrefabPath = PrefabsFolder + "/Environment/WaterHazard.prefab";
        public const string SplashPrefabPath = PrefabsFolder + "/Environment/SplashEffect.prefab";
        public const string AudioManagerPrefabPath = PrefabsFolder + "/Systems/AudioManager.prefab";

        internal sealed class PrefabSet
        {
            public GameObject Player, Sweeper, Ball, MovingPlatform, Checkpoint, FinishLine, Water, Splash, AudioManager;
        }

        private static PrefabSet BuildPrefabs(ContentAssets assets)
        {
            var prefabs = new PrefabSet();
            prefabs.Splash = GetOrBuildPrefab(SplashPrefabPath, () => CreateSplash(assets));
            prefabs.Water = GetOrBuildPrefab(WaterPrefabPath, () => CreateWater(assets, prefabs.Splash));
            prefabs.AudioManager = GetOrBuildPrefab(AudioManagerPrefabPath, () => CreateAudioManager(assets));
            prefabs.Player = GetOrBuildPrefab(PlayerPrefabPath, () => CreatePlayer(assets));
            prefabs.Sweeper = GetOrBuildPrefab(SweeperPrefabPath, () => CreateSweeper(assets));
            prefabs.Ball = GetOrBuildPrefab(BallPrefabPath, () => CreateBall(assets));
            prefabs.MovingPlatform = GetOrBuildPrefab(MovingPlatformPrefabPath, () => CreateMovingPlatform(assets));
            prefabs.Checkpoint = GetOrBuildPrefab(CheckpointPrefabPath, () => CreateCheckpoint(assets));
            prefabs.FinishLine = GetOrBuildPrefab(FinishLinePrefabPath, () => CreateFinishLine(assets));
            return prefabs;
        }

        // ------------------------------------------------------------------ player

        /// <summary>
        /// Placeholder contestant: a primitive mannequin (about 1.7 m) with an 11-body ragdoll laid out like
        /// Unity's ragdoll wizard, so the final rigged character can replace the "Model" child directly.
        /// </summary>
        private static GameObject CreatePlayer(ContentAssets assets)
        {
            var root = new GameObject("Player_Placeholder") { layer = GameLayers.Player, tag = "Player" };

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;

            var inputReader = root.AddComponent<PlayerInputReader>();
            inputReader.actionsAsset = assets.InputActions;

            var motor = root.AddComponent<CharacterMotor>();
            motor.config = assets.Movement;

            // ---- Model: everything visual and every ragdoll bone lives under this one child.
            GameObject model = Empty("Model", root.transform, Vector3.zero, GameLayers.Ragdoll);
            var animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = assets.Animator;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Transform hips = Bone("Hips", model.transform, new Vector3(0f, 0.95f, 0f));
            Transform chest = Bone("Chest", hips, new Vector3(0f, 1.07f, 0f));
            Transform head = Bone("Head", chest, new Vector3(0f, 1.45f, 0f));
            Transform leftUpperArm = Bone("LeftUpperArm", chest, new Vector3(-0.22f, 1.38f, 0f));
            Transform leftLowerArm = Bone("LeftLowerArm", leftUpperArm, new Vector3(-0.22f, 1.10f, 0f));
            Transform rightUpperArm = Bone("RightUpperArm", chest, new Vector3(0.22f, 1.38f, 0f));
            Transform rightLowerArm = Bone("RightLowerArm", rightUpperArm, new Vector3(0.22f, 1.10f, 0f));
            Transform leftUpperLeg = Bone("LeftUpperLeg", hips, new Vector3(-0.09f, 0.92f, 0f));
            Transform leftLowerLeg = Bone("LeftLowerLeg", leftUpperLeg, new Vector3(-0.09f, 0.50f, 0f));
            Transform rightUpperLeg = Bone("RightUpperLeg", hips, new Vector3(0.09f, 0.92f, 0f));
            Transform rightLowerLeg = Bone("RightLowerLeg", rightUpperLeg, new Vector3(0.09f, 0.50f, 0f));

            // ---- Visuals (no colliders) and physics shapes.
            Visual(hips, PrimitiveType.Cube, "HipsMesh", Vector3.zero, new Vector3(0.30f, 0.20f, 0.20f), assets.Suit);
            Box(hips, Vector3.zero, new Vector3(0.30f, 0.20f, 0.20f));

            Visual(chest, PrimitiveType.Cube, "TorsoMesh", new Vector3(0f, 0.09f, 0f), new Vector3(0.27f, 0.18f, 0.17f), assets.Skin);
            Visual(chest, PrimitiveType.Cube, "TopMesh", new Vector3(0f, 0.27f, 0f), new Vector3(0.33f, 0.19f, 0.20f), assets.Suit);
            Box(chest, new Vector3(0f, 0.18f, 0f), new Vector3(0.32f, 0.36f, 0.20f));

            Visual(head, PrimitiveType.Sphere, "HeadMesh", new Vector3(0f, 0.13f, 0f), Vector3.one * 0.24f, assets.Skin);
            Visual(head, PrimitiveType.Sphere, "HairMesh", new Vector3(0f, 0.16f, -0.03f), new Vector3(0.26f, 0.23f, 0.25f), assets.Hair);
            Visual(head, PrimitiveType.Sphere, "PonytailMesh", new Vector3(0f, 0.10f, -0.17f), new Vector3(0.10f, 0.20f, 0.10f), assets.Hair);
            // A visor on the face so the facing direction is readable at a glance.
            Visual(head, PrimitiveType.Cube, "VisorMesh", new Vector3(0f, 0.14f, 0.105f), new Vector3(0.17f, 0.045f, 0.04f), assets.Dark);
            var headCollider = head.gameObject.AddComponent<SphereCollider>();
            headCollider.center = new Vector3(0f, 0.13f, 0f);
            headCollider.radius = 0.12f;

            Limb(leftUpperArm, "LeftUpperArmMesh", 0.30f, 0.05f, assets.Skin);
            Limb(leftLowerArm, "LeftLowerArmMesh", 0.30f, 0.045f, assets.Skin);
            Limb(rightUpperArm, "RightUpperArmMesh", 0.30f, 0.05f, assets.Skin);
            Limb(rightLowerArm, "RightLowerArmMesh", 0.30f, 0.045f, assets.Skin);
            Limb(leftUpperLeg, "LeftUpperLegMesh", 0.44f, 0.075f, assets.Skin);
            Limb(leftLowerLeg, "LeftLowerLegMesh", 0.50f, 0.06f, assets.Skin);
            Limb(rightUpperLeg, "RightUpperLegMesh", 0.44f, 0.075f, assets.Skin);
            Limb(rightLowerLeg, "RightLowerLegMesh", 0.50f, 0.06f, assets.Skin);
            Visual(leftLowerLeg, PrimitiveType.Cube, "LeftFootMesh", new Vector3(0f, -0.46f, 0.06f), new Vector3(0.09f, 0.06f, 0.22f), assets.White);
            Visual(rightLowerLeg, PrimitiveType.Cube, "RightFootMesh", new Vector3(0f, -0.46f, 0.06f), new Vector3(0.09f, 0.06f, 0.22f), assets.White);

            // ---- Rigidbodies (about 61 kg in total).
            Rigidbody hipsBody = Body(hips, 12f);
            Rigidbody chestBody = Body(chest, 14f);
            Body(head, 5f);
            Rigidbody leftUpperArmBody = Body(leftUpperArm, 2.5f);
            Body(leftLowerArm, 1.5f);
            Rigidbody rightUpperArmBody = Body(rightUpperArm, 2.5f);
            Body(rightLowerArm, 1.5f);
            Rigidbody leftUpperLegBody = Body(leftUpperLeg, 7f);
            Body(leftLowerLeg, 4f);
            Rigidbody rightUpperLegBody = Body(rightUpperLeg, 7f);
            Body(rightLowerLeg, 4f);

            // ---- Joints. The primary axis is the bone's side-to-side axis (local X) and the limits follow
            // the convention of Unity's ragdoll wizard: positive twist swings the limb forward.
            Joint(chest, hipsBody, -20f, 20f, 10f, 10f);
            Joint(head, chestBody, -40f, 25f, 25f, 20f);
            Joint(leftUpperArm, chestBody, -50f, 120f, 70f, 30f);
            Joint(leftLowerArm, leftUpperArmBody, 0f, 130f, 0f, 0f);
            Joint(rightUpperArm, chestBody, -50f, 120f, 70f, 30f);
            Joint(rightLowerArm, rightUpperArmBody, 0f, 130f, 0f, 0f);
            Joint(leftUpperLeg, hipsBody, -20f, 70f, 30f, 10f);
            Joint(leftLowerLeg, leftUpperLegBody, -80f, 0f, 0f, 0f);
            Joint(rightUpperLeg, hipsBody, -20f, 70f, 30f, 10f);
            Joint(rightLowerLeg, rightUpperLegBody, -80f, 0f, 0f, 0f);

            ApplyPhase2ARagdollSettings(root);

            // ---- Animation.
            var animationState = model.AddComponent<AnimationStateController>();
            animationState.animator = animator;
            animationState.motor = motor;

            var procedural = model.AddComponent<PlaceholderProceduralAnimator>();
            procedural.stateSource = animationState;
            procedural.motor = motor;
            procedural.hips = hips;
            procedural.chest = chest;
            procedural.leftUpperArm = leftUpperArm;
            procedural.leftLowerArm = leftLowerArm;
            procedural.rightUpperArm = rightUpperArm;
            procedural.rightLowerArm = rightLowerArm;
            procedural.leftUpperLeg = leftUpperLeg;
            procedural.leftLowerLeg = leftLowerLeg;
            procedural.rightUpperLeg = rightUpperLeg;
            procedural.rightLowerLeg = rightLowerLeg;

            // ---- Gameplay components on the root.
            var ragdoll = root.AddComponent<RagdollController>();
            ragdoll.config = assets.Ragdoll;
            ragdoll.ragdollRoot = model.transform;
            ragdoll.rootBody = hipsBody;
            ragdoll.animator = animator;
            ragdoll.disableWhileSimulated = new Behaviour[] { procedural };

            var knockdown = root.AddComponent<KnockdownController>();
            knockdown.config = assets.Knockdown;
            knockdown.motor = motor;
            knockdown.ragdoll = ragdoll;
            animationState.knockdown = knockdown;
            procedural.recoverDuration = assets.Knockdown.timings.RecoveryDuration;

            var audio = root.AddComponent<CharacterAudio>();
            audio.motor = motor;
            audio.knockdown = knockdown;
            audio.ragdoll = ragdoll;

            var feedback = root.AddComponent<GamepadFeedback>();
            feedback.knockdown = knockdown;
            feedback.motor = motor;

            var character = root.AddComponent<PlayerCharacter>();
            var respawner = root.AddComponent<PlayerRespawner>();
            respawner.character = character;

            character.motor = motor;
            character.knockdown = knockdown;
            character.ragdoll = ragdoll;
            character.respawner = respawner;
            character.animationState = animationState;
            character.modelRoot = model.transform;

            return root;
        }

        private static Transform Bone(string name, Transform parent, Vector3 worldPosition)
        {
            var go = new GameObject(name) { layer = GameLayers.Ragdoll };
            go.transform.SetParent(parent, false);
            // The temporary character sits at the origin, so world space equals character space here.
            go.transform.position = worldPosition;
            return go.transform;
        }

        private static void Visual(Transform bone, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            Primitive(type, name, bone, localPosition, scale, material, GameLayers.Ragdoll, false);
        }

        /// <summary>A limb hanging straight down from its bone: capsule visual plus matching collider.</summary>
        private static void Limb(Transform bone, string meshName, float length, float radius, Material material)
        {
            var center = new Vector3(0f, -length * 0.5f + radius * 0.5f, 0f);
            // Unity's capsule primitive is 2 units tall and 1 unit wide.
            Visual(bone, PrimitiveType.Capsule, meshName, center, new Vector3(radius * 2f, length * 0.5f, radius * 2f), material);

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = center;
            capsule.radius = radius;
            capsule.height = length;
        }

        private static void Box(Transform bone, Vector3 center, Vector3 size)
        {
            var box = bone.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        [MenuItem("Bikini Wipeout/Apply Phase 2A Placeholder Physics")]
        public static void UpgradePhase2APlaceholder()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null || prefab.GetComponentInChildren<PlaceholderProceduralAnimator>(true) == null)
            {
                Debug.LogError("Generate the placeholder first. This command does not modify imported characters.");
                return;
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                ApplyPhase2ARagdollSettings(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Debug.Log("Phase 2A placeholder masses and knee limits updated; scenes and configuration assets preserved.");
        }

        private static void ApplyPhase2ARagdollSettings(GameObject root)
        {
            // Preserve the 61 kg total while moving mass from distal limbs into the pelvis/torso.
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                switch (body.name)
                {
                    case "Hips": body.mass = 16f; break;
                    case "Chest": body.mass = 18f; break;
                    case "LeftUpperLeg": case "RightUpperLeg":
                        body.mass = 5f;
                        CharacterJoint hip = body.GetComponent<CharacterJoint>();
                        if (hip != null) hip.highTwistLimit = new SoftJointLimit { limit = 90f };
                        break;
                    case "LeftUpperArm": case "RightUpperArm":
                        CharacterJoint shoulder = body.GetComponent<CharacterJoint>();
                        if (shoulder != null) shoulder.highTwistLimit = new SoftJointLimit { limit = 130f };
                        break;
                    case "LeftLowerLeg": case "RightLowerLeg":
                        body.mass = 2f;
                        CharacterJoint knee = body.GetComponent<CharacterJoint>();
                        if (knee != null) knee.lowTwistLimit = new SoftJointLimit { limit = -130f };
                        break;
                }
            }
        }

        private static Rigidbody Body(Transform bone, float mass)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = true;
            return body;
        }

        private static void Joint(Transform bone, Rigidbody parent, float lowTwist, float highTwist, float swing1, float swing2)
        {
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = true;
            joint.axis = Vector3.right;
            joint.swingAxis = Vector3.forward;
            joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
            joint.swing1Limit = new SoftJointLimit { limit = swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = swing2 };
            joint.enableCollision = false;
            // Conservative settings: projection pulls stretched joints back together instead of letting them explode.
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 30f;
        }

        // ------------------------------------------------------------------ obstacles

        /// <summary>Obstacle A. Root is static (hub pillar); "Arm" is a kinematic body that carries the impact zone.</summary>
        private static GameObject CreateSweeper(ContentAssets assets)
        {
            const float armReach = 6.2f;
            const float armHeight = 0.55f;
            const float armThickness = 0.36f;
            const float hubRadius = 0.6f;

            var root = new GameObject("RotatingSweeper") { layer = GameLayers.Obstacle };

            // Cylinder primitive: 2 units tall, 1 wide. Its default collider is a capsule, which suits a pillar.
            Primitive(PrimitiveType.Cylinder, "Hub", root.transform, new Vector3(0f, 0.65f, 0f),
                new Vector3(hubRadius * 2f, 0.65f, hubRadius * 2f), assets.Yellow, GameLayers.Obstacle, true);
            Primitive(PrimitiveType.Sphere, "HubCap", root.transform, new Vector3(0f, 1.3f, 0f),
                Vector3.one * (hubRadius * 2f), assets.HotPink, GameLayers.Obstacle, false);

            GameObject arm = Empty("Arm", root.transform, new Vector3(0f, armHeight, 0f), GameLayers.Obstacle);
            var body = arm.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            float barLength = armReach - hubRadius;
            float barCenter = hubRadius + barLength * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                string suffix = side < 0 ? "A" : "B";
                Primitive(PrimitiveType.Cube, "Bar" + suffix, arm.transform, new Vector3(side * barCenter, 0f, 0f),
                    new Vector3(barLength, armThickness, armThickness), assets.HotPink, GameLayers.Obstacle, true);
                Primitive(PrimitiveType.Sphere, "Pad" + suffix, arm.transform, new Vector3(side * armReach, 0f, 0f),
                    Vector3.one * (armThickness * 1.7f), assets.White, GameLayers.Obstacle, true);
            }

            // Trigger zone slightly larger than the bars: characters are detected just before solid contact.
            GameObject zone = Empty("ImpactZone", arm.transform, Vector3.zero, GameLayers.Obstacle);
            var zoneCollider = zone.AddComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3((armReach + armThickness) * 2f, armThickness + 0.2f, armThickness + 0.2f);

            var impact = arm.AddComponent<ImpactSource>();
            impact.kind = ImpactSourceKind.Sweeper;
            impact.body = body;

            var sweeper = root.AddComponent<RotatingSweeper>();
            sweeper.armBody = body;
            sweeper.impactSource = impact;
            sweeper.rotationSpeed = 70f;
            sweeper.impactForce = 1.6f;
            impact.forceMultiplier = sweeper.impactForce;

            return root;
        }

        /// <summary>Obstacle B. A light, bouncy dynamic sphere with a slightly larger trigger shell for impacts.</summary>
        private static GameObject CreateBall(ContentAssets assets)
        {
            const float radius = 1.1f;

            GameObject root = Primitive(PrimitiveType.Sphere, "BouncingBall", null, Vector3.zero,
                Vector3.one * (radius * 2f), assets.Red, GameLayers.Obstacle, true);

            // A contrasting band so the rolling motion is visible.
            Primitive(PrimitiveType.Cylinder, "Band", root.transform, Vector3.zero,
                new Vector3(1.01f, 0.07f, 1.01f), assets.Red, GameLayers.Obstacle, false);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 8f;
            body.angularDamping = 0.6f;
            body.linearDamping = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            GameObject zone = Empty("ImpactZone", root.transform, Vector3.zero, GameLayers.Obstacle);
            var zoneCollider = zone.AddComponent<SphereCollider>();
            zoneCollider.isTrigger = true;
            zoneCollider.radius = 0.54f; // in the ball's local space (the sphere mesh has radius 0.5)

            var impact = root.AddComponent<ImpactSource>();
            impact.kind = ImpactSourceKind.Ball;
            impact.body = body;

            var ball = root.AddComponent<BouncingBall>();
            ball.impactSource = impact;
            impact.forceMultiplier = ball.impactForce;

            return root;
        }

        /// <summary>Obstacle C. Unscaled kinematic root (so riders are not affected by scale) with a scaled deck.</summary>
        private static GameObject CreateMovingPlatform(ContentAssets assets)
        {
            var root = new GameObject("MovingPlatform") { layer = GameLayers.MovingPlatform };

            Primitive(PrimitiveType.Cube, "Deck", root.transform, new Vector3(0f, -0.25f, 0f),
                new Vector3(4f, 0.5f, 4f), assets.Lime, GameLayers.MovingPlatform, true);
            Primitive(PrimitiveType.Cube, "Trim", root.transform, new Vector3(0f, -0.3f, 0f),
                new Vector3(4.2f, 0.3f, 4.2f), assets.White, GameLayers.MovingPlatform, false);

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            root.AddComponent<MovingPlatform>();
            return root;
        }

        // ------------------------------------------------------------------ environment pieces

        private static GameObject CreateCheckpoint(ContentAssets assets)
        {
            var root = new GameObject("Checkpoint") { layer = GameLayers.Trigger };
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(8f, 4f, 2.5f);

            GameObject spawn = Empty("SpawnPoint", root.transform, new Vector3(0f, 0f, 1f), GameLayers.Trigger);

            var indicators = new Renderer[2];
            for (int side = -1; side <= 1; side += 2)
            {
                string suffix = side < 0 ? "Left" : "Right";
                Primitive(PrimitiveType.Cylinder, "Post" + suffix, root.transform, new Vector3(side * 3.6f, 1.2f, 0f),
                    new Vector3(0.16f, 1.2f, 0.16f), assets.White, GameLayers.Default, false);
                GameObject flag = Primitive(PrimitiveType.Sphere, "Beacon" + suffix, root.transform,
                    new Vector3(side * 3.6f, 2.6f, 0f), Vector3.one * 0.5f, assets.White, GameLayers.Default, false);
                indicators[side < 0 ? 0 : 1] = flag.GetComponent<Renderer>();
            }

            var checkpoint = root.AddComponent<Checkpoint>();
            checkpoint.spawnPoint = spawn.transform;
            checkpoint.indicators = indicators;
            return root;
        }

        private static GameObject CreateFinishLine(ContentAssets assets)
        {
            var root = new GameObject("FinishLine") { layer = GameLayers.Trigger };
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(9f, 4f, 1.2f);

            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(PrimitiveType.Cylinder, side < 0 ? "PostLeft" : "PostRight", root.transform,
                    new Vector3(side * 4.4f, 1.9f, 0f), new Vector3(0.4f, 1.9f, 0.4f), assets.Red, GameLayers.Default, false);
            }

            Primitive(PrimitiveType.Cube, "Banner", root.transform, new Vector3(0f, 4.0f, 0f),
                new Vector3(9.4f, 0.7f, 0.3f), assets.Red, GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "BannerStripe", root.transform, new Vector3(0f, 4.0f, 0f),
                new Vector3(9.5f, 0.25f, 0.34f), assets.White, GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "GroundStripe", root.transform, new Vector3(0f, 0.011f, 0f),
                new Vector3(9f, 0.02f, 0.6f), assets.White, GameLayers.Default, false);

            root.AddComponent<FinishLine>();
            return root;
        }

        /// <summary>Water surface (visual only) plus the trigger volume that claims anything falling in.</summary>
        private static GameObject CreateWater(ContentAssets assets, GameObject splashPrefab)
        {
            const float size = 320f;
            const float depth = 30f;
            // The trigger starts a little below the surface so the body is visibly in the water when it fires.
            const float triggerInset = 0.45f;

            var root = new GameObject("WaterHazard") { layer = GameLayers.Trigger };
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size, depth, size);
            trigger.center = new Vector3(0f, -triggerInset - depth * 0.5f, 0f);

            // Plane primitive is 10 x 10 units.
            Primitive(PrimitiveType.Plane, "Surface", root.transform, Vector3.zero,
                new Vector3(size / 10f, 1f, size / 10f), AssetDatabase.LoadAssetAtPath<Material>(BroadcastFolder + "EventWater.mat"), 4 /* built-in Water layer */, false);

            var hazard = root.AddComponent<WaterHazard>();
            hazard.splashPrefab = splashPrefab;
            return root;
        }

        private static GameObject CreateSplash(ContentAssets assets)
        {
            var root = new GameObject("SplashEffect");
            // The hazard spawns with identity rotation. Aim the emitter itself upwards.

            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 1f), new Color(0.6f, 0.86f, 1f, 1f));
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)48) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.angle = 46f;
            shape.radius = 0.35f;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BroadcastFolder + "SprayDrop.mat");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            root.AddComponent<SplashEffect>();
            return root;
        }

        private static GameObject CreateAudioManager(ContentAssets assets)
        {
            var root = new GameObject("AudioManager");
            var manager = root.AddComponent<AudioManager>();
            manager.library = assets.Audio;
            return root;
        }
    }
}
