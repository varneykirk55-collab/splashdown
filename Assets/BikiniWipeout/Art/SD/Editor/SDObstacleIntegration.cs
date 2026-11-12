// Visual-only integration of the refined SD_* meshes into: SpinPlatform, Pendulum, PistonWall, BouncingBallArena prefabs,
// and the seven jump-pad objects in Course_SplashdownGauntlet (BouncePad, FinalPad_1..4, LaunchPad_1..2).
// Rules: no collider, rigidbody, script, layer, pivot or transform of existing objects changes; renderers are only DISABLED;
// the existing pad support columns and foam rings are left untouched.
// Entry points (batch): SDObstacleIntegration.RunBefore / RunAfter.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SDObstacleIntegration
{
    const string PrefabFolder = "Assets/BikiniWipeout/Prefabs/Course1/";
    const string ModelFolder = "Assets/BikiniWipeout/Art/SD/Models/";
    const string ScenePath = "Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity";
    const string OutDir = "ArtStaging/Integration/";
    const string VisualPrefix = "SD_Visual_";

    struct VisualSpec { public string name, parentPath, fbx, node; public VisualSpec(string n, string p, string f, string nd) { name = n; parentPath = p; fbx = f; node = nd; } }
    struct PrefabSpec { public string prefab; public string[] keepEnabled; public VisualSpec[] visuals; }

    static readonly PrefabSpec[] Prefabs =
    {
        new PrefabSpec { prefab = "SpinPlatform", keepEnabled = new[] { "Support" },
            visuals = new[] { new VisualSpec("SD_Visual_Disc", "Disc", "SD_SpinDisc", "Disc") } },
        new PrefabSpec { prefab = "Pendulum", keepEnabled = new string[0],
            visuals = new[] { new VisualSpec("SD_Visual_Gantry", "", "SD_Pendulum", "Gantry"), new VisualSpec("SD_Visual_Arm", "Arm", "SD_Pendulum", "Arm") } },
        new PrefabSpec { prefab = "PistonWall", keepEnabled = new string[0],
            visuals = new[] { new VisualSpec("SD_Visual_Wall", "", "SD_PistonWall", "PistonWall") } },
        new PrefabSpec { prefab = "BouncingBallArena", keepEnabled = new string[0],
            visuals = new[] { new VisualSpec("SD_Visual_Ball", "", "SD_Ball", "Ball") } },
    };

    static readonly (string obj, string fbx, string node)[] Pads =
    {
        ("BouncePad", "SD_PadHead_R180", "PadHead_R180"),
        ("FinalPad_1", "SD_PadHead_R150", "PadHead_R150"), ("FinalPad_2", "SD_PadHead_R150", "PadHead_R150"),
        ("FinalPad_3", "SD_PadHead_R150", "PadHead_R150"), ("FinalPad_4", "SD_PadHead_R150", "PadHead_R150"),
        ("LaunchPad_1", "SD_PadHead_4x3", "PadHead_4x3"), ("LaunchPad_2", "SD_PadHead_4x3", "PadHead_4x3"),
    };

    public static void RunBefore() { Directory.CreateDirectory(OutDir); DumpPrefabs("ob_before"); DumpScene("scene_before"); Shoot("before_ob"); }
    public static void RunAfter() { Directory.CreateDirectory(OutDir); Apply(); DumpPrefabs("ob_after"); DumpScene("scene_after"); Shoot("after_ob"); }

    // ------------------------------------------------------------------ apply

    static Transform Node(GameObject fbx, string node)
    {
        var t = fbx.transform.Find(node);
        if (t != null) return t;
        if (fbx.GetComponent<MeshFilter>() != null) return fbx.transform; // single-object FBX: the mesh lives on the root
        throw new System.Exception("FBX " + fbx.name + " has no node " + node);
    }

    static void Apply()
    {
        var log = new StringBuilder();
        foreach (var spec in Prefabs)
        {
            string path = PrefabFolder + spec.prefab + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t != null && t.name.StartsWith(VisualPrefix)) Object.DestroyImmediate(t.gameObject);
                int disabled = 0;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (System.Array.IndexOf(spec.keepEnabled, r.name) >= 0) continue;
                    if (r.enabled) { r.enabled = false; disabled++; }
                }
                foreach (var v in spec.visuals)
                {
                    SDSweeperIntegration.EnsureMaterialRemap(ModelFolder + v.fbx + ".fbx");
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + v.fbx + ".fbx");
                    Transform parent = v.parentPath == "" ? root.transform : root.transform.Find(v.parentPath);
                    if (parent == null) throw new System.Exception(spec.prefab + " has no " + v.parentPath);
                    SDSweeperIntegration.AddVisual(v.name, parent, Node(fbx, v.node));
                    CompensateParentScale(parent.Find(v.name));
                }
                log.AppendLine(spec.prefab + ": renderers disabled=" + disabled + ", visuals=" + spec.visuals.Length + " | " + SizeCheck(root, spec));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var pad in Pads)
        {
            Transform t = FindInScene(scene, pad.obj);
            if (t == null) { log.AppendLine("PAD NOT FOUND " + pad.obj); continue; }
            foreach (Transform c in new List<Transform>(GetChildren(t))) if (c.name.StartsWith(VisualPrefix)) Object.DestroyImmediate(c.gameObject);
            int disabled = 0;
            foreach (Transform c in t)
            {
                if (c.name != "Deck" && c.name != "Trim") continue; // Support and Foam stay as they are
                var r = c.GetComponent<MeshRenderer>();
                if (r != null && r.enabled) { r.enabled = false; disabled++; }
            }
            SDSweeperIntegration.EnsureMaterialRemap(ModelFolder + pad.fbx + ".fbx");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + pad.fbx + ".fbx");
            SDSweeperIntegration.AddVisual("SD_Visual_PadHead", t, Node(fbx, pad.node));
            CompensateParentScale(t.Find("SD_Visual_PadHead"));
            log.AppendLine(pad.obj + ": renderers disabled=" + disabled + ", head=" + pad.fbx);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText(OutDir + "apply_log_obstacles.txt", log.ToString());
        Debug.Log(log.ToString());
    }

    /// <summary>Some primitives are scaled up on the prefab ROOT (e.g. the ball, scale 2.2); keep the visual at its authored world size.</summary>
    static void CompensateParentScale(Transform visual)
    {
        Vector3 s = visual.parent.lossyScale;
        visual.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
    }

    /// <summary>World-space size/centre of the original (disabled) primitives vs the new SD visuals, so scale mistakes show up numerically.</summary>
    static string SizeCheck(GameObject root, PrefabSpec spec)
    {
        Bounds? oldB = null, newB = null;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool isNew = r.name.StartsWith(VisualPrefix);
            if (!isNew && (r.enabled || System.Array.IndexOf(spec.keepEnabled, r.name) >= 0)) continue;
            if (isNew) { if (newB == null) newB = r.bounds; else { var b = newB.Value; b.Encapsulate(r.bounds); newB = b; } }
            else { if (oldB == null) oldB = r.bounds; else { var b = oldB.Value; b.Encapsulate(r.bounds); oldB = b; } }
        }
        if (oldB == null || newB == null) return "size check: n/a";
        Vector3 d = newB.Value.size - oldB.Value.size;
        return "size old=" + oldB.Value.size.ToString("F2") + " new=" + newB.Value.size.ToString("F2") + " centreDelta=" + (newB.Value.center - oldB.Value.center).ToString("F2");
    }

    static IEnumerable<Transform> GetChildren(Transform t) { foreach (Transform c in t) yield return c; }

    static Transform FindInScene(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects()) { var r = SDSweeperIntegration.Find(go.transform, name); if (r) return r; }
        return null;
    }

    // ------------------------------------------------------------------ dumps (gameplay state must not change)

    static void DumpPrefabs(string tag)
    {
        var sb = new StringBuilder(); var vis = new StringBuilder();
        foreach (var spec in Prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + spec.prefab + ".prefab");
            sb.AppendLine("#### " + spec.prefab);
            SDSweeperIntegration.DumpTransform(prefab.transform, "", sb);
            vis.AppendLine("#### " + spec.prefab);
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                vis.AppendLine(SDSweeperIntegration.Path(r.transform, prefab.transform) + " enabled=" + r.enabled + " mats=" + string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m ? m.name + "@" + AssetDatabase.GetAssetPath(m).Replace("Assets/BikiniWipeout/", "") : "null")));
        }
        File.WriteAllText(OutDir + "gameplay_" + tag + ".txt", sb.ToString());
        File.WriteAllText(OutDir + "visuals_" + tag + ".txt", vis.ToString());
    }

    static void DumpScene(string tag)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var sb = new StringBuilder();
        foreach (var go in scene.GetRootGameObjects()) { sb.AppendLine("#### ROOT " + go.name); SDSweeperIntegration.DumpTransform(go.transform, "", sb); }
        File.WriteAllText(OutDir + "gameplay_" + tag + ".txt", sb.ToString());
    }

    // ------------------------------------------------------------------ screenshots (warm-up renders first; needs a GPU)

    static readonly (string obj, Vector3 offset, float look, string label)[] Views =
    {
        ("LaunchPad_1", new Vector3(6.5f, 5f, -8f), 0f, "LaunchPad_1_sunside"),
        ("BouncePad", new Vector3(5f, 4.5f, -7f), 0f, "BouncePad_sunside"),
        ("FinalPad_2", new Vector3(-5f, 4.5f, 7f), 0f, "FinalPad_2_backlit"),
        ("SpinDisc_1", new Vector3(11f, 9f, -13f), 0f, "SpinDisc_1_sunside"),
        ("D_Pendulum_1", new Vector3(9f, 3f, -16f), -3f, "D_Pendulum_1_sunside"),
        ("D_Piston_1", new Vector3(6.5f, 4.5f, -9f), 0.9f, "D_Piston_1_sunside"),
        ("B_Ball_2", new Vector3(5f, 3.5f, -7f), 1.2f, "B_Ball_2_sunside"),
        ("B_GiantBall_1", new Vector3(6f, 4.5f, -9f), 1.5f, "B_GiantBall_1_sunside"),
    };

    static void Shoot(string tag)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new StringBuilder();
        foreach (var v in Views)
        {
            Transform target = FindInScene(scene, v.obj);
            if (target == null) { log.AppendLine(v.obj + " not found"); continue; }
            var camGo = new GameObject("SD_ShotCam"); var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f; cam.farClipPlane = 400f; cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = target.position + v.offset;
            camGo.transform.LookAt(target.position + Vector3.up * v.look);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            for (int w = 0; w < 4; w++) cam.Render(); // warm-up: the first frames of a batch session are not valid
            cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(OutDir + tag + "_" + v.label + ".png", tex.EncodeToPNG());
            log.AppendLine(v.label + " target=" + target.position);
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
        }
        log.AppendLine("scene dirty=" + scene.isDirty + " (never saved by Shoot)");
        File.WriteAllText(OutDir + "shots_" + tag + ".txt", log.ToString());
    }
}
