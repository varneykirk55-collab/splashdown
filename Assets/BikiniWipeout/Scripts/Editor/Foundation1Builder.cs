using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Generates all Foundation 1 content from code: materials, config assets, the Animator controller,
    /// prefabs, UI and the two scenes. The project is authored as scripts so that every generated asset
    /// is reproducible; after generation the assets are ordinary Unity assets and can be edited freely.
    ///
    /// Menu: Bikini Wipeout > Build Foundation 1 Content
    /// Batch: -executeMethod BikiniWipeout.EditorTools.Foundation1Builder.BuildAllBatch
    /// </summary>
    public static partial class Foundation1Builder
    {
        public const string Root = "Assets/BikiniWipeout";
        public const string ScenesFolder = Root + "/Scenes";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string MaterialsFolder = Root + "/Materials";
        public const string AnimationsFolder = Root + "/Animations";
        public const string AudioFolder = Root + "/Audio";
        public const string SettingsFolder = Root + "/Settings";

        public const string MainMenuScenePath = ScenesFolder + "/" + SceneNames.MainMenu + ".unity";
        public const string CourseScenePath = ScenesFolder + "/" + SceneNames.CourseFoundation1 + ".unity";
        public const string InputActionsPath = SettingsFolder + "/Input/BikiniWipeoutControls.inputactions";

        private const string LitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>When false, existing generated prefabs and scenes are kept as they are.</summary>
        private static bool overwriteGenerated;

        [MenuItem("Bikini Wipeout/Build Foundation 1 Content (Missing Only)", priority = 0)]
        public static void BuildMissingMenu()
        {
            if (!ConfirmEditMode() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildAll(false);
        }

        [MenuItem("Bikini Wipeout/Rebuild Foundation 1 Content (Overwrite Generated)", priority = 1)]
        public static void RebuildAllMenu()
        {
            if (!ConfirmEditMode())
            {
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Rebuild Foundation 1 content",
                "This regenerates the Foundation 1 prefabs and both scenes from code and OVERWRITES any manual " +
                "edits made to them.\n\nMaterials, config assets, the audio library and the Animator controller are " +
                "kept if they already exist.",
                "Rebuild", "Cancel");
            if (!proceed || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildAll(true);
        }

        [MenuItem("Bikini Wipeout/Open Course Scene", priority = 20)]
        public static void OpenCourseScene()
        {
            OpenScene(CourseScenePath);
        }

        [MenuItem("Bikini Wipeout/Open Main Menu Scene", priority = 21)]
        public static void OpenMainMenuScene()
        {
            OpenScene(MainMenuScenePath);
        }

        /// <summary>Command-line entry point. Exits with code 0 on success, 1 on failure.</summary>
        public static void BuildAllBatch()
        {
            bool ok = BuildAll(true);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>True when both scenes exist, i.e. the content has been generated at least once.</summary>
        public static bool IsContentBuilt()
        {
            return File.Exists(MainMenuScenePath) && File.Exists(CourseScenePath);
        }

        /// <summary>Builds everything. Returns false (and logs the reason) if any step failed.</summary>
        public static bool BuildAll(bool overwrite)
        {
            overwriteGenerated = overwrite;
            try
            {
                if (Shader.Find(LitShaderName) == null)
                {
                    throw new InvalidOperationException(
                        $"Shader '{LitShaderName}' was not found. The Universal Render Pipeline package must be installed " +
                        "and imported before the content can be generated.");
                }

                Step("Folders", 0.02f, EnsureFolders);
                Step("Project settings", 0.05f, EnsureProjectSettings);

                ContentAssets assets = null;
                Step("Materials, configs, audio library, animator", 0.15f, () => assets = BuildSharedAssets());

                PrefabSet prefabs = null;
                Step("Prefabs", 0.45f, () => prefabs = BuildPrefabs(assets));
                Step("Main menu scene", 0.7f, () => BuildMainMenuScene(assets, prefabs));
                Step("Course scene", 0.85f, () => BuildCourseScene(assets, prefabs));
                Step("Build settings", 0.95f, RegisterScenes);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!Application.isBatchMode && File.Exists(CourseScenePath))
                {
                    EditorSceneManager.OpenScene(CourseScenePath, OpenSceneMode.Single);
                }

                Debug.Log("[Bikini Wipeout] Foundation 1 content is ready. Open '" + CourseScenePath + "' and press Play.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Bikini Wipeout] Building Foundation 1 content failed: " + exception);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Step(string label, float progress, Action action)
        {
            EditorUtility.DisplayProgressBar("Bikini Wipeout - Foundation 1", label, progress);
            try
            {
                action();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Step '" + label + "' failed: " + exception.Message, exception);
            }
        }

        private static bool ConfirmEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Bikini Wipeout", "Exit Play mode before building content.", "OK");
                return false;
            }

            return true;
        }

        private static void OpenScene(string path)
        {
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Bikini Wipeout",
                    "The scene has not been generated yet.\n\nRun: Bikini Wipeout > Build Foundation 1 Content", "OK");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }

        // ------------------------------------------------------------------ shared helpers

        private static void EnsureFolders()
        {
            string[] folders =
            {
                Root, ScenesFolder, PrefabsFolder, PrefabsFolder + "/Characters", PrefabsFolder + "/Obstacles",
                PrefabsFolder + "/Environment", PrefabsFolder + "/Systems", MaterialsFolder, AnimationsFolder,
                AnimationsFolder + "/Placeholder", AudioFolder, SettingsFolder
            };

            foreach (string folder in folders)
            {
                EnsureFolder(folder);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Verifies the layer names that the hand-written ProjectSettings ship with, and re-applies the collision matrix.</summary>
        private static void EnsureProjectSettings()
        {
            foreach (int layer in GameLayers.GameplayLayers)
            {
                string expected = GameLayers.NameOf(layer);
                string actual = LayerMask.LayerToName(layer);
                if (actual != expected)
                {
                    SetLayerName(layer, expected);
                }
            }

            // Persists the rules into ProjectSettings/DynamicsManager.asset as well.
            GameLayers.ApplyCollisionMatrix();
        }

        private static void SetLayerName(int layer, string name)
        {
            Object[] tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager == null || tagManager.Length == 0)
            {
                throw new InvalidOperationException("Could not open ProjectSettings/TagManager.asset to name layer " + layer + ".");
            }

            var serialized = new SerializedObject(tagManager[0]);
            SerializedProperty layers = serialized.FindProperty("layers");
            if (layers == null || !layers.isArray || layers.arraySize <= layer)
            {
                throw new InvalidOperationException("TagManager has no 'layers' array; cannot name layer " + layer + ".");
            }

            layers.GetArrayElementAtIndex(layer).stringValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Bikini Wipeout] Named physics layer {layer} '{name}'.");
        }

        private static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            T created = create();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>Saves a temporary scene object as a prefab asset and removes the temporary object.</summary>
        private static GameObject SavePrefab(GameObject temporary, string path)
        {
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path, out bool success);
                if (!success || prefab == null)
                {
                    throw new InvalidOperationException("Unity could not save prefab '" + path + "'.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
        }

        /// <summary>Returns the existing prefab unless it is missing or an overwrite was requested.</summary>
        private static GameObject GetOrBuildPrefab(string path, Func<GameObject> build)
        {
            if (!overwriteGenerated)
            {
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null)
                {
                    return existing;
                }
            }

            return SavePrefab(build(), path);
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate prefab '" + (prefab != null ? prefab.name : "null") + "'.");
            }

            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            RecordOverrides(instance.transform);
            return instance;
        }

        /// <summary>
        /// Must be called after changing a component on a prefab instance from code, otherwise the change
        /// is not stored as an override and is lost when the scene is saved.
        /// </summary>
        private static void RecordOverrides(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }

            EditorUtility.SetDirty(target);
        }

        /// <summary>Creates a primitive with a material. Removes the default collider unless asked to keep it.</summary>
        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, int layer, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return go;
        }

        private static GameObject Empty(string name, Transform parent, Vector3 localPosition, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }
    }
}
