// Read-only diagnostic: lighting / ambient / volumes / reflection probes of the Course 1 scene, relative to two sweepers.
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SDLightingProbe
{
    public static void Run()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity", OpenSceneMode.Single);
        sb.AppendLine("ambientMode=" + RenderSettings.ambientMode + " sky=" + RenderSettings.ambientSkyColor + " equator=" + RenderSettings.ambientEquatorColor + " ground=" + RenderSettings.ambientGroundColor + " intensity=" + RenderSettings.ambientIntensity);
        sb.AppendLine("skybox=" + (RenderSettings.skybox ? RenderSettings.skybox.name + "/" + RenderSettings.skybox.shader.name : "none") + " defaultReflectionMode=" + RenderSettings.defaultReflectionMode + " customReflection=" + (RenderSettings.customReflectionTexture ? RenderSettings.customReflectionTexture.name : "none") + " reflIntensity=" + RenderSettings.reflectionIntensity);
        sb.AppendLine("fog=" + RenderSettings.fog + " " + RenderSettings.fogColor);
        Vector3 a = Pos(scene, "A_Sweeper_1"), c = Pos(scene, "C_Sweeper");
        sb.AppendLine("A_Sweeper_1=" + a + " C_Sweeper=" + c);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine("LIGHT " + l.name + " type=" + l.type + " color=" + l.color + " intensity=" + l.intensity + " range=" + l.range + " shadows=" + l.shadows + " pos=" + l.transform.position + " rot=" + l.transform.eulerAngles
                + " dist(A)=" + Vector3.Distance(l.transform.position, a).ToString("F1") + " dist(C)=" + Vector3.Distance(l.transform.position, c).ToString("F1") + " enabled=" + l.enabled);
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine("VOLUME " + v.name + " global=" + v.isGlobal + " priority=" + v.priority + " weight=" + v.weight + " profile=" + (v.sharedProfile ? v.sharedProfile.name : "none") + " pos=" + v.transform.position);
        foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine("REFLPROBE " + p.name + " pos=" + p.transform.position + " size=" + p.size);
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine("CAMERA " + cam.name + " enabled=" + cam.enabled);
        // pipeline asset essentials
        var rp = GraphicsSettings.currentRenderPipeline;
        sb.AppendLine("pipeline=" + (rp ? rp.name : "none") + " colorSpace=" + PlayerSettings.colorSpace);
        // materials on both sweepers' visuals (instance in scene) + ground material under each
        foreach (var n in new[] { "A_Sweeper_1", "C_Sweeper" })
        {
            var t = Find(scene, n);
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled) foreach (var m in r.sharedMaterials)
                    sb.AppendLine("MAT " + n + "/" + r.name + " " + m.name + " shader=" + m.shader.name + " base=" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-") + " metal=" + (m.HasProperty("_Metallic") ? m.GetFloat("_Metallic").ToString() : "-") + " smooth=" + (m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness").ToString() : "-") + " specHighlights=" + (m.HasProperty("_SpecularHighlights") ? m.GetFloat("_SpecularHighlights").ToString() : "-") + " envReflections=" + (m.HasProperty("_EnvironmentReflections") ? m.GetFloat("_EnvironmentReflections").ToString() : "-") + " emission=" + m.IsKeywordEnabled("_EMISSION") + " tex=" + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none"));
        }
        System.IO.Directory.CreateDirectory("ArtStaging/Integration");
        System.IO.File.WriteAllText("ArtStaging/Integration/lighting_probe.txt", sb.ToString());
    }

    static Vector3 Pos(UnityEngine.SceneManagement.Scene s, string n) { var t = Find(s, n); return t ? t.position : Vector3.zero; }
    static Transform Find(UnityEngine.SceneManagement.Scene s, string n) { foreach (var r in s.GetRootGameObjects()) { var t = F(r.transform, n); if (t) return t; } return null; }
    static Transform F(Transform t, string n) { if (t.name == n) return t; foreach (Transform c in t) { var r = F(c, n); if (r) return r; } return null; }
}

