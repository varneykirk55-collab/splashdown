using System;
using System.Collections.Generic;
using System.IO;
using BikiniWipeout.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Generates Course 2, "Chaos Cove", as a NEW scene. Every walkable surface, hazard, checkpoint and
    /// gate comes from <see cref="Course2Layout"/> (the same data the completability tests check).
    /// Course 1, the Foundation scenes, the player prefab and all config assets are never touched.
    ///
    /// Menu: Bikini Wipeout > Course 2 > Build Chaos Cove
    /// Batch: -executeMethod BikiniWipeout.EditorTools.Foundation1Builder.BuildCourse2Batch
    /// </summary>
    public static partial class Foundation1Builder
    {
        public const string ChaosCoveScenePath = ScenesFolder + "/" + SceneNames.CourseChaosCove + ".unity";
        public const string ChaosCoveCourseId = "chaoscove2";

        private const float CoveMinX = -36f, CoveMaxX = 132f, CoveMinZ = -40f, CoveMaxZ = 186f;

        [MenuItem("Bikini Wipeout/Course 2/Build Chaos Cove (Missing Only)", priority = 70)]
        public static void BuildCourse2Menu()
        {
            if (ConfirmEditMode() && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                BuildCourse2(false);
            }
        }

        [MenuItem("Bikini Wipeout/Course 2/Rebuild Chaos Cove (Overwrite Course 2 Scene and Prefabs)", priority = 71)]
        public static void RebuildCourse2Menu()
        {
            if (!ConfirmEditMode())
            {
                return;
            }

            bool proceed = EditorUtility.DisplayDialog("Rebuild Course 2",
                "This regenerates the Course 2 scene and the prefabs under Prefabs/Course2 and OVERWRITES manual edits to them.\n\n" +
                "Course 1, the Foundation scenes, the player prefab, config assets and existing materials are kept.", "Rebuild", "Cancel");
            if (proceed && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                BuildCourse2(true);
            }
        }

        [MenuItem("Bikini Wipeout/Course 2/Open Course 2 Scene", priority = 72)]
        public static void OpenCourse2Scene()
        {
            OpenScene(ChaosCoveScenePath);
        }

        /// <summary>
        /// Re-registers Course 2 with the main menu. Needed only after the MainMenu scene has been rebuilt
        /// (for example with Bikini Wipeout > UI > Rebuild Main Menu Scene), which resets its course list.
        /// </summary>
        [MenuItem("Bikini Wipeout/Course 2/Add Course 2 To Main Menu", priority = 73)]
        public static void AddCourse2ToMainMenuMenu()
        {
            if (!ConfirmEditMode() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!IsCourse2Built())
            {
                EditorUtility.DisplayDialog("Course 2", "Build Course 2 first: Bikini Wipeout > Course 2 > Build Chaos Cove.", "OK");
                return;
            }

            AddCourse2ButtonToMainMenu();
            AddSceneToBuildSettings(ChaosCoveScenePath);
            Debug.Log("[Splashdown] Course 2 is registered with the main menu.");
        }

        /// <summary>Command-line entry point. Exits with code 0 on success, 1 on failure.</summary>
        public static void BuildCourse2Batch()
        {
            EditorApplication.Exit(BuildCourse2(true) ? 0 : 1);
        }

        public static bool IsCourse2Built()
        {
            return File.Exists(ChaosCoveScenePath);
        }

        /// <summary>
        /// Builds the Course 2 materials, prefabs and scene. Everything it shares with earlier phases
        /// (Foundation prefabs, arena materials, Course 1 prefabs) is created only if missing, never overwritten.
        /// </summary>
        public static bool BuildCourse2(bool overwrite)
        {
            try
            {
                if (!IsContentBuilt() && !BuildAll(false))
                {
                    throw new InvalidOperationException("Foundation 1 content could not be generated (see the error above).");
                }

                overwriteGenerated = false;
                ContentAssets assets = null;
                PrefabSet prefabs = null;
                ArenaAssets arena = null;
                Course1Prefabs course1 = null;
                Step("Shared assets", 0.05f, () =>
                {
                    EnsureFolders();
                    assets = BuildSharedAssets();
                    prefabs = BuildPrefabs(assets);
                    arena = BuildArenaAssets();
                    course1 = BuildCourse1Prefabs(assets, arena);
                });

                overwriteGenerated = overwrite;
                CoveAssets cove = null;
                Course2Prefabs course2 = null;
                Step("Course 2 materials and audio", 0.2f, () => cove = BuildCoveAssets());
                Step("Course 2 prefabs", 0.35f, () => course2 = BuildCourse2Prefabs(assets, arena, cove));
                Step("Main menu button", 0.5f, AddCourse2ButtonToMainMenu);
                Step("Course 2 scene", 0.6f, () => BuildChaosCoveScene(assets, prefabs, arena, course1, cove, course2));
                Step("Build settings", 0.95f, () => AddSceneToBuildSettings(ChaosCoveScenePath));

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!Application.isBatchMode && File.Exists(ChaosCoveScenePath))
                {
                    EditorSceneManager.OpenScene(ChaosCoveScenePath, OpenSceneMode.Single);
                }

                Debug.Log("[Splashdown] Course 2 is ready. Open '" + ChaosCoveScenePath + "' and press Play.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Splashdown] Building Course 2 failed: " + exception);
                return false;
            }
            finally
            {
                overwriteGenerated = false;
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Makes Course 2 reachable from the main menu without replacing the menu.
        /// If the menu has a course registry (the UI foundation's serialized "courses" array), the reserved
        /// Course 2 entry is pointed at this scene and marked ready; otherwise a COURSE 2 button is added
        /// beside PLAY. The registry is addressed by serialized property name, so this builder compiles and
        /// works with either version of the menu. Nothing else in the menu scene is changed.
        /// </summary>
        private static void AddCourse2ButtonToMainMenu()
        {
            if (!File.Exists(MainMenuScenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MainMenuController menu = root.GetComponentInChildren<MainMenuController>(true);
                if (menu == null)
                {
                    continue;
                }

                bool changed = RegisterCourse2InMenuRegistry(menu, out bool hasRegistry);
                if (!hasRegistry)
                {
                    changed = AddStandaloneCourse2Button(menu, root);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                return;
            }
        }

        private static bool RegisterCourse2InMenuRegistry(MainMenuController menu, out bool hasRegistry)
        {
            var serialized = new SerializedObject(menu);
            SerializedProperty courses = serialized.FindProperty("courses");
            hasRegistry = courses != null && courses.isArray;
            if (!hasRegistry)
            {
                return false;
            }

            // Prefer an entry that already names this scene, then the reserved (scene-less) slot, then a new entry.
            int target = -1;
            for (int i = 0; i < courses.arraySize && target < 0; i++)
            {
                SerializedProperty sceneName = courses.GetArrayElementAtIndex(i).FindPropertyRelative("sceneName");
                if (sceneName != null && sceneName.stringValue == SceneNames.CourseChaosCove)
                {
                    target = i;
                }
            }

            for (int i = 0; i < courses.arraySize && target < 0; i++)
            {
                SerializedProperty sceneName = courses.GetArrayElementAtIndex(i).FindPropertyRelative("sceneName");
                if (sceneName != null && string.IsNullOrEmpty(sceneName.stringValue))
                {
                    target = i;
                }
            }

            if (target < 0)
            {
                target = courses.arraySize;
                courses.InsertArrayElementAtIndex(target);
            }

            SerializedProperty entry = courses.GetArrayElementAtIndex(target);
            SerializedProperty title = entry.FindPropertyRelative("title");
            SerializedProperty scene = entry.FindPropertyRelative("sceneName");
            SerializedProperty ready = entry.FindPropertyRelative("ready");
            if (title == null || scene == null || ready == null)
            {
                Debug.LogWarning("[Splashdown] The menu's course registry has an unexpected shape; Course 2 was not registered. " +
                                 "Add it by hand: scene '" + SceneNames.CourseChaosCove + "'.");
                return false;
            }

            if (title.stringValue == "CHAOS COVE" && scene.stringValue == SceneNames.CourseChaosCove && ready.boolValue)
            {
                return false;
            }

            title.stringValue = "CHAOS COVE";
            scene.stringValue = SceneNames.CourseChaosCove;
            ready.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);
            return true;
        }

        private static bool AddStandaloneCourse2Button(MainMenuController menu, GameObject root)
        {
            foreach (CourseLaunchButton existing in root.GetComponentsInChildren<CourseLaunchButton>(true))
            {
                if (existing.sceneName == SceneNames.CourseChaosCove)
                {
                    return false;
                }
            }

            Transform parent = menu.playButton != null ? menu.playButton.transform.parent : menu.transform;
            Button button = UiButton("Course2Button", parent, "COURSE 2", new Vector2(430f, 110f), new Vector2(360f, 96f));
            button.gameObject.AddComponent<CourseLaunchButton>().sceneName = SceneNames.CourseChaosCove;
            return true;
        }

        // ------------------------------------------------------------------ frames

        private static LegFrame Frame2(int leg)
        {
            return new LegFrame(Course2Layout.Legs[leg]);
        }

        private static GameObject PlaceOnLeg2(GameObject prefab, Transform parent, int leg, float x, float y, float z, string name,
            float extraYaw = 0f)
        {
            LegFrame frame = Frame2(leg);
            GameObject instance = InstantiatePrefab(prefab, parent, frame.Point(x, y, z), frame.Rotation * Quaternion.Euler(0f, extraYaw, 0f));
            instance.name = name;
            RecordOverrides(instance);
            return instance;
        }

        // ------------------------------------------------------------------ scene

        private static void BuildChaosCoveScene(ContentAssets assets, PrefabSet prefabs, ArenaAssets arena, Course1Prefabs c1,
            CoveAssets cove, Course2Prefabs c2)
        {
            if (!overwriteGenerated && File.Exists(ChaosCoveScenePath))
            {
                return;
            }

            GameObject playerPrefab = ResolvePlayerPrefab(prefabs);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureArenaLighting(arena);

            Transform environment = new GameObject("Cove").transform;
            BuildCoveEnvironment(environment, assets, arena, cove, c2);

            GameObject water = InstantiatePrefab(c1.Water, environment,
                new Vector3((CoveMinX + CoveMaxX) * 0.5f, Course2Layout.WaterLevel, (CoveMinZ + CoveMaxZ) * 0.5f), Quaternion.identity);
            var hazard = water.GetComponent<WaterHazard>();
            hazard.surfaceHeight = Course2Layout.WaterLevel;
            RecordOverrides(hazard);

            Transform surfaces = new GameObject("Course Surfaces").transform;
            Transform obstacles = new GameObject("Obstacles").transform;
            var resetRoots = new List<Transform>();
            Transform arenaDisc = BuildCoveSurfaces(surfaces, obstacles, assets, arena, cove, c2, resetRoots);
            BuildCoveObstacles(obstacles, arenaDisc, c1, c2);

            // ---- Course logic
            var courseObject = new GameObject("Course");
            var checkpointManager = courseObject.AddComponent<CheckpointManager>();
            var course = courseObject.AddComponent<CourseManager>();
            course.courseId = ChaosCoveCourseId;
            Transform logic = courseObject.transform;

            var checkpoints = new Checkpoint[Course2Layout.Checkpoints.Count];
            for (int i = 0; i < checkpoints.Length; i++)
            {
                checkpoints[i] = PlaceCoveCheckpoint(c2.CheckpointGate, logic, checkpointManager, i, Course2Layout.Checkpoints[i]);
            }

            checkpointManager.checkpoints = checkpoints;

            for (int i = 0; i < Course2Layout.Gates.Count; i++)
            {
                PlaceCoveGate(c1.SectionGate, logic, Course2Layout.Gates[i], i);
            }

            CourseSurface finishSurface = Course2Layout.Find("FinishPlatform");
            GameObject finish = PlaceOnLeg2(c1.FinishGate, logic, finishSurface.Leg, 0f, finishSurface.Height, Course2Layout.FinishGateZ, "FinishGate");
            var finishLine = finish.GetComponent<FinishLine>();
            finishLine.course = course;
            RecordOverrides(finishLine);
            var celebration = finish.GetComponentInChildren<FinishCelebration>(true);
            celebration.course = course;
            RecordOverrides(celebration);

            var impactEffects = courseObject.AddComponent<ImpactEffects>();
            impactEffects.course = course;
            impactEffects.knockdownBurstPrefab = c1.ImpactBurst;

            // Collapsed bridge planks are put back whenever the character respawns.
            var resetter = courseObject.AddComponent<ObstacleResetter>();
            resetter.roots = resetRoots.ToArray();

            // ---- Player and camera (identical wiring to Course 1; whichever character prefab the project uses)
            LegFrame startFrame = Frame2(0);
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
                respawner.killHeight = Course2Layout.WaterLevel - 12f;
                RecordOverrides(respawner);
            }

            RecordOverrides(playerObject);

            course.player = player;
            course.checkpoints = checkpointManager;
            course.gameCamera = gameCamera;

            // ---- Systems, ambience and UI
            InstantiatePrefab(prefabs.AudioManager, null, Vector3.zero, Quaternion.identity);
            var ambience = new GameObject("Cove Ambience");
            ambience.AddComponent<AudioSource>().playOnAwake = false;
            ambience.AddComponent<ArenaAmbience>().audioEvent = AudioEventId.TropicalAmbience;
            CreateEventSystem();
            BuildCourseUi(course);

            SaveScene(scene, ChaosCoveScenePath);
        }

        // ------------------------------------------------------------------ walkable surfaces

        private static Material CoveSectionMaterial(char section, ArenaAssets arena, CoveAssets cove)
        {
            switch (section)
            {
                case 'A': return cove.DeckBlue;
                case 'B': return arena.PanelsPink;
                case 'C': return arena.PanelsYellow;
                case 'D': return cove.DeckBlue;
                case 'E': return arena.PanelsPink;
                default: return arena.PanelsRainbow;
            }
        }

        private static Material CoveSectionTrim(char section, ContentAssets assets)
        {
            switch (section)
            {
                case 'A': return assets.Yellow;
                case 'B': return assets.Yellow;
                case 'C': return assets.HotPink;
                case 'D': return assets.HotPink;
                case 'E': return assets.Turquoise;
                default: return assets.Yellow;
            }
        }

        /// <summary>Builds every surface in the layout. Returns the tilting deck of the floating arena (hazards are parented to it).</summary>
        private static Transform BuildCoveSurfaces(Transform parent, Transform obstacles, ContentAssets assets, ArenaAssets arena,
            CoveAssets cove, Course2Prefabs c2, List<Transform> resetRoots)
        {
            Transform arenaDisc = null;
            int movers = 0;
            foreach (CourseSurface surface in Course2Layout.Surfaces)
            {
                LegFrame frame = Frame2(surface.Leg);
                Vector3 position = frame.Point(surface.X, surface.Height, surface.Z);
                Vector3 waterPoint = new Vector3(position.x, Course2Layout.WaterLevel + 0.04f, position.z);

                switch (surface.Kind)
                {
                    case SurfaceKind.MovingPlatform:
                    {
                        GameObject mover = PlaceOnLeg2(c2.Mover, obstacles, surface.Leg, surface.X, surface.Height, surface.Z, surface.Name);
                        var platform = mover.GetComponent<MovingPlatform>();
                        platform.waypoints = new[] { Vector3.zero, new Vector3(surface.MoveX, 0f, surface.MoveZ) };
                        platform.startTimeOffset = movers * 1.3f;
                        RecordOverrides(platform);
                        movers++;
                        break;
                    }

                    case SurfaceKind.Seesaw:
                    {
                        GameObject prefab = surface.Width >= 3.9f ? c2.SeesawWide : c2.SeesawNarrow;
                        PlaceOnLeg2(prefab, obstacles, surface.Leg, surface.X, surface.Height, surface.Z, surface.Name);
                        FoamRing(parent, waterPoint, 4f, arena);
                        break;
                    }

                    case SurfaceKind.BouncePad:
                    {
                        GameObject pad = PlaceOnLeg2(c2.BouncePad, obstacles, surface.Leg, surface.X, surface.Height, surface.Z, surface.Name);
                        var bounce = pad.GetComponent<BouncePad>();
                        bounce.launchSpeed = surface.LaunchSpeed;
                        RecordOverrides(bounce);
                        FoamRing(parent, waterPoint, 2.4f, arena);
                        break;
                    }

                    case SurfaceKind.CollapsingBridge:
                        resetRoots.Add(BuildCollapsingBridge(obstacles, surface, assets, cove, c2));
                        break;

                    case SurfaceKind.FloatingArena:
                    {
                        GameObject raft = PlaceOnLeg2(c2.Arena, obstacles, surface.Leg, surface.X, surface.Height, surface.Z, surface.Name);
                        arenaDisc = raft.transform.Find("Disc");
                        FoamRing(parent, waterPoint, surface.Radius * 2.5f, arena);
                        break;
                    }

                    case SurfaceKind.BigBall:
                        PlaceOnLeg2(c2.BigBall, obstacles, surface.Leg, surface.X, surface.Height, surface.Z, surface.Name);
                        FoamRing(parent, waterPoint, 3f, arena);
                        break;

                    case SurfaceKind.SlideRamp:
                    case SurfaceKind.Ramp:
                        BuildIncline(parent, surface, frame, assets, arena, cove);
                        break;

                    default:
                    {
                        var container = new GameObject(surface.Name);
                        container.transform.SetParent(parent, false);
                        container.transform.SetPositionAndRotation(position, frame.Rotation);
                        BuildCoveStaticSurface(container.transform, surface, assets, arena, cove);
                        break;
                    }
                }
            }

            if (arenaDisc == null)
            {
                throw new InvalidOperationException("The floating arena prefab has no 'Disc' child.");
            }

            return arenaDisc;
        }

        private static void BuildCoveStaticSurface(Transform root, CourseSurface surface, ContentAssets assets, ArenaAssets arena, CoveAssets cove)
        {
            const float thickness = 1f;
            bool plain = surface.Kind == SurfaceKind.Checkpoint || surface.Kind == SurfaceKind.Start || surface.Kind == SurfaceKind.Finish;
            Material body = surface.Kind == SurfaceKind.Pad ? arena.Target
                : plain ? assets.White
                : CoveSectionMaterial(surface.Section, arena, cove);
            Material trim = CoveSectionTrim(surface.Section, assets);
            float pillarRadius;

            if (surface.Shape == SurfaceShape.Disc)
            {
                float radius = surface.Radius;
                SolidCylinder("Deck", root, new Vector3(0f, -thickness * 0.5f, 0f), radius, thickness, body, GameLayers.Ground);
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

                if (plain || surface.Kind == SurfaceKind.Bridge)
                {
                    Material chevron = surface.Section == 'B' || surface.Section == 'E' ? arena.ChevronBlue : arena.ChevronPink;
                    float width = Mathf.Min(surface.Width - 0.6f, 3f);
                    Primitive(PrimitiveType.Cube, "Arrows", root, new Vector3(0f, 0.011f, 0f),
                        new Vector3(width, 0.02f, surface.Length - 1f), chevron, GameLayers.Default, false);
                }

                pillarRadius = Mathf.Max(0.5f, Mathf.Min(surface.Width, surface.Length) * 0.28f);
            }

            float top = -thickness;
            float bottom = Course2Layout.WaterLevel - surface.Height - 5f;
            Primitive(PrimitiveType.Cylinder, "Support", root, new Vector3(0f, (top + bottom) * 0.5f, 0f),
                new Vector3(pillarRadius * 2f, (top - bottom) * 0.5f, pillarRadius * 2f), cove.PillarBlue, GameLayers.Default, false);
            FoamRing(root, root.position + Vector3.up * (Course2Layout.WaterLevel - surface.Height + 0.04f), pillarRadius * 2f * 2.2f, arena);
        }

        /// <summary>
        /// A sloping deck: the water slide (slick, with side rails) or a plain ramp. The surface's Height is its
        /// near end; the far end is Drop lower. The deck is one box rotated about its middle.
        /// </summary>
        private static void BuildIncline(Transform parent, CourseSurface surface, LegFrame frame, ContentAssets assets, ArenaAssets arena,
            CoveAssets cove)
        {
            bool slide = surface.Kind == SurfaceKind.SlideRamp;
            float pitch = Mathf.Atan2(surface.Drop, surface.Length) * Mathf.Rad2Deg;   // positive pitch lowers the far (+Z) end
            float sloped = Mathf.Sqrt(surface.Length * surface.Length + surface.Drop * surface.Drop);
            float middleHeight = surface.Height - surface.Drop * 0.5f;

            var container = new GameObject(surface.Name);
            container.transform.SetParent(parent, false);
            container.transform.SetPositionAndRotation(frame.Point(surface.X, middleHeight, surface.Z),
                frame.Rotation * Quaternion.Euler(pitch, 0f, 0f));
            Transform root = container.transform;

            GameObject deck = Primitive(PrimitiveType.Cube, "Deck", root, new Vector3(0f, -0.3f, 0f), new Vector3(surface.Width, 0.6f, sloped),
                slide ? cove.DeckBlue : CoveSectionMaterial(surface.Section, arena, cove), GameLayers.Ground, true);
            Primitive(PrimitiveType.Cube, "Trim", root, new Vector3(0f, -0.4f, 0f), new Vector3(surface.Width + 0.4f, 0.4f, sloped + 0.1f),
                slide ? assets.HotPink : CoveSectionTrim(surface.Section, assets), GameLayers.Default, false);

            if (slide)
            {
                GameObject sheet = Primitive(PrimitiveType.Cube, "WaterSheet", root, new Vector3(0f, 0.012f, 0f),
                    new Vector3(surface.Width - 1f, 0.02f, sloped), cove.SlideWater, GameLayers.Default, false);
                sheet.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var flow = sheet.AddComponent<ScrollingSurface>();
                flow.tiling = new Vector2(2f, sloped / 3f);
                flow.scrollSpeed = new Vector2(0f, 0.9f);
                flow.wobble = 0.01f;

                for (int side = -1; side <= 1; side += 2)
                {
                    // Solid inflatable rails keep a sliding character on the slide.
                    GameObject rail = CapsuleBar(side < 0 ? "RailLeft" : "RailRight", root,
                        new Vector3(side * (surface.Width * 0.5f - 0.25f), 0.3f, 0f), sloped, 0.7f,
                        side < 0 ? assets.HotPink : assets.Yellow, GameLayers.Ground, true);
                    rail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }

                // Water feeding the slide at the top and foaming off the bottom lip (two small looping sprays).
                ParticleSystem feed = WaterSpray("FeedSpray", root, new Vector3(0f, 0.25f, -sloped * 0.5f + 0.3f), Quaternion.identity,
                    30f, 2.5f, 4.5f, 0.5f, 0.8f, 0.2f, 0.45f, 22f, surface.Width * 0.3f, 0.6f, 36, true, assets.Splash);
                ParticleSystem lip = WaterSpray("LipSpray", root, new Vector3(0f, 0.2f, sloped * 0.5f - 0.2f), Quaternion.Euler(-35f, 0f, 0f),
                    26f, 2f, 4f, 0.4f, 0.7f, 0.2f, 0.5f, 30f, surface.Width * 0.3f, 1.2f, 30, true, assets.Splash);
                foreach (ParticleSystem spray in new[] { feed, lip })
                {
                    ParticleSystem.MainModule sprayMain = spray.main;
                    sprayMain.playOnAwake = true;
                }

                var slick = deck.AddComponent<SlipperySurface>();
                slick.shape = SlipShape.Collider;
                slick.surface = deck.GetComponent<BoxCollider>();
                slick.traction = 0.05f;
                slick.slideGravity = 14f;
                slick.maxSlideSpeed = 9f;
                slick.landingBounce = 0f;
                slick.landingHop = 0f;
                slick.ragdollBounciness = 0.1f;
                slick.slideEvent = AudioEventId.SlideWhoosh;
            }
            else
            {
                Primitive(PrimitiveType.Cube, "Arrows", root, new Vector3(0f, 0.011f, 0f),
                    new Vector3(Mathf.Min(surface.Width - 0.6f, 3f), 0.02f, sloped - 1f), arena.ChevronPink, GameLayers.Default, false);
            }

            // Two vertical supports (their own object, so they are not tilted with the deck).
            var supports = new GameObject(surface.Name + "_Supports");
            supports.transform.SetParent(parent, false);
            supports.transform.SetPositionAndRotation(frame.Point(surface.X, 0f, surface.Z), frame.Rotation);
            for (int end = -1; end <= 1; end += 2)
            {
                float z = end * surface.Length * 0.28f;
                float top = middleHeight - surface.Drop * (z / surface.Length) - 0.6f;
                float bottom = Course2Layout.WaterLevel - 5f;
                Primitive(PrimitiveType.Cylinder, end < 0 ? "SupportNear" : "SupportFar", supports.transform,
                    new Vector3(0f, (top + bottom) * 0.5f, z), new Vector3(1.4f, (top - bottom) * 0.5f, 1.4f), cove.PillarBlue,
                    GameLayers.Default, false);
                FoamRing(supports.transform, supports.transform.TransformPoint(new Vector3(0f, Course2Layout.WaterLevel + 0.04f, z)), 3.2f, arena);
            }
        }

        /// <summary>A row of collapsing planks filling the surface, alternating colours. Returns the container (for the resetter).</summary>
        private static Transform BuildCollapsingBridge(Transform parent, CourseSurface surface, ContentAssets assets, CoveAssets cove,
            Course2Prefabs c2)
        {
            LegFrame frame = Frame2(surface.Leg);
            var container = new GameObject(surface.Name);
            container.transform.SetParent(parent, false);
            container.transform.SetPositionAndRotation(frame.Point(surface.X, surface.Height, surface.Z), frame.Rotation);

            int count = Mathf.Max(1, Mathf.RoundToInt(surface.Length / CollapseSegmentLength));
            float start = surface.Z - count * CollapseSegmentLength * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float z = start + (i + 0.5f) * CollapseSegmentLength;
                GameObject segment = PlaceOnLeg2(c2.CollapseSegment, container.transform, surface.Leg, surface.X, surface.Height, z,
                    surface.Name + "_Plank_" + (i + 1));
                if (i % 2 == 1)
                {
                    foreach (MeshRenderer renderer in segment.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (renderer.name == "Plank")
                        {
                            renderer.sharedMaterial = assets.Yellow;
                            RecordOverrides(renderer);
                        }
                    }
                }
            }

            // Fixed side cables and end posts, so the bridge still reads as a bridge once planks have gone.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject cable = CapsuleBar(side < 0 ? "CableLeft" : "CableRight", container.transform,
                    new Vector3(side * 1.75f, -0.45f, 0f), surface.Length + 1.2f, 0.16f, cove.PillarBlue, GameLayers.Default, false);
                cable.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            for (int end = -1; end <= 1; end += 2)
            {
                Primitive(PrimitiveType.Cylinder, end < 0 ? "PostNear" : "PostFar", container.transform,
                    new Vector3(0f, -4.4f, end * (surface.Length * 0.5f + 0.5f)), new Vector3(0.7f, 4f, 0.7f), cove.PillarBlue,
                    GameLayers.Default, false);
            }

            return container.transform;
        }

        // ------------------------------------------------------------------ hazards

        private static void BuildCoveObstacles(Transform parent, Transform arenaDisc, Course1Prefabs c1, Course2Prefabs c2)
        {
            CourseSurface arenaSurface = null;
            foreach (CourseSurface surface in Course2Layout.Surfaces)
            {
                if (surface.Kind == SurfaceKind.FloatingArena)
                {
                    arenaSurface = surface;
                }
            }

            foreach (CourseObstacle obstacle in Course2Layout.Obstacles)
            {
                GameObject instance;
                switch (obstacle.Kind)
                {
                    case ObstacleKind.Sweeper:
                        instance = PlaceSpinner(c2.Sweeper, parent, obstacle);
                        break;
                    case ObstacleKind.SpinningArms:
                        instance = PlaceSpinner(c2.SpinningArms, parent, obstacle);
                        break;
                    case ObstacleKind.GiantArms:
                        instance = PlaceSpinner(c2.GiantArms, parent, obstacle);
                        break;
                    case ObstacleKind.DoubleSweeper:
                        instance = PlaceSpinner(c2.DoubleSweeper, parent, obstacle);
                        break;

                    case ObstacleKind.SwingingBall:
                    {
                        instance = PlaceOnLeg2(c2.SwingingBall, parent, obstacle.Leg, obstacle.X, obstacle.Y + SwingPivotHeight, obstacle.Z, obstacle.Name);
                        var pendulum = instance.GetComponent<Pendulum>();
                        pendulum.period = obstacle.A;
                        pendulum.startPhase = obstacle.B;
                        pendulum.amplitude = obstacle.C;
                        RecordOverrides(pendulum);
                        break;
                    }

                    case ObstacleKind.Geyser:
                    {
                        instance = PlaceOnLeg2(c2.Geyser, parent, obstacle.Leg, obstacle.X, obstacle.Y, obstacle.Z, obstacle.Name);
                        var jet = instance.GetComponent<WaterJet>();
                        jet.period = obstacle.A;
                        jet.cycleOffset = obstacle.B;
                        RecordOverrides(jet);
                        break;
                    }

                    case ObstacleKind.SideJet:
                    {
                        // The nozzle sits at chest height and sprays along the heading given in the layout.
                        instance = PlaceOnLeg2(c2.SideJet, parent, obstacle.Leg, obstacle.X, obstacle.Y + 0.9f, obstacle.Z, obstacle.Name, obstacle.C);
                        var jet = instance.GetComponent<WaterJet>();
                        jet.period = obstacle.A;
                        jet.cycleOffset = obstacle.B;
                        RecordOverrides(jet);
                        break;
                    }

                    case ObstacleKind.TippingBucket:
                    {
                        instance = PlaceOnLeg2(c2.Bucket, parent, obstacle.Leg, obstacle.X, obstacle.Y, obstacle.Z, obstacle.Name);
                        var bucket = instance.GetComponent<TippingBucket>();
                        bucket.period = obstacle.A;
                        bucket.cycleOffset = obstacle.B;
                        bucket.pushDirection = Quaternion.Euler(0f, obstacle.C, 0f) * Vector3.forward;
                        RecordOverrides(bucket);
                        break;
                    }

                    case ObstacleKind.BouncingBall:
                        instance = PlaceCoveBall(c1.Ball, parent, obstacle);
                        break;

                    default:
                        throw new InvalidOperationException("No builder for obstacle kind " + obstacle.Kind + " (" + obstacle.Name + ").");
                }

                // Hazards that stand on the floating arena lean with its deck. Free-rolling balls do not.
                bool onArena = arenaSurface != null && obstacle.Leg == arenaSurface.Leg && obstacle.Kind != ObstacleKind.BouncingBall
                    && CourseLayoutMath.Contains(arenaSurface, obstacle.X, obstacle.Z);
                if (onArena)
                {
                    instance.transform.SetParent(arenaDisc, true);
                    RecordOverrides(instance.transform);
                }
            }
        }

        private static GameObject PlaceSpinner(GameObject prefab, Transform parent, CourseObstacle obstacle)
        {
            GameObject instance = PlaceOnLeg2(prefab, parent, obstacle.Leg, obstacle.X, obstacle.Y, obstacle.Z, obstacle.Name);
            SetSweeperSpeed(instance, obstacle.A, obstacle.B);
            return instance;
        }

        private static GameObject PlaceCoveBall(GameObject prefab, Transform parent, CourseObstacle obstacle)
        {
            const float diameter = 2.2f;
            LegFrame frame = Frame2(obstacle.Leg);
            GameObject instance = InstantiatePrefab(prefab, parent, frame.Point(obstacle.X, obstacle.Y + diameter * 0.5f + 1.3f, obstacle.Z),
                Quaternion.identity);
            instance.name = obstacle.Name;
            instance.transform.localScale = Vector3.one * diameter;

            var ball = instance.GetComponent<BouncingBall>();
            // The ball patrols in world axes, so the direction along the leg is rotated into world space here.
            ball.patrolOffset = frame.Rotation * new Vector3(0f, 0f, obstacle.A);
            ball.startPhase = obstacle.B;
            ball.travelSpeed = obstacle.C;
            ball.bounceHeight = 1.8f;
            RecordOverrides(ball);
            RecordOverrides(instance.transform);
            RecordOverrides(instance);
            return instance;
        }

        // ------------------------------------------------------------------ checkpoints and gates

        private static Checkpoint PlaceCoveCheckpoint(GameObject prefab, Transform parent, CheckpointManager manager, int index,
            CourseCheckpoint data)
        {
            GameObject instance = PlaceOnLeg2(prefab, parent, data.Leg, data.X, data.Y, data.Z, data.Name, data.Yaw);

            var checkpoint = instance.GetComponent<Checkpoint>();
            checkpoint.index = index;
            checkpoint.manager = manager;
            RecordOverrides(checkpoint);

            // Sized to the platform so the checkpoint cannot be skirted.
            var trigger = instance.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(data.TriggerWidth, 4f, data.TriggerDepth);
            RecordOverrides(trigger);

            if (checkpoint.spawnPoint != null)
            {
                checkpoint.spawnPoint.localPosition = new Vector3(0f, 0f, index == 0 ? 0f : 0.7f);
                RecordOverrides(checkpoint.spawnPoint);
            }

            return checkpoint;
        }

        private static void PlaceCoveGate(GameObject prefab, Transform parent, CourseGate data, int sectionIndex)
        {
            char letter = (char)('A' + sectionIndex);
            string title = Course2Layout.SectionNames[sectionIndex];
            GameObject gate = PlaceOnLeg2(prefab, parent, data.Leg, data.X, data.Y, data.Z, "Gate_" + letter);

            var announcer = gate.GetComponent<SectionAnnouncer>();
            announcer.sectionName = title;
            RecordOverrides(announcer);

            Text label = gate.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = letter + " - " + title;
                RecordOverrides(label);
            }
        }
    }
}
