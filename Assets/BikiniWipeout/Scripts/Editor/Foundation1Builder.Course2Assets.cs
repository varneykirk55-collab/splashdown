using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Course 2 (Chaos Cove) - materials and audio. Like the Course 1 files, this only ever ADDS
    /// content: nothing generated for Foundation 1 or Course 1 is modified or overwritten here.
    /// </summary>
    public static partial class Foundation1Builder
    {
        public const string CoveTextureFolder = Root + "/Textures/Cove";
        public const string CoveMaterialFolder = MaterialsFolder + "/Course2";
        public const string Course2PrefabFolder = PrefabsFolder + "/Course2";

        /// <summary>Materials for the tropical water park and the Course 2 obstacles.</summary>
        internal sealed class CoveAssets
        {
            public Material Rock, RockDark, Sand, Grass, Waterfall, SlideWater, PalmTrunk, PalmLeaf, Flower;
            public Material DeckBlue, PillarBlue, StripesPink, RoofStripes, BucketYellow, Spring;
        }

        private static CoveAssets BuildCoveAssets()
        {
            EnsureFolder(CoveMaterialFolder);
            EnsureFolder(Course2PrefabFolder);

            var cove = new CoveAssets
            {
                Rock = CoveTextured("CoveRock", "Rock", new Vector2(2f, 2f), Color.white, 0.12f),
                RockDark = CoveTextured("CoveRockDark", "Rock", new Vector2(1.5f, 1.5f), new Color(0.72f, 0.70f, 0.76f), 0.1f),
                Sand = CoveTextured("CoveSand", "Sand", new Vector2(24f, 6f), Color.white, 0.08f),
                Waterfall = CoveTextured("CoveWaterfall", "Waterfall", new Vector2(2f, 2f), Color.white, 0.75f),
                SlideWater = CoveTextured("CoveSlideWater", "Waterfall", new Vector2(2f, 4f), new Color(0.55f, 0.86f, 1f), 0.9f),
                StripesPink = CoveTextured("CoveStripesPinkYellow", "Stripes_PinkYellow", new Vector2(2f, 1f), Color.white, 0.6f),
                RoofStripes = CoveTextured("CoveRoofStripes", "Stripes_PinkWhite", new Vector2(6f, 1f), Color.white, 0.4f),
                DeckBlue = CoveTextured("CoveDeckBlue", "Deck_Blue", new Vector2(2f, 2f), Color.white, 0.8f),
                Grass = CoveLit("CoveGrass", new Color(0.30f, 0.74f, 0.28f), 0.08f),
                PalmTrunk = CoveLit("CovePalmTrunk", new Color(0.62f, 0.44f, 0.26f), 0.1f),
                PalmLeaf = CoveLit("CovePalmLeaf", new Color(0.22f, 0.72f, 0.24f), 0.25f),
                Flower = CoveLit("CoveFlower", new Color(1f, 0.33f, 0.62f), 0.4f),
                PillarBlue = CoveLit("CovePillarBlue", new Color(0.16f, 0.36f, 0.86f), 0.84f),
                BucketYellow = CoveLit("CoveBucketYellow", new Color(1f, 0.80f, 0.10f), 0.7f),
                Spring = CoveLit("CoveSpring", new Color(0.14f, 0.30f, 0.80f), 0.75f)
            };

            EnsureCoveAudio();
            AssetDatabase.SaveAssets();
            return cove;
        }

        private static Material CoveLit(string name, Color color, float smoothness)
        {
            return LoadOrCreate(CoveMaterialFolder + "/" + name + ".mat", () =>
            {
                var material = new Material(Shader.Find(LitShaderName)) { name = name };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
                return material;
            });
        }

        private static Material CoveTextured(string name, string textureName, Vector2 tiling, Color tint, float smoothness)
        {
            return LoadOrCreate(CoveMaterialFolder + "/" + name + ".mat", () =>
            {
                var material = new Material(Shader.Find(LitShaderName)) { name = name };
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);

                string path = CoveTextureFolder + "/" + textureName + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    Debug.LogWarning("[Splashdown] Course 2 texture missing: " + path + " (run Tools/generate_cove_textures.py). Using a flat colour.");
                }
                else
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTextureScale("_BaseMap", tiling);
                }

                return material;
            });
        }

        /// <summary>
        /// Adds the Course 2 audio events to the central AudioLibrary. Only entries that do not exist
        /// yet are appended, so clips and levels that were already tuned are never touched.
        /// Every clip is a synthesized placeholder: swap it in the library asset, no code changes needed.
        /// </summary>
        private static void EnsureCoveAudio()
        {
            AudioLibrary library = BuildAudioLibrary();
            // id, volume, pitch min/max, spatial blend, minimum interval (stops stacked plays getting loud), clip
            var additions = new List<AudioLibrary.SfxEntry>
            {
                Sfx(AudioEventId.SpringBounce, 0.7f, 0.92f, 1.1f, 0.5f, 0.1f, "sfx_spring_bounce"),
                Sfx(AudioEventId.WaterJet, 0.5f, 0.9f, 1.1f, 0.85f, 0.25f, "sfx_water_jet"),
                Sfx(AudioEventId.BucketWarning, 0.55f, 0.95f, 1.05f, 0.8f, 0.3f, "sfx_bucket_creak"),
                Sfx(AudioEventId.BucketDump, 0.75f, 0.92f, 1.06f, 0.8f, 0.3f, "sfx_bucket_dump"),
                Sfx(AudioEventId.SlideWhoosh, 0.45f, 0.9f, 1.1f, 0.2f, 0.5f, "sfx_slide_whoosh"),
                Sfx(AudioEventId.BridgeCrack, 0.6f, 0.9f, 1.12f, 0.6f, 0.08f, "sfx_bridge_crack"),
                Sfx(AudioEventId.BridgeCollapse, 0.7f, 0.9f, 1.1f, 0.6f, 0.1f, "sfx_bridge_collapse"),
                Sfx(AudioEventId.InflatableBoing, 0.65f, 0.9f, 1.12f, 0.4f, 0.1f, "sfx_inflatable_boing"),
                Sfx(AudioEventId.TropicalAmbience, 0.32f, 1f, 1f, 0f, 0f, "amb_tropical_waterpark_loop")
            };

            bool changed = false;
            foreach (AudioLibrary.SfxEntry entry in additions)
            {
                if (library.FindSfx(entry.id) == null)
                {
                    library.sfx.Add(entry);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(library);
            }
        }
    }
}
