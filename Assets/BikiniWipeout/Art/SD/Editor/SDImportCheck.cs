// Read-only check of the staged SD_* assets against the existing Course 1 prefabs and scene.
// Menu: Bikini Wipeout/Art/Check SD Assets. Batch: -executeMethod SDImportCheck.Run (writes SD_import_check.txt). Never saves scenes.
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SDImportCheck
{
    const string Models = "Assets/BikiniWipeout/Art/SD/Models/";
    const string Mats = "Assets/BikiniWipeout/Art/SD/Materials/";

    [MenuItem("Bikini Wipeout/Art/Check SD Assets")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("colorSpace=" + PlayerSettings.colorSpace + " pipeline=" + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "none"));
        // 1 materials
        foreach (var n in new[] { "SD_Red", "SD_Blue", "SD_White", "SD_Chrome" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + n + ".mat");
            sb.AppendLine(n + ": " + (m ? m.shader.name + " base=" + m.GetColor("_BaseColor") : "MISSING"));
        }
        // 2 remap and inspect each FBX
        foreach (var n in new[] { "SD_Sweeper_Small", "SD_Sweeper_Large", "SD_JumpPad_Small", "SD_BallMount" })
        {
            string p = Models + n + ".fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(p);
            bool changed = false;
            foreach (var mn in new[] { "SD_Red", "SD_Blue", "SD_White", "SD_Chrome" })
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), mn);
                var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + mn + ".mat");
                var map = imp.GetExternalObjectMap();
                if (!map.ContainsKey(id) || map[id] != m) { imp.AddRemap(id, m); changed = true; }
            }
            if (changed) imp.SaveAndReimport();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            sb.AppendLine("== " + n + " loaded=" + (go != null));
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                sb.AppendLine("  " + r.name + " localPos=" + r.transform.localPosition + " bounds=" + r.bounds.size + " mats=" + string.Join(",", System.Array.ConvertAll(r.sharedMaterials, x => x ? x.name + "@" + AssetDatabase.GetAssetPath(x).Replace("Assets/BikiniWipeout/Art/SD/", "") : "null")));
        }
        // 3 compare with existing prefabs
        foreach (var pair in new[] { ("SweeperSmall", "SD_Sweeper_Small"), ("SweeperLarge", "SD_Sweeper_Large") })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BikiniWipeout/Prefabs/Course1/" + pair.Item1 + ".prefab");
            if (prefab == null) { sb.AppendLine(pair.Item1 + ": prefab not found"); continue; }
            var rs = prefab.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var arm = prefab.transform.Find("Arm");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Models + pair.Item2 + ".fbx");
            var fr = fbx.GetComponentsInChildren<Renderer>();
            Bounds fb = fr[0].bounds; foreach (var r in fr) fb.Encapsulate(r.bounds);
            var farm = fbx.transform.Find("Arm");
            sb.AppendLine(pair.Item1 + " prefab size=" + b.size + " armLocalPos=" + (arm ? arm.localPosition.ToString() : "n/a") + " | FBX size=" + fb.size + " armLocalPos=" + (farm ? farm.localPosition.ToString() : "n/a"));
        }
        // 4 place into the Course 1 scene IN MEMORY (never saved) and compare against an existing sweeper
        var scene = EditorSceneManager.OpenScene("Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity", OpenSceneMode.Single);
        GameObject first = null;
        foreach (var go in scene.GetRootGameObjects()) { var t = FindByNamePrefix(go.transform, "A_Sweeper_1"); if (t) { first = t.gameObject; break; } }
        sb.AppendLine("scene=" + scene.name + " roots=" + scene.rootCount + " firstSweeperSmall=" + (first ? first.name + " at " + first.transform.position : "none"));
        if (first != null)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "SD_Sweeper_Small.fbx");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            inst.transform.SetPositionAndRotation(first.transform.position, first.transform.rotation);
            var a = inst.transform.Find("Arm");
            Physics.SyncTransforms();
            int onGround = 0; const int N = 16;
            for (int i = 0; i < N; i++) { float ang = i * Mathf.PI * 2f / N; var o = first.transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.1f + Vector3.up * 3f; if (Physics.Raycast(o, Vector3.down, out var h, 6f) && Mathf.Abs(h.point.y - first.transform.position.y) < 0.15f) onGround++; }
            sb.AppendLine("SD base footprint (r=1.1) supported by ground at " + onGround + "/" + N + " rim points around the existing A_Sweeper_1 axis (axis y=" + first.transform.position.y + ")");
            sb.AppendLine("SD instance placed at same transform; Arm world pos=" + a.position + " vs existing Arm world pos=" + first.transform.Find("Arm").position);
            Object.DestroyImmediate(inst);
        }
        foreach (var pr in new[] { ("A_Sweeper_1", "SD_Sweeper_Small"), ("A_Sweeper_2", "SD_Sweeper_Small"), ("C_Sweeper", "SD_Sweeper_Large") })
        {
            Transform st = null;
            foreach (var go in scene.GetRootGameObjects()) { st = FindByNamePrefix(go.transform, pr.Item1); if (st) break; }
            if (!st) { sb.AppendLine(pr.Item1 + ": not found"); continue; }
            var bf = AssetDatabase.LoadAssetAtPath<GameObject>(Models + pr.Item2 + ".fbx");
            float rad = bf.transform.Find("Base").GetComponent<MeshRenderer>().bounds.size.x * 0.5f; foreach (var rr in new[] { rad, rad + 0.35f }) {
            Physics.SyncTransforms();
            int ok = 0; const int M = 32;
            for (int i = 0; i < M; i++) { float ang = i * Mathf.PI * 2f / M; var o = st.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rr + Vector3.up * 3f; bool g = false; foreach (var h in Physics.RaycastAll(o, Vector3.down, 6f)) { if (h.collider.transform.IsChildOf(st)) continue; if (Mathf.Abs(h.point.y - st.position.y) < 0.15f) { g = true; break; } } if (g) ok++; }
            sb.AppendLine("BASE FOOTPRINT " + pr.Item1 + " with " + pr.Item2 + " r=" + rr.ToString("0.00") + ": " + ok + "/" + M + " rim points supported");
            }
        }
        sb.AppendLine("scene dirty=" + scene.isDirty + " (not saved)");
        System.IO.File.WriteAllText("SD_import_check.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }

    static Transform FindByNamePrefix(Transform t, string prefix)
    {
        if (t.name.StartsWith(prefix)) return t;
        foreach (Transform c in t) { var r = FindByNamePrefix(c, prefix); if (r) return r; }
        return null;
    }
}
