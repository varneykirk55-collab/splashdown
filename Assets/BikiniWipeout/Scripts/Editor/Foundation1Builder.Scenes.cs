using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BikiniWipeout.EditorTools
{
    public static partial class Foundation1Builder
    {
        /// <summary>Height of the water surface. Platform tops are at y = 0.</summary>
        private const float WaterLevel = -2.2f;

        private static readonly Color SkyColor = new Color(0.45f, 0.75f, 0.98f, 1f);

        // ------------------------------------------------------------------ main menu

        private static void BuildMainMenuScene(ContentAssets assets, PrefabSet prefabs)
        {
            if (!overwriteGenerated && File.Exists(MainMenuScenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            cameraObject.AddComponent<AudioListener>();

            InstantiatePrefab(prefabs.AudioManager, null, Vector3.zero, Quaternion.identity);
            CreateEventSystem();
            BuildMainMenuUi();

            SaveScene(scene, MainMenuScenePath);
        }

        // ------------------------------------------------------------------ course

        /// <summary>
        /// Start platform -> rotating sweeper island -> checkpoint -> bouncing balls -> checkpoint ->
        /// moving platforms -> finish. The course runs along +Z over water.
        /// </summary>
        private static void BuildCourseScene(ContentAssets assets, PrefabSet prefabs)
        {
            if (!overwriteGenerated && File.Exists(CourseScenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureLighting();

            // ---- Static environment
            Transform environment = new GameObject("Environment").transform;
            Platform("StartPlatform", environment, new Vector3(0f, 0f, 0f), new Vector3(10f, 1f, 12f), assets.Sand, assets);
            Plank("StartStripe", environment, new Vector3(0f, 0.011f, 4.5f), new Vector3(10f, 0.02f, 0.5f), assets.White, false);
            Plank("BridgeToSweeper", environment, new Vector3(0f, 0f, 6.85f), new Vector3(3f, 0.4f, 2.3f), assets.White, true);
            RoundPlatform("SweeperIsland", environment, new Vector3(0f, 0f, 14.5f), 7f, assets.Turquoise, assets);
            Plank("BridgeFromSweeper", environment, new Vector3(0f, 0f, 22.15f), new Vector3(3f, 0.4f, 2.3f), assets.White, true);
            Platform("CheckpointPlatform1", environment, new Vector3(0f, 0f, 26f), new Vector3(8f, 1f, 6f), assets.Sand, assets);
            Platform("BallAlley", environment, new Vector3(0f, 0f, 39.5f), new Vector3(9f, 1f, 18f), assets.Purple, assets);
            Platform("CheckpointPlatform2", environment, new Vector3(0f, 0f, 53f), new Vector3(8f, 1f, 6f), assets.Sand, assets);
            Platform("FinishPlatform", environment, new Vector3(0f, 0f, 85.5f), new Vector3(10f, 1f, 12f), assets.Sand, assets);
            BuildBoundaries(environment);
            BuildBuoys(environment, assets);

            GameObject water = InstantiatePrefab(prefabs.Water, environment, new Vector3(0f, WaterLevel, 43f), Quaternion.identity);
            var hazard = water.GetComponent<WaterHazard>();
            hazard.surfaceHeight = WaterLevel;
            RecordOverrides(hazard);

            // ---- Obstacles
            Transform obstacles = new GameObject("Obstacles").transform;
            InstantiatePrefab(prefabs.Sweeper, obstacles, new Vector3(0f, 0f, 14.5f), Quaternion.identity);

            PlaceBall(prefabs.Ball, obstacles, "BouncingBall_1", 34f, -0.8f, 2.4f, 1.8f, assets.Orange);
            PlaceBall(prefabs.Ball, obstacles, "BouncingBall_2", 39.5f, 0.3f, 3.0f, 2.2f, assets.Yellow);
            PlaceBall(prefabs.Ball, obstacles, "BouncingBall_3", 45f, 0.9f, 2.2f, 1.6f, assets.HotPink);

            PlaceMovingPlatform(prefabs.MovingPlatform, obstacles, "MovingPlatform_1", new Vector3(-3f, 0f, 59.5f),
                new Vector3(6f, 0f, 0f), 0f);
            PlaceMovingPlatform(prefabs.MovingPlatform, obstacles, "MovingPlatform_2", new Vector3(0f, 0f, 65f),
                new Vector3(0f, 0f, 5.5f), 1.5f);
            PlaceMovingPlatform(prefabs.MovingPlatform, obstacles, "MovingPlatform_3", new Vector3(3f, 0f, 76f),
                new Vector3(-6f, 0f, 0f), 0.7f);

            // ---- Course logic
            var courseObject = new GameObject("Course");
            var checkpointManager = courseObject.AddComponent<CheckpointManager>();
            var course = courseObject.AddComponent<CourseManager>();

            var checkpoints = new List<Checkpoint>
            {
                PlaceCheckpoint(prefabs.Checkpoint, courseObject.transform, checkpointManager, 0, "Checkpoint_0_Start", -4f),
                PlaceCheckpoint(prefabs.Checkpoint, courseObject.transform, checkpointManager, 1, "Checkpoint_1", 24.6f),
                PlaceCheckpoint(prefabs.Checkpoint, courseObject.transform, checkpointManager, 2, "Checkpoint_2", 51.6f)
            };
            checkpointManager.checkpoints = checkpoints.ToArray();

            GameObject finish = InstantiatePrefab(prefabs.FinishLine, courseObject.transform, new Vector3(0f, 0f, 86f), Quaternion.identity);
            var finishLine = finish.GetComponent<FinishLine>();
            finishLine.course = course;
            RecordOverrides(finishLine);

            // ---- Player and camera
            GameObject playerObject = InstantiatePrefab(prefabs.Player, null, new Vector3(0f, 0.02f, -3f), Quaternion.identity);
            playerObject.name = "Player";
            var player = playerObject.GetComponent<PlayerCharacter>();

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = SkyColor;
            camera.fieldOfView = assets.Camera.fieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -8.5f), Quaternion.Euler(16f, 0f, 0f));

            var gameCamera = cameraObject.AddComponent<ThirdPersonCamera>();
            gameCamera.target = player;
            gameCamera.config = assets.Camera;
            gameCamera.controlledCamera = camera;

            var motor = playerObject.GetComponent<CharacterMotor>();
            motor.viewTransform = cameraObject.transform;
            RecordOverrides(motor);

            var respawner = playerObject.GetComponent<PlayerRespawner>();
            respawner.checkpoints = checkpointManager;
            respawner.killHeight = WaterLevel - 12f;
            RecordOverrides(respawner);
            RecordOverrides(playerObject);

            course.player = player;
            course.checkpoints = checkpointManager;
            course.gameCamera = gameCamera;

            // ---- Systems and UI
            InstantiatePrefab(prefabs.AudioManager, null, Vector3.zero, Quaternion.identity);
            CreateEventSystem();
            BuildCourseUi(course);

            SaveScene(scene, CourseScenePath);
        }

        private static void ConfigureLighting()
        {
            // Bright tropical daylight: one warm sun plus a flat three-colour ambient (no baking needed).
            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.86f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.86f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.82f, 0.80f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.42f, 0.50f);
            RenderSettings.fog = false;
        }

        /// <summary>Box platform whose top face is centred on <paramref name="topCenter"/>, with a support column into the water.</summary>
        private static void Platform(string name, Transform parent, Vector3 topCenter, Vector3 size, Material material, ContentAssets assets)
        {
            Primitive(PrimitiveType.Cube, name, parent, topCenter + Vector3.down * (size.y * 0.5f), size, material,
                GameLayers.Ground, true);
            Support(name + "_Support", parent, topCenter + Vector3.down * size.y, Mathf.Min(size.x, size.z) * 0.3f, assets);
        }

        private static void RoundPlatform(string name, Transform parent, Vector3 topCenter, float radius, Material material, ContentAssets assets)
        {
            const float thickness = 1f;
            // Cylinder primitive: 2 units tall, 1 unit wide. Swap its capsule collider for the real shape.
            GameObject go = Primitive(PrimitiveType.Cylinder, name, parent, topCenter + Vector3.down * (thickness * 0.5f),
                new Vector3(radius * 2f, thickness * 0.5f, radius * 2f), material, GameLayers.Ground, false);
            var meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            meshCollider.convex = true;
            Support(name + "_Support", parent, topCenter + Vector3.down * thickness, radius * 0.35f, assets);
        }

        private static void Plank(string name, Transform parent, Vector3 topCenter, Vector3 size, Material material, bool solid)
        {
            Primitive(PrimitiveType.Cube, name, parent, topCenter + Vector3.down * (size.y * 0.5f), size, material,
                GameLayers.Ground, solid);
        }

        private static void Support(string name, Transform parent, Vector3 top, float radius, ContentAssets assets)
        {
            const float length = 8f;
            Primitive(PrimitiveType.Cylinder, name, parent, top + Vector3.down * (length * 0.5f),
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), assets.Dark, GameLayers.Default, false);
        }

        /// <summary>Invisible walls that keep the character, the ragdoll and the balls inside the course area.</summary>
        private static void BuildBoundaries(Transform parent)
        {
            Transform root = Empty("Boundaries", parent, Vector3.zero, GameLayers.Boundary).transform;
            const float minX = -24f, maxX = 24f, minZ = -16f, maxZ = 102f, height = 40f, thickness = 1f;
            float centerZ = (minZ + maxZ) * 0.5f;
            float lengthZ = maxZ - minZ;
            float lengthX = maxX - minX;

            Wall("Wall_Left", root, new Vector3(minX, 5f, centerZ), new Vector3(thickness, height, lengthZ));
            Wall("Wall_Right", root, new Vector3(maxX, 5f, centerZ), new Vector3(thickness, height, lengthZ));
            Wall("Wall_Back", root, new Vector3(0f, 5f, minZ), new Vector3(lengthX, height, thickness));
            Wall("Wall_Front", root, new Vector3(0f, 5f, maxZ), new Vector3(lengthX, height, thickness));
        }

        private static void Wall(string name, Transform parent, Vector3 center, Vector3 size)
        {
            GameObject wall = Empty(name, parent, center, GameLayers.Boundary);
            var box = wall.AddComponent<BoxCollider>();
            box.size = size;
        }

        private static void BuildBuoys(Transform parent, ContentAssets assets)
        {
            Transform root = Empty("Buoys", parent, Vector3.zero, GameLayers.Default).transform;
            float[] zs = { 2f, 20f, 38f, 56f, 74f, 90f };
            for (int i = 0; i < zs.Length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Material material = (i + (side > 0 ? 1 : 0)) % 2 == 0 ? assets.Orange : assets.White;
                    Primitive(PrimitiveType.Sphere, "Buoy", root, new Vector3(side * 14f, WaterLevel + 0.15f, zs[i]),
                        Vector3.one * 1.2f, material, GameLayers.Default, false);
                }
            }
        }

        private static void PlaceBall(GameObject prefab, Transform parent, string name, float z, float startPhase,
            float travelSpeed, float bounceHeight, Material material)
        {
            GameObject instance = InstantiatePrefab(prefab, parent, new Vector3(0f, 2.4f, z), Quaternion.identity);
            instance.name = name;

            var ball = instance.GetComponent<BouncingBall>();
            ball.patrolOffset = new Vector3(3f, 0f, 0f);
            ball.startPhase = startPhase;
            ball.travelSpeed = travelSpeed;
            ball.bounceHeight = bounceHeight;
            RecordOverrides(ball);

            var renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            RecordOverrides(renderer);
            RecordOverrides(instance);
        }

        private static void PlaceMovingPlatform(GameObject prefab, Transform parent, string name, Vector3 position,
            Vector3 travel, float startTimeOffset)
        {
            GameObject instance = InstantiatePrefab(prefab, parent, position, Quaternion.identity);
            instance.name = name;

            var platform = instance.GetComponent<MovingPlatform>();
            platform.waypoints = new[] { Vector3.zero, travel };
            platform.startTimeOffset = startTimeOffset;
            RecordOverrides(platform);
            RecordOverrides(instance);
        }

        private static Checkpoint PlaceCheckpoint(GameObject prefab, Transform parent, CheckpointManager manager, int index,
            string name, float z)
        {
            GameObject instance = InstantiatePrefab(prefab, parent, new Vector3(0f, 0f, z), Quaternion.identity);
            instance.name = name;

            var checkpoint = instance.GetComponent<Checkpoint>();
            checkpoint.index = index;
            checkpoint.manager = manager;
            RecordOverrides(checkpoint);
            RecordOverrides(instance);
            return checkpoint;
        }

        private static void SaveScene(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException("Unity could not save scene '" + path + "'.");
            }
        }

        private static void RegisterScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in new[] { MainMenuScenePath, CourseScenePath })
            {
                if (File.Exists(path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
