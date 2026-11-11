using System;
using System.Collections.Generic;
using System.IO;
using BikiniWipeout.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Phase 3 — generates Course 1, "The Splashdown Gauntlet", and its arena as a NEW scene.
    /// Walkable surfaces come from <see cref="Course1Layout"/> (the same data the completability tests
    /// check); obstacles, checkpoints, signage and the stadium are placed around them.
    ///
    /// Menu: Bikini Wipeout > Course 1 > Build Splashdown Gauntlet
    /// Batch: -executeMethod BikiniWipeout.EditorTools.Foundation1Builder.BuildCourse1Batch
    /// </summary>
    public static partial class Foundation1Builder
    {
        public const string GauntletScenePath = ScenesFolder + "/" + SceneNames.CourseGauntlet + ".unity";
        public const string GauntletCourseId = "gauntlet1";

        private const float ArenaMinX = -24f, ArenaMaxX = 101f, ArenaMinZ = -30f, ArenaMaxZ = 134f;

        [MenuItem("Bikini Wipeout/Course 1/Build Splashdown Gauntlet (Missing Only)", priority = 60)]
        public static void BuildCourse1Menu()
        {
            if (ConfirmEditMode() && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                BuildCourse1(false);
            }
        }

        [MenuItem("Bikini Wipeout/Course 1/Rebuild Splashdown Gauntlet (Overwrite Course 1 Scene and Prefabs)", priority = 61)]
        public static void RebuildCourse1Menu()
        {
            if (!ConfirmEditMode())
            {
                return;
            }

            bool proceed = EditorUtility.DisplayDialog("Rebuild Course 1",
                "This regenerates the Course 1 scene and the prefabs under Prefabs/Course1 and OVERWRITES manual edits to them.\n\n" +
                "Nothing from Foundation 1 is touched: the player prefab, the original course scene, config assets and " +
                "existing materials are kept.", "Rebuild", "Cancel");
            if (proceed && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                BuildCourse1(true);
            }
        }

        [MenuItem("Bikini Wipeout/Course 1/Open Course 1 Scene", priority = 62)]
        public static void OpenCourse1Scene()
        {
            OpenScene(GauntletScenePath);
        }

        /// <summary>Command-line entry point. Exits with code 0 on success, 1 on failure.</summary>
        public static void BuildCourse1Batch()
        {
            EditorApplication.Exit(BuildCourse1(true) ? 0 : 1);
        }

        public static bool IsCourse1Built()
        {
            return File.Exists(GauntletScenePath);
        }

        /// <summary>Builds the arena assets, Course 1 prefabs and scene. Foundation 1 content is only created if missing, never overwritten.</summary>
        public static bool BuildCourse1(bool overwrite)
        {
            try
            {
                if (!IsContentBuilt() && !BuildAll(false))
                {
                    throw new InvalidOperationException("Foundation 1 content could not be generated (see the error above).");
                }

                // Foundation assets and prefabs: load, or create if missing. Never overwrite.
                overwriteGenerated = false;
                ContentAssets assets = null;
                PrefabSet prefabs = null;
                Step("Shared assets", 0.05f, () =>
                {
                    EnsureFolders();
                    assets = BuildSharedAssets();
                    prefabs = BuildPrefabs(assets);
                });

                overwriteGenerated = overwrite;
                ArenaAssets arena = null;
                Course1Prefabs course1 = null;
                Step("Arena materials and audio", 0.2f, () => arena = BuildArenaAssets());
                Step("Course 1 prefabs", 0.35f, () => course1 = BuildCourse1Prefabs(assets, arena));
                Step("Main menu link", 0.5f, PointMainMenuAtCourse1);
                Step("Course 1 scene", 0.6f, () => BuildGauntletScene(assets, prefabs, arena, course1));
                Step("Build settings", 0.95f, () => AddSceneToBuildSettings(GauntletScenePath));

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!Application.isBatchMode && File.Exists(GauntletScenePath))
                {
                    EditorSceneManager.OpenScene(GauntletScenePath, OpenSceneMode.Single);
                }

                Debug.Log("[Splashdown] Course 1 is ready. Open '" + GauntletScenePath + "' and press Play.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Splashdown] Building Course 1 failed: " + exception);
                return false;
            }
            finally
            {
                overwriteGenerated = false;
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ project hooks

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == path)
                {
                    scene.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Makes the main menu's PLAY button start Course 1. Only the scene name and best-time id are
        /// changed, and only if the menu still points at the Foundation 1 course.
        /// </summary>
        private static void PointMainMenuAtCourse1()
        {
            if (!File.Exists(MainMenuScenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MainMenuController menu = root.GetComponentInChildren<MainMenuController>(true);
                if (menu == null || menu.courseSceneName != SceneNames.CourseFoundation1)
                {
                    continue;
                }

                menu.courseSceneName = SceneNames.CourseGauntlet;
                menu.courseId = GauntletCourseId;
                EditorUtility.SetDirty(menu);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }
        }

        /// <summary>
        /// The character to put in Course 1: whatever player prefab the existing course scene uses
        /// (so an integrated character carries over), falling back to the placeholder.
        /// </summary>
        private static GameObject ResolvePlayerPrefab(PrefabSet prefabs)
        {
            // Phase 3B integration: the integrated character lives in the cowgirl course copy
            // (CowgirlIntegration.ScenePath); Course_Foundation1 still holds the placeholder mannequin.
            // Look there first so Course 1 gets the latest working character.
            foreach (string scenePath in new[] { CowgirlIntegration.ScenePath, CourseScenePath })
            {
                GameObject found = PlayerPrefabUsedBy(scenePath);
                if (found != null)
                {
                    return found;
                }
            }

            return prefabs.Player;
        }

        private static GameObject PlayerPrefabUsedBy(string scenePath)
        {
            if (File.Exists(scenePath))
            {
                GameObject best = null;
                foreach (string dependency in AssetDatabase.GetDependencies(scenePath, true))
                {
                    if (!dependency.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(dependency);
                    if (candidate != null && candidate.GetComponent<PlayerCharacter>() != null &&
                        (best == null || dependency != PlayerPrefabPath))
                    {
                        best = candidate;
                    }
                }

                return best;
            }

            return null;
        }

        // ------------------------------------------------------------------ frames

        private readonly struct LegFrame
        {
            public readonly Vector3 Origin;
            public readonly Quaternion Rotation;

            public LegFrame(CourseLeg leg)
            {
                Origin = new Vector3(leg.OriginX, 0f, leg.OriginZ);
                Rotation = Quaternion.Euler(0f, leg.YawDegrees, 0f);
            }

            public Vector3 Point(float x, float y, float z)
            {
                return Origin + Rotation * new Vector3(x, y, z);
            }
        }

        private static LegFrame Frame(int leg)
        {
            return new LegFrame(Course1Layout.Legs[leg]);
        }

        private static GameObject PlaceOnLeg(GameObject prefab, Transform parent, int leg, float x, float y, float z, string name,
            float extraYaw = 0f)
        {
            LegFrame frame = Frame(leg);
            GameObject instance = InstantiatePrefab(prefab, parent, frame.Point(x, y, z), frame.Rotation * Quaternion.Euler(0f, extraYaw, 0f));
            instance.name = name;
            RecordOverrides(instance);
            return instance;
        }

        // ------------------------------------------------------------------ scene

        private static void BuildGauntletScene(ContentAssets assets, PrefabSet prefabs, ArenaAssets arena, Course1Prefabs c1)
        {
            if (!overwriteGenerated && File.Exists(GauntletScenePath))
            {
                return;
            }

            GameObject playerPrefab = ResolvePlayerPrefab(prefabs);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureArenaLighting(arena);

            Transform environment = new GameObject("Arena").transform;
            BuildStadium(environment, assets, arena);

            GameObject water = InstantiatePrefab(c1.Water, environment,
                new Vector3((ArenaMinX + ArenaMaxX) * 0.5f, Course1Layout.WaterLevel, (ArenaMinZ + ArenaMaxZ) * 0.5f), Quaternion.identity);
            var hazard = water.GetComponent<WaterHazard>();
            hazard.surfaceHeight = Course1Layout.WaterLevel;
            RecordOverrides(hazard);

            Transform surfaces = new GameObject("Course Surfaces").transform;
            Transform obstacles = new GameObject("Obstacles").transform;
            BuildCourseSurfaces(surfaces, obstacles, assets, prefabs, arena, c1);
            BuildSectionA(obstacles, c1);
            BuildSectionB(obstacles, c1);
            BuildSectionC(obstacles, c1);
            BuildSectionD(obstacles, assets, arena, c1);
            BuildSectionE(obstacles, c1);

            // ---- Course logic
            var courseObject = new GameObject("Course");
            var checkpointManager = courseObject.AddComponent<CheckpointManager>();
            var course = courseObject.AddComponent<CourseManager>();
            course.courseId = GauntletCourseId;

            Transform logic = courseObject.transform;
            checkpointManager.checkpoints = new[]
            {
                // leg, x, z, facing offset, trigger width, trigger depth, spawn offset along facing
                PlaceArenaCheckpoint(prefabs.Checkpoint, logic, checkpointManager, 0, "Checkpoint_0_Start", 0, 0f, 3f, 0f, 12f, 6f, 0f),
                PlaceArenaCheckpoint(prefabs.Checkpoint, logic, checkpointManager, 1, "Checkpoint_1_BigBounce", 0, 0f, 61.5f, 0f, 10f, 3f, 0.7f),
                PlaceArenaCheckpoint(prefabs.Checkpoint, logic, checkpointManager, 2, "Checkpoint_2_SpinCity", 0, 0f, 110.3f, 90f, 12f, 12f, 0f),
                PlaceArenaCheckpoint(prefabs.Checkpoint, logic, checkpointManager, 3, "Checkpoint_3_ChaosCrossing", 1, 0f, 70.5f, 90f, 12f, 12f, 0f),
                PlaceArenaCheckpoint(prefabs.Checkpoint, logic, checkpointManager, 4, "Checkpoint_4_FinalSplash", 2, 0f, 56f, 0f, 10f, 4f, 0.7f)
            };

            PlaceGate(c1.SectionGate, logic, 0, 0f, 11f, 0);
            PlaceGate(c1.SectionGate, logic, 0, 0f, 64.4f, 1);
            PlaceGate(c1.SectionGate, logic, 1, 0f, -1.2f, 2);
            PlaceGate(c1.SectionGate, logic, 2, 0f, -1.2f, 3);
            PlaceGate(c1.SectionGate, logic, 2, 0f, 60.4f, 4);

            GameObject finish = PlaceOnLeg(c1.FinishGate, logic, 2, 0f, 1.6f, 106.5f, "FinishGate");
            var finishLine = finish.GetComponent<FinishLine>();
            finishLine.course = course;
            RecordOverrides(finishLine);
            var celebration = finish.GetComponentInChildren<FinishCelebration>(true);
            celebration.course = course;
            RecordOverrides(celebration);

            var impactEffects = courseObject.AddComponent<ImpactEffects>();
            impactEffects.course = course;
            impactEffects.knockdownBurstPrefab = c1.ImpactBurst;

            // ---- Player and camera
            LegFrame startFrame = Frame(0);
            GameObject playerObject = InstantiatePrefab(playerPrefab, null, startFrame.Point(0f, 0.02f, 3f), startFrame.Rotation);
            playerObject.name = "Player";
            var player = playerObject.GetComponent<PlayerCharacter>();

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = SkyColor;
            camera.fieldOfView = assets.Camera.fieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 900f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -2.5f), Quaternion.Euler(16f, 0f, 0f));
            EnablePostProcessing(camera);

            var gameCamera = cameraObject.AddComponent<ThirdPersonCamera>();
            gameCamera.target = player;
            gameCamera.config = assets.Camera;
            gameCamera.controlledCamera = camera;

            var motor = playerObject.GetComponent<CharacterMotor>();
            if (motor != null)
            {
                motor.viewTransform = cameraObject.transform;
                RecordOverrides(motor);
            }

            var respawner = playerObject.GetComponent<PlayerRespawner>();
            if (respawner != null)
            {
                respawner.checkpoints = checkpointManager;
                respawner.killHeight = Course1Layout.WaterLevel - 12f;
                RecordOverrides(respawner);
            }

            RecordOverrides(playerObject);

            course.player = player;
            course.checkpoints = checkpointManager;
            course.gameCamera = gameCamera;

            // ---- Systems, ambience and UI
            InstantiatePrefab(prefabs.AudioManager, null, Vector3.zero, Quaternion.identity);
            var ambience = new GameObject("Arena Ambience");
            ambience.AddComponent<AudioSource>().playOnAwake = false;
            ambience.AddComponent<ArenaAmbience>();
            CreateEventSystem();
            BuildCourseUi(course);

            SaveScene(scene, GauntletScenePath);
        }

        private static void ConfigureArenaLighting(ArenaAssets arena)
        {
            // One warm sun with soft shadows, a bright flat ambient and a light haze for depth. No baking.
            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.84f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunObject.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            RenderSettings.sun = sun;
            if (arena.Sky != null && arena.Sky.shader != null && arena.Sky.shader.name.StartsWith("Skybox"))
            {
                RenderSettings.skybox = arena.Sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.90f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.84f, 0.80f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.62f, 0.66f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.88f, 1f);
            RenderSettings.fogStartDistance = 170f;
            RenderSettings.fogEndDistance = 700f;
        }

        /// <summary>
        /// Turns on post-processing for the camera. The URP camera component is addressed by name so
        /// this assembly does not need a compile-time reference to the render pipeline package.
        /// </summary>
        private static void EnablePostProcessing(Camera camera)
        {
            Type type = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type == null)
            {
                Debug.LogWarning("[Splashdown] URP camera data type not found; post-processing was left off.");
                return;
            }

            Component data = camera.GetComponent(type);
            if (data == null)
            {
                data = camera.gameObject.AddComponent(type);
            }

            var serialized = new SerializedObject(data);
            SerializedProperty property = serialized.FindProperty("m_RenderPostProcessing");
            if (property == null)
            {
                Debug.LogWarning("[Splashdown] Could not enable post-processing on the camera (property not found).");
                return;
            }

            property.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ walkable surfaces

        private static Material SectionMaterial(char section, ArenaAssets arena)
        {
            switch (section)
            {
                case 'A': return arena.PanelsPink;
                case 'B': return arena.PanelsBlue;
                case 'C': return arena.PanelsYellow;
                case 'D': return arena.PanelsBlue;
                default: return arena.PanelsRainbow;
            }
        }

        private static Material SectionTrim(char section, ContentAssets assets)
        {
            switch (section)
            {
                case 'A': return assets.Yellow;
                case 'B': return assets.Orange;
                case 'C': return assets.HotPink;
                case 'D': return assets.Purple;
                default: return assets.Turquoise;
            }
        }

        private static void BuildCourseSurfaces(Transform parent, Transform obstacles, ContentAssets assets, PrefabSet prefabs,
            ArenaAssets arena, Course1Prefabs c1)
        {
            int movers = 0;
            int discs = 0;
            foreach (CourseSurface surface in Course1Layout.Surfaces)
            {
                LegFrame frame = Frame(surface.Leg);
                Vector3 position = frame.Point(surface.X, surface.Height, surface.Z);

                if (surface.Kind == SurfaceKind.MovingPlatform)
                {
                    GameObject mover = InstantiatePrefab(prefabs.MovingPlatform, obstacles, position, frame.Rotation);
                    mover.name = surface.Name;
                    var platform = mover.GetComponent<MovingPlatform>();
                    platform.waypoints = new[] { Vector3.zero, new Vector3(surface.MoveX, 0f, surface.MoveZ) };
                    platform.startTimeOffset = movers * 1.4f;
                    RecordOverrides(platform);
                    RecordOverrides(mover);
                    movers++;
                    continue;
                }

                if (surface.Kind == SurfaceKind.SpinDisc)
                {
                    GameObject disc = InstantiatePrefab(c1.SpinPlatform, obstacles, position, frame.Rotation);
                    disc.name = surface.Name;
                    var spinner = disc.GetComponent<RotatingSweeper>();
                    spinner.rotationSpeed = discs % 2 == 0 ? 28f : -40f;
                    RecordOverrides(spinner);
                    RecordOverrides(disc);
                    FoamRing(parent, new Vector3(position.x, Course1Layout.WaterLevel + 0.04f, position.z), 7f, arena);
                    discs++;
                    continue;
                }

                var container = new GameObject(surface.Name);
                container.transform.SetParent(parent, false);
                container.transform.SetPositionAndRotation(position, frame.Rotation);
                BuildStaticSurface(container.transform, surface, assets, arena);
            }
        }

        private static void BuildStaticSurface(Transform root, CourseSurface surface, ContentAssets assets, ArenaAssets arena)
        {
            const float thickness = 1f;
            bool plain = surface.Kind == SurfaceKind.Checkpoint || surface.Kind == SurfaceKind.Start || surface.Kind == SurfaceKind.Finish;
            Material body = surface.Kind == SurfaceKind.Pad ? arena.Target
                : plain ? assets.White
                : SectionMaterial(surface.Section, arena);
            Material trim = SectionTrim(surface.Section, assets);
            float pillarRadius;

            if (surface.Shape == SurfaceShape.Disc)
            {
                float radius = surface.Radius;
                // Cylinder primitive: 2 units tall, 1 wide. Swap its capsule collider for the true shape.
                GameObject deck = Primitive(PrimitiveType.Cylinder, "Deck", root, new Vector3(0f, -thickness * 0.5f, 0f),
                    new Vector3(radius * 2f, thickness * 0.5f, radius * 2f), body, GameLayers.Ground, false);
                var collider = deck.AddComponent<MeshCollider>();
                collider.sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = true;
                Primitive(PrimitiveType.Cylinder, "Trim", root, new Vector3(0f, -0.45f, 0f),
                    new Vector3(radius * 2f + 0.6f, 0.28f, radius * 2f + 0.6f), trim, GameLayers.Default, false);
                pillarRadius = Mathf.Max(0.6f, radius * 0.4f);
            }
            else
            {
                Primitive(PrimitiveType.Cube, "Deck", root, new Vector3(0f, -thickness * 0.5f, 0f),
                    new Vector3(surface.Width, thickness, surface.Length), body, GameLayers.Ground, true);
                Primitive(PrimitiveType.Cube, "Trim", root, new Vector3(0f, -0.45f, 0f),
                    new Vector3(surface.Width + 0.6f, 0.5f, surface.Length + 0.6f), trim, GameLayers.Default, false);

                // Direction arrows on anything you run straight along.
                if (plain || surface.Kind == SurfaceKind.Bridge)
                {
                    Material chevron = surface.Section == 'A' || surface.Section == 'C' || surface.Section == 'E'
                        ? arena.ChevronPink
                        : arena.ChevronBlue;
                    float width = Mathf.Min(surface.Width - 0.6f, 3f);
                    Primitive(PrimitiveType.Cube, "Arrows", root, new Vector3(0f, 0.011f, 0f),
                        new Vector3(width, 0.02f, surface.Length - 1f), chevron, GameLayers.Default, false);
                }

                pillarRadius = Mathf.Max(0.5f, Mathf.Min(surface.Width, surface.Length) * 0.3f);
            }

            // Support column down into the water, with a foam ring where it meets the surface.
            float top = -thickness;
            float bottom = Course1Layout.WaterLevel - surface.Height - 5f;
            Primitive(PrimitiveType.Cylinder, "Support", root, new Vector3(0f, (top + bottom) * 0.5f, 0f),
                new Vector3(pillarRadius * 2f, (top - bottom) * 0.5f, pillarRadius * 2f), assets.Dark, GameLayers.Default, false);
            FoamRing(root, root.position + Vector3.up * (Course1Layout.WaterLevel - surface.Height + 0.04f), pillarRadius * 2f * 2.2f, arena);
        }

        private static void FoamRing(Transform parent, Vector3 worldPosition, float diameter, ArenaAssets arena)
        {
            GameObject foam = GameObject.CreatePrimitive(PrimitiveType.Quad);
            foam.name = "Foam";
            Object.DestroyImmediate(foam.GetComponent<Collider>());
            foam.transform.SetParent(parent, true);
            foam.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(90f, 0f, 0f));
            foam.transform.localScale = new Vector3(diameter, diameter, 1f);
            var renderer = foam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = arena.Foam;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ sections

        /// <summary>A — The Launch Pad: two slow, short sweepers with wide safe lanes either side.</summary>
        private static void BuildSectionA(Transform parent, Course1Prefabs c1)
        {
            SetSweeperSpeed(PlaceOnLeg(c1.SweeperSmall, parent, 0, 0f, 0f, 43f, "A_Sweeper_1"), 55f, 0f);
            SetSweeperSpeed(PlaceOnLeg(c1.SweeperSmall, parent, 0, 0f, 0f, 52.4f, "A_Sweeper_2"), -62f, 90f);
        }

        /// <summary>B — The Big Bounce: three balls on a wide alley, then two giant fast balls that fill a narrow one.</summary>
        private static void BuildSectionB(Transform parent, Course1Prefabs c1)
        {
            PlaceArenaBall(c1.Ball, parent, "B_Ball_1", 70.2f, 1f, 3f, -0.8f, 2.4f, 1.8f);
            PlaceArenaBall(c1.Ball, parent, "B_Ball_2", 74.7f, 1f, 3f, 0.3f, 3.0f, 2.2f);
            PlaceArenaBall(c1.Ball, parent, "B_Ball_3", 79.2f, 1f, 3f, 0.9f, 2.2f, 1.6f);
            // Giant balls span the whole narrow alley: the way through is to run under them at the top of the bounce.
            PlaceArenaBall(c1.Ball, parent, "B_GiantBall_1", 87.8f, 1.35f, 1.1f, -1f, 3.0f, 2.7f);
            PlaceArenaBall(c1.Ball, parent, "B_GiantBall_2", 93.2f, 1.35f, 1.1f, 1f, 3.4f, 2.5f);
        }

        /// <summary>C — Spin City: (spin discs come from the layout) a big sweeper island, then counter-rotating beams on a bridge.</summary>
        private static void BuildSectionC(Transform parent, Course1Prefabs c1)
        {
            SetSweeperSpeed(PlaceOnLeg(c1.SweeperLarge, parent, 1, 0f, 0f, 34.5f, "C_Sweeper"), 75f, 0f);
            SetSweeperSpeed(PlaceOnLeg(c1.Beam, parent, 1, 0f, 0f, 52.5f, "C_Beam_1"), 95f, 0f);
            SetSweeperSpeed(PlaceOnLeg(c1.Beam, parent, 1, 0f, 0f, 59.5f, "C_Beam_2"), -110f, 45f);
        }

        /// <summary>D — The Chaos Crossing: pendulum bridge, piston walls, then pendulums and a beam combined.</summary>
        private static void BuildSectionD(Transform parent, ContentAssets assets, ArenaAssets arena, Course1Prefabs c1)
        {
            const float pivotHeight = 6.55f; // rod 5.5 + weight radius 0.9 + 0.15 m clearance above the deck

            PlacePendulum(c1.Pendulum, parent, "D_Pendulum_1", 4.5f, pivotHeight, 3.2f, 0f);
            PlacePendulum(c1.Pendulum, parent, "D_Pendulum_2", 8.5f, pivotHeight, 2.9f, 0.33f);
            PlacePendulum(c1.Pendulum, parent, "D_Pendulum_3", 12.5f, pivotHeight, 3.4f, 0.66f);

            // Pistons rest beside the deck and push across it; they alternate sides and are staggered in time.
            PlacePiston(c1.Piston, parent, assets, arena, "D_Piston_1", -6f, 25.5f, 7f, 0f);
            PlacePiston(c1.Piston, parent, assets, arena, "D_Piston_2", 6f, 29.5f, -7f, 2.2f);
            PlacePiston(c1.Piston, parent, assets, arena, "D_Piston_3", -6f, 33.5f, 7f, 4.4f);

            PlacePendulum(c1.Pendulum, parent, "D_Pendulum_4", 41.5f, pivotHeight, 2.8f, 0.1f);
            SetSweeperSpeed(PlaceOnLeg(c1.Beam, parent, 2, 0f, 0f, 46f, "D_Beam"), 100f, 0f);
            PlacePendulum(c1.Pendulum, parent, "D_Pendulum_5", 50.2f, pivotHeight, 3.1f, 0.6f);
        }

        /// <summary>E — The Final Splash: a large sweeper with low bars to jump and high bars that punish a badly timed jump.</summary>
        private static void BuildSectionE(Transform parent, Course1Prefabs c1)
        {
            SetSweeperSpeed(PlaceOnLeg(c1.SweeperDouble, parent, 2, 0f, 0f, 71.1f, "E_DoubleSweeper"), 85f, 0f);
        }

        private static void SetSweeperSpeed(GameObject instance, float speed, float startAngle)
        {
            var sweeper = instance.GetComponent<RotatingSweeper>();
            sweeper.rotationSpeed = speed;
            sweeper.startAngle = startAngle;
            RecordOverrides(sweeper);
        }

        private static void PlaceArenaBall(GameObject prefab, Transform parent, string name, float z, float scale, float patrolHalfWidth,
            float startPhase, float travelSpeed, float bounceHeight)
        {
            const float baseDiameter = 2.2f;
            LegFrame frame = Frame(0);
            float radius = baseDiameter * 0.5f * scale;
            GameObject instance = InstantiatePrefab(prefab, parent, frame.Point(0f, radius + 1.3f, z), Quaternion.identity);
            instance.name = name;
            instance.transform.localScale = Vector3.one * (baseDiameter * scale);

            var ball = instance.GetComponent<BouncingBall>();
            // The ball patrols in world axes, so the sideways direction is rotated into the leg's frame here.
            ball.patrolOffset = frame.Rotation * new Vector3(patrolHalfWidth, 0f, 0f);
            ball.startPhase = startPhase;
            ball.travelSpeed = travelSpeed;
            ball.bounceHeight = bounceHeight;
            RecordOverrides(ball);
            RecordOverrides(instance.transform);
            RecordOverrides(instance);
        }

        private static void PlacePendulum(GameObject prefab, Transform parent, string name, float z, float pivotHeight, float period,
            float startPhase)
        {
            GameObject instance = PlaceOnLeg(prefab, parent, 2, 0f, pivotHeight, z, name);
            var pendulum = instance.GetComponent<Pendulum>();
            pendulum.period = period;
            pendulum.startPhase = startPhase;
            RecordOverrides(pendulum);
        }

        private static void PlacePiston(GameObject prefab, Transform parent, ContentAssets assets, ArenaAssets arena, string name,
            float restX, float z, float travelX, float startTimeOffset)
        {
            GameObject instance = PlaceOnLeg(prefab, parent, 2, restX, 0f, z, name);
            var mover = instance.GetComponent<MovingPlatform>();
            mover.waypoints = new[] { Vector3.zero, new Vector3(travelX, 0f, 0f) };
            mover.startTimeOffset = startTimeOffset;
            RecordOverrides(mover);

            // The pontoon the piston rests on (visual only).
            LegFrame frame = Frame(2);
            var dock = new GameObject(name + "_Dock");
            dock.transform.SetParent(parent, false);
            dock.transform.SetPositionAndRotation(frame.Point(restX, 0f, z), frame.Rotation);
            Primitive(PrimitiveType.Cube, "Pontoon", dock.transform, new Vector3(0f, -0.55f, 0f), new Vector3(5f, 1f, 2.2f),
                arena.PanelsYellow, GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Support", dock.transform, new Vector3(0f, -4.5f, 0f), new Vector3(1.4f, 3.5f, 1.4f),
                assets.Dark, GameLayers.Default, false);
        }

        private static Checkpoint PlaceArenaCheckpoint(GameObject prefab, Transform parent, CheckpointManager manager, int index,
            string name, int leg, float x, float z, float facingOffset, float triggerWidth, float triggerDepth, float spawnForward)
        {
            GameObject instance = PlaceOnLeg(prefab, parent, leg, x, 0f, z, name, facingOffset);

            var checkpoint = instance.GetComponent<Checkpoint>();
            checkpoint.index = index;
            checkpoint.manager = manager;
            RecordOverrides(checkpoint);

            // Size the trigger to the platform so the checkpoint cannot be skirted.
            var trigger = instance.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(triggerWidth, 4f, triggerDepth);
            RecordOverrides(trigger);

            if (checkpoint.spawnPoint != null)
            {
                checkpoint.spawnPoint.localPosition = new Vector3(0f, 0f, spawnForward);
                RecordOverrides(checkpoint.spawnPoint);
            }

            return checkpoint;
        }

        private static void PlaceGate(GameObject prefab, Transform parent, int leg, float x, float z, int sectionIndex)
        {
            char letter = (char)('A' + sectionIndex);
            string title = letter + " - " + Course1Layout.SectionNames[sectionIndex];
            GameObject gate = PlaceOnLeg(prefab, parent, leg, x, 0f, z, "Gate_" + letter);

            var announcer = gate.GetComponent<SectionAnnouncer>();
            announcer.sectionName = Course1Layout.SectionNames[sectionIndex];
            RecordOverrides(announcer);

            Text label = gate.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = title;
                RecordOverrides(label);
            }
        }
    }
}
