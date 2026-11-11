using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>
    /// Splashdown's televised-competition look: red/blue palette, UI font and procedurally drawn glossy panels.
    /// Everything is generated at runtime (no art files), so every screen shares one source of truth.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Red = new Color(0.95f, 0.14f, 0.2f);
        public static readonly Color Blue = new Color(0.16f, 0.52f, 0.98f);
        public static readonly Color Cyan = new Color(0.45f, 0.82f, 1f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.2f);
        public static readonly Color Navy = new Color(0.03f, 0.08f, 0.22f);
        public static readonly Color Ink = new Color(0.92f, 0.96f, 1f);

        public enum Skin
        {
            Panel, PanelDark, ButtonRed, ButtonBlue, Card, CardSelected, Row, RowSelected,
            BannerRed, BannerBlue, Pip, PipOn, Glow, Gradient, Dots, Vignette, Beam, Plain
        }

        private static Font font;
        private static readonly Dictionary<Skin, Sprite> cache = new Dictionary<Skin, Sprite>();

        /// <summary>A bold sans UI face from the OS (Bahnschrift on Windows 10+), falling back to Unity's built-in font.</summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try { font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Arial" }, 40); }
                catch { font = null; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static void ApplyFonts(GameObject root)
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                text.font = Font;
                // Heavy synthetic bold turns small body text to mush; keep it for headings and buttons.
                if (text.fontSize <= 26 && text.fontStyle == FontStyle.Bold) text.fontStyle = FontStyle.Normal;
            }
        }

        public static bool IsSliced(Skin skin)
        {
            return skin != Skin.Glow && skin != Skin.Gradient && skin != Skin.Dots && skin != Skin.Vignette && skin != Skin.Beam && skin != Skin.Plain;
        }

        public static Sprite Get(Skin skin)
        {
            if (cache.TryGetValue(skin, out Sprite existing) && existing != null) return existing;
            Sprite sprite = Build(skin);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprite.texture.hideFlags = HideFlags.HideAndDontSave;
            cache[skin] = sprite;
            return sprite;
        }

        private static Sprite Build(Skin skin)
        {
            switch (skin)
            {
                case Skin.Panel: return Chamfer(128, 128, 22, 3, Hex(0x173a86, 0.94f), Hex(0x081a44, 0.96f), Hex(0x58b4ff), 0.07f);
                case Skin.PanelDark: return Chamfer(128, 128, 18, 2, Hex(0x0b1a40, 0.97f), Hex(0x030a20, 0.98f), Hex(0x2e6fc4), 0f);
                case Skin.ButtonRed: return Chamfer(128, 64, 16, 3, Hex(0xff5a5a), Hex(0xa80a1c), Hex(0xffc4c4), 0.32f);
                case Skin.ButtonBlue: return Chamfer(128, 64, 16, 3, Hex(0x2f78d8), Hex(0x0b2c68), Hex(0x7cd0ff), 0.28f);
                case Skin.Card: return Chamfer(128, 160, 18, 3, Hex(0x1b3f86), Hex(0x0a2050), Hex(0x4c9cf0), 0.05f);
                case Skin.CardSelected: return Chamfer(128, 160, 18, 4, Hex(0x9e1428), Hex(0x3e0816), Hex(0xff5a5a), 0.10f);
                case Skin.Row: return Chamfer(128, 64, 16, 3, Hex(0x1a3b80, 0.95f), Hex(0x0a2050, 0.95f), Hex(0x4c9cf0), 0.05f);
                case Skin.RowSelected: return Chamfer(128, 64, 16, 4, Hex(0x2a4fa0), Hex(0x12306c), Hex(0xffc83a), 0.12f);
                case Skin.BannerRed: return Chamfer(128, 64, 14, 3, Hex(0xff3b4a, 0.97f), Hex(0x8a0618, 0.97f), Hex(0xffb0b0), 0.3f);
                case Skin.BannerBlue: return Chamfer(128, 64, 14, 3, Hex(0x2f86f0, 0.97f), Hex(0x0a2a78, 0.97f), Hex(0xa6e0ff), 0.3f);
                case Skin.Pip: return Chamfer(32, 32, 6, 2, Hex(0x20386e), Hex(0x0c1b3e), Hex(0x3d6fb8), 0f);
                case Skin.PipOn: return Chamfer(32, 32, 6, 2, Hex(0xffe27a), Hex(0xff9a1a), Hex(0xfff3c0), 0.3f);
                case Skin.Glow: return Radial(128);
                case Skin.Gradient: return Vertical(4, 128, Hex(0x0e2a6e), Hex(0x030a22));
                case Skin.Dots: return DotGrid();
                case Skin.Vignette: return VignetteSprite();
                case Skin.Beam: return Horizontal(128, 4);
                default: return Vertical(4, 4, Color.white, Color.white);
            }
        }

        private static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
        }

        private static Sprite Make(Texture2D texture, Vector4 border)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>Chamfered-corner glossy plate: vertical gradient, bright rim, soft top gloss.</summary>
        private static Sprite Chamfer(int w, int h, int cut, int rim, Color top, Color bottom, Color rimColor, float gloss)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = Mathf.Max(cut - x, x - (w - 1 - cut), 0f);
                    float cy = Mathf.Max(cut - y, y - (h - 1 - cut), 0f);
                    float cornerDepth = (cut - (cx + cy)) / 1.4142f;           // distance inside the diagonal cut
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    float depth = edge;
                    if (cx > 0f && cy > 0f) depth = Mathf.Min(cornerDepth, edge);
                    float alpha = Mathf.Clamp01(depth + 0.5f);
                    float t = y / (float)(h - 1);
                    Color fill = Color.Lerp(bottom, top, t);
                    if (gloss > 0f && t > 0.5f) fill = Color.Lerp(fill, Color.white, gloss * Mathf.SmoothStep(0f, 1f, (t - 0.5f) * 2f));
                    float rimMix = Mathf.Clamp01(rim + 0.5f - depth);
                    Color c = Color.Lerp(fill, rimColor, rimMix);
                    c.a = Mathf.Max(fill.a, rimMix) * alpha;
                    tex.SetPixel(x, y, c);
                }
            }

            return Make(tex, new Vector4(cut, cut, cut, cut));
        }

        private static Sprite Radial(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            return Make(tex, Vector4.zero);
        }

        private static Sprite Vertical(int w, int h, Color top, Color bottom)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, Color.Lerp(bottom, top, y / (float)Mathf.Max(1, h - 1)));
            return Make(tex, Vector4.zero);
        }

        /// <summary>Soft-edged streak: transparent at both ends, bright through the middle (light beams, shimmer lines).</summary>
        private static Sprite Horizontal(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs(x / (float)(w - 1) * 2f - 1f);
                    float v = Mathf.Abs(y / (float)(h - 1) * 2f - 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - u * u) * Mathf.Clamp01(1f - v)));
                }

            return Make(tex, Vector4.zero);
        }

        private static Sprite DotGrid()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f - 0.5f, size / 2f - 0.5f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(3f - d * 1.1f)));
                }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite VignetteSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((d - 0.55f) / 0.6f)));
                }

            return Make(tex, Vector4.zero);
        }
    }

}
