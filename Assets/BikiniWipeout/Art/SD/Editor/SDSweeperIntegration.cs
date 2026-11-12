// Integrates the refined SD_Sweeper_Small / SD_Sweeper_Large meshes into the existing Course 1 prefabs
// SweeperSmall / SweeperLarge as VISUAL-ONLY children. Nothing else is modified:
//  - all colliders, rigidbodies, scripts, layers, pivots and transforms stay as they are;
//  - the primitive MeshRenderers (hub, cap, tubes, pads) are only DISABLED (colliders stay);
//  - the decorative base skirt and arm visuals carry no collider.
// Batch entry points (use -executeMethod): SDSweeperIntegration.DumpBefore / Apply / DumpAfter / ShootBefore / ShootAfter.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SDSweeperIntegration
{
    const string PrefabFolder = "Assets/BikiniWipeout/Prefabs/Course1/";
    const string ModelFolder = "Assets/BikiniWipeout/Art/SD/Models/";
    const string OutDir = "ArtStaging/Integration/";
    const string VisualPrefix = "SD_Visual_";

    static readonly (string prefab, string fbx)[] Pairs =
    {
        ("SweeperSmall", "SD_Sweeper_Small"),
        ("SweeperLarge", "SD_Sweeper_Large"),
    };

    public static void RunBefore() { DumpBefore(); ShootBefore(); }
    public static void RunAfter() { Apply(); DumpAfter(); ShootAfter(); }

    // ------------------------------------------------------------------ apply

    public static void Apply()
    {
        var log = new StringBuilder();
        foreach (var pair in Pairs)
        {
            string path = PrefabFolder + pair.prefab + ".prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + pair.fbx + ".fbx");
            if (fbx == null) throw new System.Exception("Missing " + pair.fbx);
            Transform fbxBase = fbx.transform.Find("Base"), fbxArm = fbx.transform.Find("Arm");
            if (fbxBase == null || fbxArm == null) throw new System.Exception(pair.fbx + " needs Base and Arm");

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform arm = root.transform.Find("Arm");
                if (arm == null) throw new System.Exception(pair.prefab + " has no Arm");

                // idempotent: drop visuals from an earlier run
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name.StartsWith(VisualPrefix)) Object.DestroyImmediate(t.gameObject);

                int disabled = 0;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.enabled) { r.enabled = false; disabled++; }
                }

                AddVisual("SD_Visual_Base", root.transform, fbxBase);
                AddVisual("SD_Visual_Arm", arm, fbxArm);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine(pair.prefab + ": primitive renderers disabled=" + disabled + ", visuals added from " + pair.fbx);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(OutDir);
        File.WriteAllText(OutDir + "apply_log.txt", log.ToString());
        Debug.Log(log.ToString());
    }

    static void AddVisual(string name, Transform parent, Transform source)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);          // local pose = identity: FBX meshes are authored around the pivot
        go.layer = 0;                                    // Default: purely decorative, no physics layer
        go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        r.receiveShadows = true;
        // intentionally no collider
    }

    // ------------------------------------------------------------------ gameplay dump (before/after must be identical)

    public static void DumpBefore() { Dump("before"); }
    public static void DumpAfter() { Dump("after"); }

    static void Dump(string tag)
    {
        Directory.CreateDirectory(OutDir);
        var sb = new StringBuilder();
        foreach (var pair in Pairs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + pair.prefab + ".prefab");
            sb.AppendLine("#### " + pair.prefab);
            DumpTransform(prefab.transform, "", sb);
        }
        File.WriteAllText(OutDir + "gameplay_" + tag + ".txt", sb.ToString());
        // visual-only report
        var vis = new StringBuilder();
        foreach (var pair in Pairs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + pair.prefab + ".prefab");
            vis.AppendLine("#### " + pair.prefab);
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                vis.AppendLine(Path(r.transform, prefab.transform) + " enabled=" + r.enabled + " mats=" + string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m ? m.name : "null")));
        }
        File.WriteAllText(OutDir + "visuals_" + tag + ".txt", vis.ToString());
    }

    static string Path(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
        return root.name + (parts.Count > 0 ? "/" + string.Join("/", parts) : "");
    }

    static void DumpTransform(Transform t, string indent, StringBuilder sb)
    {
        if (t.name.StartsWith(VisualPrefix)) return;   // new decorative objects are reported separately
        sb.AppendLine(indent + t.name + " layer=" + t.gameObject.layer + " active=" + t.gameObject.activeSelf
            + " pos=" + t.localPosition.ToString("F4") + " rot=" + t.localRotation.ToString("F4") + " scale=" + t.localScale.ToString("F4"));
        foreach (var c in t.GetComponents<Component>())
        {
            if (c == null || c is Transform || c is MeshRenderer || c is MeshFilter) continue; // visuals are compared separately
            sb.AppendLine(indent + "  [" + c.GetType().Name + "]");
            var so = new SerializedObject(c);
            var it = so.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.propertyPath == "m_Script" || it.propertyPath == "m_ObjectHideFlags") continue;
                sb.AppendLine(indent + "    " + it.propertyPath + " = " + Value(it));
            }
        }
        foreach (Transform child in t) DumpTransform(child, indent + "  ", sb);
    }

    static string Value(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer: return p.longValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.Float: return p.floatValue.ToString("F4");
            case SerializedPropertyType.String: return p.stringValue;
            case SerializedPropertyType.Enum: return p.enumValueIndex.ToString();
            case SerializedPropertyType.Vector3: return p.vector3Value.ToString("F4");
            case SerializedPropertyType.Vector2: return p.vector2Value.ToString("F4");
            case SerializedPropertyType.Quaternion: return p.quaternionValue.ToString("F4");
            case SerializedPropertyType.Color: return p.colorValue.ToString();
            case SerializedPropertyType.LayerMask: return p.intValue.ToString();
            case SerializedPropertyType.Bounds: return p.boundsValue.ToString();
            case SerializedPropertyType.ObjectReference:
                return p.objectReferenceValue ? p.objectReferenceValue.GetType().Name + ":" + p.objectReferenceValue.name : "null";
            default: return "(" + p.propertyType + ")";
        }
    }

    // ------------------------------------------------------------------ screenshots (needs a GPU: do not pass -nographics)

    public static void ShootBefore() { Shoot("before"); }
    public static void ShootAfter() { Shoot("after"); }

    static void Shoot(string tag)
    {
        Directory.CreateDirectory(OutDir);
        var scene = EditorSceneManager.OpenScene("Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity", OpenSceneMode.Single);
        var log = new StringBuilder();
        // name, camera offset from the sweeper, look-at height
        var views = new[]
        {
            ("A_Sweeper_1", new Vector3(-6.5f, 5.0f, 8.5f), 0.9f),
            ("C_Sweeper", new Vector3(13f, 9f, -15f), 1.0f),
        };
        foreach (var v in views)
        {
            Transform target = null;
            foreach (var go in scene.GetRootGameObjects()) { target = Find(go.transform, v.Item1); if (target) break; }
            if (!target) { log.AppendLine(v.Item1 + " not found"); continue; }

            var camGo = new GameObject("SD_ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = target.position + v.Item2;
            camGo.transform.LookAt(target.position + Vector3.up * v.Item3);

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(OutDir + tag + "_" + v.Item1 + ".png", tex.EncodeToPNG());
            log.AppendLine(v.Item1 + " rendered from " + camGo.transform.position + " avg=" + Average(tex).ToString("F3"));
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
        }
        log.AppendLine("scene dirty=" + scene.isDirty + " (never saved)");
        File.WriteAllText(OutDir + "shots_" + tag + ".txt", log.ToString());
    }

    static float Average(Texture2D t)
    {
        var px = t.GetPixels32(); double s = 0;
        for (int i = 0; i < px.Length; i += 37) s += (px[i].r + px[i].g + px[i].b) / 765.0;
        return (float)(s / (px.Length / 37 + 1));
    }

    static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = Find(c, name); if (r) return r; }
        return null;
    }
}
