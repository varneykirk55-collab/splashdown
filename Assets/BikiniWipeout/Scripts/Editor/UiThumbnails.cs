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
    /// Renders menu portraits and course thumbnails from existing project assets (no new art is authored).
    /// Needs a graphics device: run without -nographics. Re-runnable; overwrites only the PNGs it owns.
    /// </summary>
    public static class UiThumbnails
    {
        public const string Folder = "Assets/BikiniWipeout/UI/Generated";
        private const string Scenes = "Assets/BikiniWipeout/Scenes/";

        [MenuItem("Bikini Wipeout/UI/Render Menu Thumbnails")]
        public static void RenderAll()
        {
            Directory.CreateDirectory(Folder);
            RenderPortrait("Player_Cowgirl", "Portrait_Cowgirl", "Assets/BikiniWipeout/Animations/Cowgirl/Cowgirl.controller");
            RenderPortrait("Player_StarsStripes", "Portrait_StarsStripes", "Assets/BikiniWipeout/Animations/StarsStripes/StarsStripes.controller");
            RenderCourse("Course_SplashdownGauntlet", "Thumb_Gauntlet");
            RenderCourse("Course_ChaosCove", "Thumb_ChaosCove");
            AssetDatabase.Refresh();
            foreach (string png in Directory.GetFiles(Folder, "*.png"))
            {
                string path = png.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        public static void RenderAllBatch() { RenderAll(); }

        private static Texture2D Capture(Camera camera, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
            return tex;
        }

        private static void Save(Texture2D tex, string name)
        {
            File.WriteAllBytes(Folder + "/" + name + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[UiThumbnails] wrote " + name);
        }

        private static void RenderPortrait(string prefabName, string outName, string controllerPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BikiniWipeout/Prefabs/Characters/" + prefabName + ".prefab");
            if (prefab == null) { Debug.LogWarning("[UiThumbnails] missing " + prefabName); return; }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Pose with the project's own idle clip. Skinned meshes are baked so batch/GPU skinning cannot go stale.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            AnimationClip idle = controller == null ? null : controller.layers[0].stateMachine.states
                .Select(s => s.state).Where(s => s.name == "Idle").Select(s => s.motion as AnimationClip).FirstOrDefault();
            var animator = instance.GetComponentInChildren<Animator>();
            if (idle != null && animator != null) idle.SampleAnimation(animator.gameObject, idle.length * 0.35f);

            foreach (var skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                skinned.BakeMesh(baked);
                var go = new GameObject(skinned.name + "_baked");
                go.transform.SetParent(instance.transform, false); go.transform.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = baked;
                go.AddComponent<MeshRenderer>().sharedMaterials = skinned.sharedMaterials;
                skinned.enabled = false;
            }

            Soften(instance.GetComponentsInChildren<Renderer>());
            Bounds bounds = default; bool first = true;
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
            {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }

            Debug.Log("[UiThumbnails] " + outName + " bounds center " + bounds.center + " size " + bounds.size + " renderers " + instance.GetComponentsInChildren<MeshRenderer>().Length);
            var camObject = new GameObject("Cam");
            var camera = camObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.14f, 0.3f, 0.66f);
            camera.fieldOfView = 24f;
            Vector3 focus = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.58f, bounds.center.z);
            camera.transform.position = focus + instance.transform.forward * 3.9f + Vector3.up * 0.05f;
            camera.transform.LookAt(focus);
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 30f;

            AddLight(new Vector3(40, -30, 0), 1.2f, Color.white);
            AddLight(new Vector3(30, 150, 0), 0.8f, new Color(0.5f, 0.7f, 1f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.65f);

            Save(Capture(camera, 512, 768), outName);
        }

        /// <summary>Thumbnail-only: fully metallic materials with no metallic map mirror a black void in a bare render; clone them with a matte finish. Project assets are not touched.</summary>
        private static void Soften(Renderer[] renderers)
        {
            foreach (Renderer renderer in renderers)
            {
                Material[] mats = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !m.HasProperty("_Metallic") || m.GetFloat("_Metallic") < 0.5f) continue;
                    var clone = new Material(m);
                    clone.SetFloat("_Metallic", 0f);
                    if (clone.HasProperty("_Smoothness")) clone.SetFloat("_Smoothness", 0.35f);
                    clone.DisableKeyword("_METALLICSPECGLOSSMAP");
                    mats[i] = clone; changed = true;
                }
                if (changed) renderer.sharedMaterials = mats;
            }
        }

        private static void AddLight(Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject("Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = intensity; light.color = color;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        private static void RenderCourse(string sceneName, string outName)
        {
            string path = Scenes + sceneName + ".unity";
            if (!File.Exists(path)) { Debug.LogWarning("[UiThumbnails] missing scene " + path); return; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            Vector3 start = Vector3.zero, finish = Vector3.forward * 60f;
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player != null) start = player.transform.position;
            var fin = Object.FindFirstObjectByType<FinishLine>();
            if (fin != null) finish = fin.transform.position;
            Vector3 dir = finish - start; dir.y = 0f;
            float length = Mathf.Max(10f, dir.magnitude); dir = dir.sqrMagnitude < 0.01f ? Vector3.forward : dir.normalized;

            // Disable the gameplay camera and render from a hero vantage over the first third of the course.
            foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                existing.enabled = false;
            Soften(Object.FindObjectsByType<Renderer>());
            var camObject = new GameObject("ThumbCam");
            var camera = camObject.AddComponent<Camera>();
            camera.fieldOfView = 55f; camera.nearClipPlane = 0.3f; camera.farClipPlane = 800f;
            camera.clearFlags = CameraClearFlags.Skybox;
            Vector3 target = start + dir * Mathf.Min(length * 0.6f, 70f) + Vector3.up * 3f;
            camera.transform.position = start - dir * 4f + Vector3.up * 7f + Vector3.Cross(Vector3.up, dir) * -5f; camera.fieldOfView = 48f;
            camera.transform.LookAt(target);
            if (Object.FindObjectsByType<Light>().All(l => l.type != LightType.Directional))
            {
                AddLight(new Vector3(50, -30, 0), 1.1f, Color.white);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);

            Save(Capture(camera, 768, 432), outName);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
