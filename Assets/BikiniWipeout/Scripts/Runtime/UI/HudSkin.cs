using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Restyles the course canvas and adds progress pips, a message banner and screen-edge feedback.</summary>
    [DisallowMultipleComponent]
    public sealed class HudSkin : MonoBehaviour
    {
        private Image vignette;
        private Image banner;
        private Text message;
        private Text checkpointText;
        private float flashAlpha;
        private Color flashColor = UiTheme.Red;
        private CourseManager course;
        private KnockdownController knockdown;
        private PlayerRespawner respawner;
        private Image[] pips;
        private RectTransform pipRow;
        private int lastPipValue = -1;

        private void Awake()
        {
            Transform hud = Find("HUD");
            if (hud == null) return;

            Text timer = Child(hud, "Timer"), best = Child(hud, "Best");
            checkpointText = Child(hud, "Checkpoint");
            Text counters = Child(hud, "Counters");
            message = Child(hud, "Message");
            Text courseName = Child(transform, "CourseName");

            // ---- timer plate (top centre)
            RectTransform timerPlate = Plate(hud, "TimerPlate", UiTheme.Skin.PanelDark, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(470f, 150f));
            Plate(timerPlate, "TimerAccent", UiTheme.Skin.BannerRed, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(430f, 9f));
            Place(timer, new Vector2(0f, -30f), 70);
            Place(best, new Vector2(0f, -108f), 28);
            if (best != null) best.color = UiTheme.Cyan;
            Place(courseName, new Vector2(0f, -170f), 22);
            if (courseName != null) courseName.color = new Color(1f, 1f, 1f, 0.85f);

            // ---- checkpoint plate with progress pips (top left)
            RectTransform cpPlate = Plate(hud, "CheckpointPlate", UiTheme.Skin.PanelDark, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(430f, 120f));
            cpPlate.pivot = new Vector2(0f, 1f);
            Plate(cpPlate, "CheckpointAccent", UiTheme.Skin.BannerBlue, new Vector2(0f, 1f), new Vector2(215f, -4f), new Vector2(390f, 9f));
            Place(checkpointText, new Vector2(44f, -30f), 30);
            pipRow = new GameObject("Pips", typeof(RectTransform)) { layer = 5 }.GetComponent<RectTransform>();
            pipRow.SetParent(cpPlate, false);
            pipRow.anchorMin = pipRow.anchorMax = pipRow.pivot = new Vector2(0f, 1f);
            pipRow.anchoredPosition = new Vector2(24f, -84f);
            pipRow.sizeDelta = new Vector2(382f, 16f);

            // ---- counters plate (top right)
            RectTransform ctPlate = Plate(hud, "CounterPlate", UiTheme.Skin.PanelDark, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(330f, 120f));
            ctPlate.pivot = new Vector2(1f, 1f);
            Plate(ctPlate, "CounterAccent", UiTheme.Skin.BannerRed, new Vector2(1f, 1f), new Vector2(-165f, -4f), new Vector2(290f, 9f));
            Place(counters, new Vector2(-44f, -28f), 30);

            // ---- message banner (WIPEOUT! / CHECKPOINT!)
            if (message != null)
            {
                message.fontSize = 60;
                banner = NewImage("MessageBanner", message.transform.parent, UiTheme.Skin.BannerRed, new Vector2(0.5f, 0.5f), message.rectTransform.anchoredPosition, new Vector2(760f, 112f));
                banner.transform.SetSiblingIndex(message.transform.GetSiblingIndex());
                message.transform.SetAsLastSibling();
                Color c = banner.color; c.a = 0f; banner.color = c;
                message.fontStyle = FontStyle.BoldAndItalic;
            }

            // ---- screen-edge feedback (wipeout / respawn)
            vignette = NewImage("EdgeFlash", transform, UiTheme.Skin.Vignette, Vector2.zero, Vector2.zero, Vector2.zero);
            vignette.rectTransform.anchorMin = Vector2.zero; vignette.rectTransform.anchorMax = Vector2.one;
            vignette.rectTransform.offsetMin = vignette.rectTransform.offsetMax = Vector2.zero;
            vignette.transform.SetAsFirstSibling();
            vignette.color = new Color(1f, 0.1f, 0.15f, 0f);

            // ---- pause / settings / results
            SkinOverlay("PausePanel");
            SkinOverlay("ResultsPanel");
            Transform pauseSettings = Find("PauseSettings");
            if (pauseSettings != null)
            {
                Image full = pauseSettings.GetComponent<Image>();
                if (full != null) { full.sprite = UiTheme.Get(UiTheme.Skin.Gradient); full.type = Image.Type.Simple; full.color = Color.white; }
            }

            foreach (Button button in GetComponentsInChildren<Button>(true)) SkinButton(button);
            foreach (Image image in GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Fill") image.color = UiTheme.Cyan;
            }

            UiTheme.ApplyFonts(gameObject);
        }

        private void Start()
        {
            course = CourseManager.Current;
            if (course == null || course.Player == null) return;
            knockdown = course.Player.Knockdown;
            if (knockdown != null) knockdown.KnockedDown += OnKnockedDown;
            respawner = course.Player.GetComponentInChildren<PlayerRespawner>();
            if (respawner != null) respawner.Respawned += OnRespawned;
        }

        private void OnDestroy()
        {
            if (knockdown != null) knockdown.KnockedDown -= OnKnockedDown;
            if (respawner != null) respawner.Respawned -= OnRespawned;
        }

        private void OnKnockedDown(ImpactData impact) { Flash(UiTheme.Red, 0.6f); }
        private void OnRespawned() { Flash(UiTheme.Blue, 0.4f); }

        private void Flash(Color color, float alpha)
        {
            flashColor = color;
            flashAlpha = alpha;
        }

        private void LateUpdate()
        {
            if (vignette != null)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 0.9f);
                vignette.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            }

            if (banner != null && message != null)
            {
                bool show = !string.IsNullOrEmpty(message.text);
                if (show)
                {
                    bool red = message.text.Contains("WIPEOUT");
                    banner.sprite = UiTheme.Get(red ? UiTheme.Skin.BannerRed : UiTheme.Skin.BannerBlue);
                }

                Color c = banner.color;
                c.a = Mathf.MoveTowards(c.a, show ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                banner.color = c;
            }

            UpdatePips();
        }

        private void UpdatePips()
        {
            if (pipRow == null) return;
            CourseManager current = CourseManager.Current;
            CheckpointManager checkpoints = current != null ? current.Checkpoints : null;
            if (checkpoints == null || checkpoints.Count < 2) return;
            int total = Mathf.Min(24, checkpoints.Count - 1);
            if (pips == null || pips.Length != total)
            {
                if (pips != null) foreach (Image old in pips) if (old != null) Destroy(old.gameObject);
                pips = new Image[total];
                const float gap = 5f;
                float width = Mathf.Min(34f, (pipRow.sizeDelta.x - gap * (total - 1)) / total);
                for (int i = 0; i < total; i++)
                {
                    pips[i] = NewImage("Pip" + i, pipRow, UiTheme.Skin.Pip, new Vector2(0f, 0.5f), new Vector2(i * (width + gap) + width * 0.5f, 0f), new Vector2(width, 14f));
                }

                lastPipValue = -1;
            }

            int reached = Mathf.Clamp(checkpoints.CurrentIndex, 0, total);
            if (reached == lastPipValue) return;
            lastPipValue = reached;
            for (int i = 0; i < pips.Length; i++) pips[i].sprite = UiTheme.Get(i < reached ? UiTheme.Skin.PipOn : UiTheme.Skin.Pip);
        }

        // ------------------------------------------------------------------ helpers

        private Transform Find(string name)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private static Text Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static void Place(Text text, Vector2 position, int fontSize)
        {
            if (text == null) return;
            text.rectTransform.anchoredPosition = position;
            text.fontSize = fontSize;
        }

        private static RectTransform Plate(Transform parent, string name, UiTheme.Skin skin, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image image = NewImage(name, parent, skin, anchor, position, size);
            if (parent.name == "HUD") image.transform.SetAsFirstSibling();
            return image.rectTransform;
        }

        private static Image NewImage(string name, Transform parent, UiTheme.Skin skin, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.sprite = UiTheme.Get(skin);
            image.type = UiTheme.IsSliced(skin) ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private void SkinOverlay(string panelName)
        {
            Transform panel = Find(panelName);
            if (panel == null) return;
            Image dim = panel.GetComponent<Image>();
            if (dim != null) dim.color = new Color(0.01f, 0.03f, 0.1f, 0.78f);
            Transform box = panel.Find("Box");
            if (box == null) return;
            Image boxImage = box.GetComponent<Image>();
            if (boxImage != null) { boxImage.sprite = UiTheme.Get(UiTheme.Skin.Panel); boxImage.type = Image.Type.Sliced; boxImage.color = Color.white; }
            Text title = Child(box, "Title");
            if (title != null) title.fontStyle = FontStyle.BoldAndItalic;
        }

        private static void SkinButton(Button button)
        {
            var image = button.GetComponent<Image>();
            if (image == null) return;
            string n = button.name;
            bool primary = n.StartsWith("Resume") || n.StartsWith("Restart") || n == "Back";
            var tag = button.GetComponent<UiSkinTag>() ?? button.gameObject.AddComponent<UiSkinTag>();
            tag.skin = primary ? UiTheme.Skin.ButtonRed : UiTheme.Skin.ButtonBlue;
            tag.Apply();
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.fontStyle = FontStyle.BoldAndItalic;
        }
    }
}
