using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Phase 3 — arena art and Course 1 prefabs. These files extend the Foundation 1 builder so they
    /// can reuse its helpers, but they only ever ADD content: nothing generated for Foundation 1
    /// (scenes, player prefab, config assets) is modified or overwritten here.
    /// </summary>
    public static partial class Foundation1Builder
    {
        public const string ArenaTextureFolder = Root + "/Textures/Arena";
        public const string ArenaMaterialFolder = MaterialsFolder + "/Arena";
        public const string Course1PrefabFolder = PrefabsFolder + "/Course1";

        /// <summary>Materials and effect prefabs shared by the arena and Course 1.</summary>
        internal sealed class ArenaAssets
        {
            public Material Water, Foam, PanelsPink, PanelsBlue, PanelsYellow, PanelsRainbow, ChevronPink, ChevronBlue;
            public Material Target, Checker, StripesRed, StripesBlue, Crowd, Floodlight, Sky, Cloud, Hill, Screen, Concrete;
            public Material BallRed, Ring, Confetti, Star;
        }

        private static ArenaAssets BuildArenaAssets()
        {
            EnsureFolder(ArenaMaterialFolder);
            EnsureFolder(Course1PrefabFolder);

            var arena = new ArenaAssets
            {
                Water = TexturedMaterial("ArenaWater", "Water_Caustics", new Vector2(40f, 40f), Color.white, 0.82f),
                PanelsPink = TexturedMaterial("PanelsPinkPurple", "Panels_PinkPurple", new Vector2(2f, 2f), Color.white, 0.82f),
                PanelsBlue = TexturedMaterial("PanelsBlueTeal", "Panels_BlueTeal", new Vector2(2f, 2f), Color.white, 0.81f),
                PanelsYellow = TexturedMaterial("PanelsYellowOrange", "Panels_YellowOrange", new Vector2(2f, 2f), Color.white, 0.55f),
                PanelsRainbow = TexturedMaterial("PanelsRainbow", "Panels_Rainbow", new Vector2(1f, 1f), Color.white, 0.76f),
                ChevronPink = TexturedMaterial("ChevronPinkWhite", "Chevron_PinkWhite", new Vector2(1f, 3f), Color.white, 0.5f),
                ChevronBlue = TexturedMaterial("ChevronBlueYellow", "Chevron_BlueYellow", new Vector2(1f, 3f), Color.white, 0.5f),
                Target = TexturedMaterial("TargetRings", "Target_Rings", Vector2.one, Color.white, 0.5f),
                Checker = TexturedMaterial("Checker", "Checker", new Vector2(8f, 1f), Color.white, 0.3f),
                StripesRed = TexturedMaterial("StripesRedWhite", "Stripes_RedWhite", new Vector2(2f, 1f), Color.white, 0.6f),
                StripesBlue = TexturedMaterial("StripesBlueYellow", "Stripes_BlueYellow", new Vector2(2f, 1f), Color.white, 0.6f),
                Crowd = TexturedMaterial("Crowd", "Crowd", new Vector2(6f, 1f), Color.white, 0.05f),
                Floodlight = EmissiveMaterial("Floodlight", "Floodlight", new Color(1f, 0.97f, 0.85f) * 1.6f),
                BallRed = TexturedMaterial("BallRed", "Ball_Shell", Vector2.one, Color.white, 0.94f),
                Cloud = ArenaLit("Cloud", new Color(1f, 1f, 1f), 0.05f),
                Hill = ArenaLit("Hill", new Color(0.36f, 0.78f, 0.36f), 0.05f),
                Screen = TexturedMaterial("Screen", "Scoreboard_Pixels", Vector2.one, Color.white, 0.76f),
                Concrete = ArenaLit("StandBase", new Color(0.16f, 0.23f, 0.33f), 0.38f),
                Foam = TransparentMaterial("Foam", "Universal Render Pipeline/Unlit", "Foam_Ring", new Color(1f, 1f, 1f, 0.9f)),
                Ring = TransparentMaterial("SplashRing", "Universal Render Pipeline/Particles/Unlit", "Foam_Ring", Color.white),
                Star = TransparentMaterial("ImpactStar", "Universal Render Pipeline/Particles/Unlit", "SoftDot", Color.white),
                Sky = SkyMaterial()
            };

            // Confetti reuses the opaque particle material from Foundation 1 (tinted per particle).
            arena.Confetti = SplashMaterial();
            EnsureArenaAudio();
            AssetDatabase.SaveAssets();
            return arena;
        }

        private static Material ArenaLit(string name, Color color, float smoothness)
        {
            return LoadOrCreate(ArenaMaterialFolder + "/" + name + ".mat", () =>
            {
                var material = new Material(Shader.Find(LitShaderName)) { name = name };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
                return material;
            });
        }

        private static Texture2D ArenaTexture(string textureName)
        {
            string path = ArenaTextureFolder + "/" + textureName + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning("[Splashdown] Arena texture missing: " + path + " (run Tools/generate_arena_textures.py). Using a flat colour.");
            }

            return texture;
        }

        private static Material TexturedMaterial(string name, string textureName, Vector2 tiling, Color tint, float smoothness)
        {
            return LoadOrCreate(ArenaMaterialFolder + "/" + name + ".mat", () =>
            {
                var material = new Material(Shader.Find(LitShaderName)) { name = name };
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
                Texture2D texture = ArenaTexture(textureName);
                if (texture != null)
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTextureScale("_BaseMap", tiling);
                }

                return material;
            });
        }

        private static Material EmissiveMaterial(string name, string textureName, Color emission)
        {
            return LoadOrCreate(ArenaMaterialFolder + "/" + name + ".mat", () =>
            {
                var material = new Material(Shader.Find(LitShaderName)) { name = name };
                Texture2D texture = ArenaTexture(textureName);
                if (texture != null)
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTexture("_EmissionMap", texture);
                }

                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                return material;
            });
        }

        /// <summary>Alpha-blended material (same surface settings the URP material inspector writes).</summary>
        private static Material TransparentMaterial(string name, string shaderName, string textureName, Color color)
        {
            return LoadOrCreate(ArenaMaterialFolder + "/" + name + ".mat", () =>
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                var material = new Material(shader) { name = name };
                material.SetColor("_BaseColor", color);
                Texture2D texture = ArenaTexture(textureName);
                if (texture != null)
                {
                    material.SetTexture("_BaseMap", texture);
                }

                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("DepthOnly", false);
                material.SetShaderPassEnabled("ShadowCaster", false);
                return material;
            });
        }

        private static Material SkyMaterial()
        {
            return LoadOrCreate(ArenaMaterialFolder + "/ArenaSky.mat", () =>
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                {
                    return new Material(Shader.Find(LitShaderName)) { name = "ArenaSky" };
                }

                // Clear, saturated summer sky with a soft sun disc.
                var material = new Material(shader) { name = "ArenaSky" };
                material.SetFloat("_SunSize", 0.045f);
                material.SetFloat("_SunSizeConvergence", 6f);
                material.SetFloat("_AtmosphereThickness", 0.85f);
                material.SetColor("_SkyTint", new Color(0.35f, 0.62f, 1f));
                material.SetColor("_GroundColor", new Color(0.35f, 0.80f, 0.85f));
                material.SetFloat("_Exposure", 1.25f);
                return material;
            });
        }

        /// <summary>
        /// Adds the Phase 3 audio events to the central AudioLibrary. Only entries that do not exist
        /// yet are appended, so clips and levels that were already tuned are never touched.
        /// </summary>
        private static void EnsureArenaAudio()
        {
            AudioLibrary library = BuildAudioLibrary();
            var additions = new List<AudioLibrary.SfxEntry>
            {
                Sfx(AudioEventId.BallBounce, 0.55f, 0.85f, 1.15f, 0.6f, 0.08f, "sfx_ball_bounce"),
                Sfx(AudioEventId.MechanismWhoosh, 0.5f, 0.85f, 1.15f, 0.7f, 0.2f, "sfx_mechanism_whoosh"),
                Sfx(AudioEventId.CrowdCheer, 0.9f, 1f, 1f, 0f, 1f, "sfx_crowd_cheer"),
                Sfx(AudioEventId.ArenaAmbience, 0.35f, 1f, 1f, 0f, 0f, "amb_arena_crowd_loop"),
                Sfx(AudioEventId.ConfettiPop, 0.8f, 0.95f, 1.05f, 0.2f, 0.3f, "sfx_confetti_pop")
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
