// Splashdown staged-asset helper. NOT under Assets/, so it is not compiled into the game.
// Copy into an Editor folder (or the scratch project) to (re)generate the SD_* URP Lit materials
// and remap an FBX's embedded materials to them. Values come from ArtStaging/Blender/scripts/sd_lib.py PALETTE.
using UnityEditor;
using UnityEngine;

public static class SDMaterials
{
    // name, linear base colour (as authored in Blender), metallic, smoothness (= 1 - Blender roughness, tuned)
    private static readonly (string name, Color linear, float metallic, float smoothness)[] Palette =
    {
        ("SD_Red",    new Color(0.80f, 0.04f, 0.07f), 0f, 0.74f),
        ("SD_Blue",   new Color(0.04f, 0.16f, 0.85f), 0f, 0.74f),
        ("SD_White",  new Color(0.92f, 0.93f, 0.96f), 0f, 0.62f),
        ("SD_Chrome", new Color(0.78f, 0.80f, 0.84f), 1f, 0.80f),
    };

    public const string Folder = "Assets/SDMaterials";

    public static void CreateAll()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "SDMaterials");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new System.Exception("URP Lit shader not found");
        foreach (var p in Palette)
        {
            string path = Folder + "/" + p.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit) { name = p.name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            // Blender values are linear; a Linear-colour-space project expects sRGB-entered colours.
            m.SetColor("_BaseColor", p.linear.gamma);
            m.SetFloat("_Metallic", p.metallic);
            m.SetFloat("_Smoothness", p.smoothness);
            m.SetFloat("_WorkflowMode", 1f); // metallic workflow
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Point an imported FBX's embedded materials (matched by name) at the SD_* assets.</summary>
    public static int Remap(string fbxPath)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        int n = 0;
        foreach (var p in Palette)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + p.name + ".mat");
            if (m == null) continue;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), p.name), m);
            n++;
        }
        importer.SaveAndReimport();
        return n;
    }
}
