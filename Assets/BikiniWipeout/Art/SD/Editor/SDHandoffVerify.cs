// Read-only handoff verification of the Course 1 visual integration. Never saves anything.
// Batch: -executeMethod SDHandoffVerify.Run  -> writes ArtStaging/Integration/handoff_verification.txt
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SDHandoffVerify
{
    const string PrefabFolder = "Assets/BikiniWipeout/Prefabs/Course1/";
    const string ScenePath = "Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity";
    static int problems;

    static readonly string[] IntegratedPrefabs =
        { "SweeperSmall", "SweeperLarge", "SweeperDouble", "RotatingBeam", "SpinPlatform", "Pendulum", "PistonWall", "BouncingBallArena" };

    // scene object name prefix -> expected number of instances
    static readonly (string prefix, int count)[] SceneExpect =
    {
        ("A_Sweeper_", 2), ("C_Sweeper", 1), ("C_Beam_", 2), ("D_Beam", 1), ("E_DoubleSweeper", 1),
        ("SpinDisc_", 2), ("D_Pendulum_", 5), ("D_Piston_", 3), ("B_Ball_", 3), ("B_GiantBall_", 2),
    };

    static readonly string[] PadNames = { "BouncePad", "FinalPad_1", "FinalPad_2", "FinalPad_3", "FinalPad_4", "LaunchPad_1", "LaunchPad_2" };

    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Splashdown Course 1 handoff verification (read-only)");

        foreach (var name in IntegratedPrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            sb.AppendLine("PREFAB " + name + ": " + (prefab == null ? Fail("missing") : CheckObject(prefab.transform, name == "SpinPlatform")));
            sb.AppendLine("  missing refs: " + MissingRefs(prefab));
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        sb.AppendLine("SCENE " + scene.name + " roots=" + scene.rootCount);
        foreach (var e in SceneExpect)
        {
            var found = new List<Transform>();
            foreach (var r in scene.GetRootGameObjects()) Collect(r.transform, e.prefix, found);
            int ok = 0;
            foreach (var t in found) if (CheckObject(t, e.prefix == "SpinDisc_").StartsWith("OK")) ok++;
            string line = e.prefix + "*: found " + found.Count + " (expected " + e.count + "), valid " + ok;
            if (found.Count != e.count || ok != found.Count) line = Fail(line);
            sb.AppendLine("  " + line);
        }
        int padsOk = 0;
        foreach (var pn in PadNames)
        {
            Transform t = null; foreach (var r in scene.GetRootGameObjects()) { t = SDSweeperIntegration.Find(r.transform, pn); if (t) break; }
            if (t == null) { sb.AppendLine("  " + Fail("pad missing: " + pn)); continue; }
            var head = t.Find("SD_Visual_PadHead");
            var deck = t.Find("Deck");
            bool headOk = head != null && VisualOk(head.gameObject, out string why) && head.GetComponentsInChildren<Collider>(true).Length == 0;
            bool colliderKept = deck != null && deck.GetComponent<Collider>() != null && deck.GetComponent<Collider>().enabled && !deck.GetComponent<MeshRenderer>().enabled;
            bool supportKept = t.Find("Support") != null && t.Find("Support").GetComponent<MeshRenderer>().enabled;
            if (headOk && colliderKept && supportKept) padsOk++; else sb.AppendLine("  " + Fail(pn + ": head=" + headOk + " deckColliderKept=" + colliderKept + " supportKept=" + supportKept));
        }
        sb.AppendLine("  pads valid: " + padsOk + "/7");
        sb.AppendLine("  scene missing refs: " + MissingRefsScene(scene));
        sb.AppendLine("  scene dirty=" + scene.isDirty + " (never saved by this tool)");

        // every integrated asset's material shader
        foreach (var n in new[] { "SD_Red", "SD_Blue", "SD_White", "SD_Chrome" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/BikiniWipeout/Art/SD/Materials/" + n + ".mat");
            string line = n + ": " + (m == null ? "MISSING" : m.shader.name);
            if (m == null || m.shader.name != "Universal Render Pipeline/Lit") line = Fail(line);
            sb.AppendLine("MATERIAL " + line);
        }
        sb.AppendLine(problems == 0 ? "RESULT: ALL CHECKS PASSED" : "RESULT: " + problems + " PROBLEM(S) FOUND");
        Directory.CreateDirectory("ArtStaging/Integration");
        File.WriteAllText("ArtStaging/Integration/handoff_verification.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }

    static string Fail(string s) { problems++; return "FAIL: " + s; }

    static void Collect(Transform t, string prefix, List<Transform> found)
    {
        if (t.name.StartsWith(prefix) && !t.name.StartsWith("SD_Visual_") && HasVisual(t)) { found.Add(t); return; }
        foreach (Transform c in t) Collect(c, prefix, found);
    }

    static bool HasVisual(Transform t) { foreach (Transform c in t.GetComponentsInChildren<Transform>(true)) if (c.name.StartsWith("SD_Visual_")) return true; return false; }

    // OK if: has SD_Visual_* children, each with a mesh from Art/SD/Models and materials from Art/SD/Materials, no colliders on them,
    // and every other MeshRenderer disabled (except the intentionally kept SpinPlatform support).
    static string CheckObject(Transform root, bool allowSupport)
    {
        int visuals = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("SD_Visual_")) continue;
            visuals++;
            if (!VisualOk(t.gameObject, out string why)) return Fail(t.name + ": " + why);
            if (t.GetComponentsInChildren<Collider>(true).Length > 0) return Fail(t.name + " has a collider");
        }
        if (visuals == 0) return Fail("no SD_Visual_* children");
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name.StartsWith("SD_Visual_") || !r.enabled) continue;
            if (allowSupport && r.name == "Support") continue;
            return Fail("old primitive renderer still enabled: " + r.name);
        }
        return "OK (" + visuals + " visual(s))";
    }

    static bool VisualOk(GameObject go, out string why)
    {
        why = "";
        var mf = go.GetComponent<MeshFilter>(); var mr = go.GetComponent<MeshRenderer>();
        if (mf == null || mr == null || !mr.enabled) { why = "no enabled MeshFilter/MeshRenderer"; return false; }
        if (mf.sharedMesh == null) { why = "mesh missing"; return false; }
        string mp = AssetDatabase.GetAssetPath(mf.sharedMesh);
        if (!mp.StartsWith("Assets/BikiniWipeout/Art/SD/Models/")) { why = "mesh from unexpected path " + mp; return false; }
        if (mr.sharedMaterials.Length == 0) { why = "no materials"; return false; }
        foreach (var m in mr.sharedMaterials)
        {
            if (m == null) { why = "null material"; return false; }
            string p = AssetDatabase.GetAssetPath(m);
            if (!p.StartsWith("Assets/BikiniWipeout/Art/SD/Materials/")) { why = "material " + m.name + " from " + p; return false; }
        }
        return true;
    }

    static int Count(SerializedObject so)
    {
        int n = 0; var it = so.GetIterator();
        for (bool enter = true; it.Next(enter); enter = false)
        {
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.objectReferenceEntityIdValue != EntityId.None) n++;
        }
        return n;
    }

    static string MissingRefs(GameObject root)
    {
        int refs = 0, scripts = 0;
        foreach (var c in root.GetComponentsInChildren<Component>(true)) { if (c == null) { scripts++; continue; } refs += Count(new SerializedObject(c)); }
        if (refs + scripts > 0) problems++;
        return refs + " missing object refs, " + scripts + " missing scripts";
    }

    static string MissingRefsScene(UnityEngine.SceneManagement.Scene scene)
    {
        int refs = 0, scripts = 0;
        foreach (var r in scene.GetRootGameObjects())
            foreach (var c in r.GetComponentsInChildren<Component>(true)) { if (c == null) { scripts++; continue; } refs += Count(new SerializedObject(c)); }
        if (refs + scripts > 0) problems++;
        return refs + " missing object refs, " + scripts + " missing scripts";
    }
}
