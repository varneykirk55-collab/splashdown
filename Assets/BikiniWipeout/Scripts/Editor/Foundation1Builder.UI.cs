using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BikiniWipeout.EditorTools
{
    /// <summary>Functional, readable placeholder UI built from plain uGUI elements. Final visual design comes later.</summary>
    public static partial class Foundation1Builder
    {
        private const int UiLayer = 5;
        private static readonly Color PanelColor = new Color(0.05f, 0.12f, 0.2f, 0.92f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color ButtonColor = new Color(1f, 0.36f, 0.04f, 1f);
        private static readonly Color AccentColor = new Color(1f, 0.84f, 0.12f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Font UiFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void CreateEventSystem()
        {
            // The Input System UI module assigns its default UI actions on enable when none are set.
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.layer = UiLayer;
        }

        private static GameObject CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform UiStretch(string name, Transform parent)
        {
            return UiRect(name, parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        }

        private static Image UiImage(RectTransform rect, Color color, bool raycastTarget)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>Text anchored to a point of its parent. <paramref name="anchor"/> is used for anchors and pivot.</summary>
        private static Text UiText(string name, Transform parent, string content, int fontSize, TextAnchor alignment,
            Color color, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool outline = true)
        {
            RectTransform rect = UiRect(name, parent, anchor, anchor, anchor, anchoredPosition, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UiFont();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (outline)
            {
                var effect = rect.gameObject.AddComponent<Outline>();
                effect.effectColor = new Color(0f, 0f, 0f, 0.85f);
                effect.effectDistance = new Vector2(2f, -2f);
            }

            return text;
        }

        private static Button UiButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform rect = UiRect(name, parent, Center, Center, Center, anchoredPosition, size);
            Image image = UiImage(rect, ButtonColor, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.6f, 1f);
            colors.selectedColor = new Color(1f, 0.92f, 0.6f, 1f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.4f, 1f);
            button.colors = colors;

            Text text = UiText("Label", rect, label, 40, TextAnchor.MiddleCenter, Color.white, Center, Vector2.zero, size);
            text.raycastTarget = false;
            rect.gameObject.AddComponent<UiButtonSound>();
            rect.gameObject.AddComponent<UiPresentation>().button = true;
            return button;
        }

        private static Slider UiSlider(string name, Transform parent, string label, Vector2 anchoredPosition, float width)
        {
            RectTransform rect = UiRect(name, parent, Center, Center, Center, anchoredPosition, new Vector2(width, 32f));
            UiText("Label", rect, label, 28, TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 34f),
                new Vector2(width, 34f));

            RectTransform background = UiRect("Background", rect, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), Center,
                Vector2.zero, Vector2.zero);
            UiImage(background, new Color(0f, 0f, 0f, 0.6f), true);

            RectTransform fillArea = UiRect("Fill Area", rect, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), Center,
                new Vector2(-5f, 0f), new Vector2(-20f, 0f));
            RectTransform fill = UiRect("Fill", fillArea, Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(10f, 0f));
            UiImage(fill, AccentColor, false);

            RectTransform handleArea = UiRect("Handle Slide Area", rect, Vector2.zero, Vector2.one, Center, Vector2.zero,
                new Vector2(-20f, 0f));
            RectTransform handle = UiRect("Handle", handleArea, Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(24f, 0f));
            Image handleImage = UiImage(handle, Color.white, true);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        /// <summary>Master / music / effects / announcer sliders stacked downwards from <paramref name="top"/>.</summary>
        private static void BuildVolumeControls(Transform parent, Vector2 top, float width)
        {
            RectTransform container = UiRect("Volume", parent, Center, Center, Center, top, new Vector2(width, 240f));
            Slider master = UiSlider("MasterVolume", container, "MASTER VOLUME", new Vector2(0f, 90f), width);
            Slider music = UiSlider("MusicVolume", container, "MUSIC", new Vector2(0f, 20f), width);
            Slider sfx = UiSlider("SfxVolume", container, "SOUND EFFECTS", new Vector2(0f, -50f), width);
            Slider announcer = UiSlider("AnnouncerVolume", container, "ANNOUNCERS", new Vector2(0f, -120f), width);

            var binder = container.gameObject.AddComponent<VolumeSliders>();
            binder.masterSlider = master;
            binder.musicSlider = music;
            binder.sfxSlider = sfx;
            binder.announcerSlider = announcer;
        }

        // ------------------------------------------------------------------ gameplay UI

        private static void BuildCourseUi(CourseManager course)
        {
            GameObject canvas = CreateCanvas("UI");
            Transform root = canvas.transform;

            // ---- HUD
            UiText("CourseName", root, course.courseId == GauntletCourseId ? "THE SPLASHDOWN GAUNTLET" : "FOUNDATION COURSE", 26, TextAnchor.UpperCenter, AccentColor, new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(800f, 40f));
            RectTransform hud = UiStretch("HUD", root);
            var topCenter = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);

            var controller = canvas.AddComponent<HudController>();
            controller.course = course;
            controller.timerText = UiText("Timer", hud, "0:00.00", 72, TextAnchor.UpperCenter, Color.white, topCenter,
                new Vector2(0f, -28f), new Vector2(500f, 90f));
            controller.bestText = UiText("Best", hud, "BEST  --", 30, TextAnchor.UpperCenter, AccentColor, topCenter,
                new Vector2(0f, -112f), new Vector2(500f, 40f));
            controller.checkpointText = UiText("Checkpoint", hud, "CHECKPOINT  0 / 0", 36, TextAnchor.UpperLeft, Color.white,
                topLeft, new Vector2(40f, -36f), new Vector2(600f, 50f));
            controller.countersText = UiText("Counters", hud, "WIPEOUTS  0\nFALLS  0", 36, TextAnchor.UpperRight, Color.white,
                topRight, new Vector2(-40f, -36f), new Vector2(600f, 100f));
            controller.countdownText = UiText("Countdown", hud, string.Empty, 240, TextAnchor.MiddleCenter, AccentColor, Center,
                new Vector2(0f, 60f), new Vector2(900f, 300f));
            controller.messageText = UiText("Message", hud, string.Empty, 80, TextAnchor.MiddleCenter, Color.white, Center,
                new Vector2(0f, 260f), new Vector2(1200f, 120f));
            Text hudHint = UiText("ControlsHint", hud, ControlHints.DefaultKeyboardText, 24,
                TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.85f), Vector2.zero, new Vector2(40f, 28f), new Vector2(1700f, 36f));
            hudHint.gameObject.AddComponent<ControlHints>().target = hudHint;

            // ---- Pause menu
            RectTransform pauseDim = UiStretch("PausePanel", root);
            UiImage(pauseDim, DimColor, true);
            RectTransform pauseBox = UiRect("Box", pauseDim, Center, Center, Center, Vector2.zero, new Vector2(640f, 860f));
            UiImage(pauseBox, PanelColor, true);
            UiText("Title", pauseBox, "PAUSED", 80, TextAnchor.MiddleCenter, AccentColor, Center, new Vector2(0f, 350f),
                new Vector2(600f, 100f));

            var pause = canvas.AddComponent<PauseMenuController>();
            pause.course = course;
            pause.panel = pauseDim.gameObject;
            pause.pauseContent = pauseBox.gameObject;
            pause.resumeButton = UiButton("ResumeButton", pauseBox, "RESUME", new Vector2(0f, 220f), new Vector2(460f, 84f));
            pause.restartButton = UiButton("RestartButton", pauseBox, "RESTART COURSE", new Vector2(0f, 120f), new Vector2(460f, 84f));
            pause.mainMenuButton = UiButton("MainMenuButton", pauseBox, "MAIN MENU", new Vector2(0f, 20f), new Vector2(460f, 84f));
            pause.settingsButton = UiButton("SettingsButton", pauseBox, "SETTINGS", new Vector2(0f, -80f), new Vector2(460f, 84f));
            RectTransform pauseSettings = UiStretch("PauseSettings", pauseDim);
            UiImage(pauseSettings, new Color(0.02f, 0.18f, 0.29f, 0.98f), true);
            pause.settingsPanel = pauseSettings.gameObject;
            UiText("Title", pauseSettings, "SETTINGS", 88, TextAnchor.MiddleCenter, AccentColor, Center, new Vector2(0f, 420f), new Vector2(1400f, 100f));
            BuildVolumeControls(pauseSettings, new Vector2(-450f, 170f), 500f);
            var pauseGraphics = pauseSettings.gameObject.AddComponent<GraphicsSettingsController>();
            pauseGraphics.qualityButton = UiButton("Quality", pauseSettings, "CHANGE QUALITY", new Vector2(430f, 210f), new Vector2(600f, 75f));
            pauseGraphics.resolutionButton = UiButton("Resolution", pauseSettings, "CHANGE RESOLUTION", new Vector2(430f, 115f), new Vector2(600f, 75f));
            pauseGraphics.fullscreenButton = UiButton("Fullscreen", pauseSettings, "FULLSCREEN / WINDOWED", new Vector2(430f, 20f), new Vector2(600f, 75f));
            pauseGraphics.label = UiText("Graphics", pauseSettings, "", 30, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, -120f), new Vector2(1600f, 50f));
            UiText("Keyboard", pauseSettings, ControlHints.DefaultKeyboardText, 25, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, -190f), new Vector2(1700f, 45f));
            UiText("Gamepad", pauseSettings, ControlHints.DefaultGamepadText, 25, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, -235f), new Vector2(1700f, 45f));
            pause.settingsBackButton = UiButton("Back", pauseSettings, "BACK", new Vector2(0f, -345f), new Vector2(460f, 84f));
            pauseSettings.gameObject.AddComponent<UiPresentation>();
            pauseSettings.gameObject.SetActive(false);

            // ---- Completion screen
            RectTransform resultsDim = UiStretch("ResultsPanel", root);
            UiImage(resultsDim, DimColor, true);
            RectTransform resultsBox = UiRect("Box", resultsDim, Center, Center, Center, Vector2.zero, new Vector2(900f, 720f));
            UiImage(resultsBox, PanelColor, true);
            UiText("Title", resultsBox, "COURSE COMPLETE!", 84, TextAnchor.MiddleCenter, AccentColor, Center,
                new Vector2(0f, 270f), new Vector2(860f, 110f));

            var results = canvas.AddComponent<ResultsScreenController>();
            results.course = course;
            results.panel = resultsDim.gameObject;
            resultsBox.gameObject.AddComponent<UiPresentation>();
            results.timeText = UiText("Time", resultsBox, "TIME  0:00.00", 96, TextAnchor.MiddleCenter, Color.white, Center,
                new Vector2(0f, 140f), new Vector2(860f, 120f));
            results.bestText = UiText("Best", resultsBox, "BEST  --", 44, TextAnchor.MiddleCenter, AccentColor, Center,
                new Vector2(0f, 40f), new Vector2(860f, 60f));
            results.statsText = UiText("Stats", resultsBox, "WIPEOUTS  0     FALLS  0", 44, TextAnchor.MiddleCenter, Color.white,
                Center, new Vector2(0f, -30f), new Vector2(860f, 60f));
            results.restartButton = UiButton("RestartButton", resultsBox, "RESTART COURSE", new Vector2(0f, -135f), new Vector2(460f, 72f));
            results.courseSelectButton = UiButton("CourseSelectButton", resultsBox, "COURSE SELECT", new Vector2(0f, -220f), new Vector2(460f, 72f));
            results.mainMenuButton = UiButton("MainMenuButton", resultsBox, "MAIN MENU", new Vector2(0f, -305f), new Vector2(460f, 72f));

            // Panels start hidden; their controllers live on the always-active canvas.
            pauseDim.gameObject.SetActive(false);
            resultsDim.gameObject.SetActive(false);
        }
    }
}