// Diagnostic renders: both sweepers from both camera directions (separates "section" from "view angle").
public static class SDDiagShots
{
    public static void Run() { Go("diag", null); }

    internal static void Go(string tag, System.Action<Transform> prepare)
    {
        var scene = EditorSceneManager.OpenScene("Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity", OpenSceneMode.Single);
        System.IO.Directory.CreateDirectory("ArtStaging/Integration");
        var sun = Object.FindFirstObjectByType<Light>();
        var log = new StringBuilder("sun forward=" + sun.transform.forward + "\n");
        var dirs = new[] { ("viewA", new Vector3(-6.5f, 5f, 8.5f).normalized), ("viewC", new Vector3(13f, 9f, -15f).normalized) };
        foreach (var n in new[] { "A_Sweeper_1", "C_Sweeper" })
        {
            Transform target = null;
            foreach (var r in scene.GetRootGameObjects()) { target = Fin(r.transform, n); if (target) break; }
            float dist = n == "A_Sweeper_1" ? 11f : 20f;
            foreach (var d in dirs)
            {
                var camGo = new GameObject("DiagCam");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 38f; cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 400f;
                camGo.transform.position = target.position + d.Item2 * dist;
                camGo.transform.LookAt(target.position + Vector3.up * 0.9f);
                var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null;
                System.IO.File.WriteAllBytes("ArtStaging/Integration/" + tag + "_" + n + "_" + d.Item1 + ".png", tex.EncodeToPNG());
                log.AppendLine(n + " " + d.Item1 + " camDir.sunFwd dot=" + Vector3.Dot((camGo.transform.position - target.position).normalized, -sun.transform.forward).ToString("F2") + " (1 = camera on the sun side)");
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
            }
        }
        System.IO.File.WriteAllText("ArtStaging/Integration/" + tag + "_log.txt", log.ToString());
    }
    static Transform Fin(Transform t, string n) { if (t.name == n) return t; foreach (Transform c in t) { var r = Fin(c, n); if (r) return r; } return null; }
}

public static class SDDiagShadows
{
    // Same viewA render of A_Sweeper_1 under different shadow / warm-up settings, plus a list of renderers around the sweeper.
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity", OpenSceneMode.Single);
        var sb = new StringBuilder();
        Transform t = null; foreach (var r in scene.GetRootGameObjects()) { t = F(r.transform, "A_Sweeper_1"); if (t) break; }
        var probe = new Bounds(t.position + Vector3.up * 0.3f, new Vector3(3f, 2f, 3f));
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (r.enabled && r.bounds.Intersects(probe))
                sb.AppendLine("NEAR " + r.name + " (" + r.transform.parent?.name + ") bounds=" + r.bounds.size.ToString("F1") + " center=" + r.bounds.center.ToString("F1") + " mat=" + (r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "null") + " cast=" + r.shadowCastingMode);
        var sun = Object.FindFirstObjectByType<Light>();
        var dir = new Vector3(-6.5f, 5f, 8.5f).normalized * 11f;
        foreach (var variant in new[] { "default", "warm2x", "noShadows" })
        {
            var prev = sun.shadows;
            if (variant == "noShadows") sun.shadows = LightShadows.None;
            var camGo = new GameObject("DiagCam"); var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f; cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 400f;
            camGo.transform.position = t.position + dir; camGo.transform.LookAt(t.position + Vector3.up * 0.9f);
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            cam.Render(); if (variant == "warm2x") { cam.Render(); cam.Render(); }
            RenderTexture.active = rt; var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null;
            var c = tex.GetPixel(480, 150); // ground pixel above the sweeper
            sb.AppendLine("RENDER " + variant + " groundPixel=" + c);
            System.IO.File.WriteAllBytes("ArtStaging/Integration/shadowtest_" + variant + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); sun.shadows = prev;
        }
        System.IO.File.WriteAllText("ArtStaging/Integration/shadowtest_log.txt", sb.ToString());
    }
    static Transform F(Transform t, string n) { if (t.name == n) return t; foreach (Transform c in t) { var r = F(c, n); if (r) return r; } return null; }
}
