using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout.EditorTools
{
    /// <summary>The televised-competition main menu: one hub screen with navigation, character and course selection.</summary>
    public static partial class Foundation1Builder
    {
        private const string GeneratedUi = "Assets/BikiniWipeout/UI/Generated/";

        /// <summary>Batch entry point: rebuilds only the MainMenu scene (no dialogs). Other scenes and prefabs are kept.</summary>
        public static void RebuildMainMenuBatch()
        {
            try
            {
                overwriteGenerated = false;
                EnsureFolders();
                var assets = BuildSharedAssets();
                var prefabs = BuildPrefabs(assets);
                overwriteGenerated = true;
                BuildMainMenuScene(assets, prefabs);
                RegisterScenes();
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception exception) { Debug.LogError("[Splashdown] Menu rebuild failed: " + exception); EditorApplication.Exit(1); }
            finally { overwriteGenerated = false; }
        }

        private static Image Skinned(string name, Transform parent, UiTheme.Skin skin, Vector2 anchor, Vector2 position, Vector2 size,
            Color? tint = null, float rotation = 0f, float ppu = 1f)
        {
            RectTransform rect = UiRect(name, parent, anchor, anchor, Center, position, size);
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
            Image image = UiImage(rect, tint ?? Color.white, false);
            var tag = rect.gameObject.AddComponent<UiSkinTag>();
            tag.skin = skin; tag.tint = tint ?? Color.white; tag.ppuMultiplier = ppu;
            return image;
        }

        private static Image SkinnedStretch(string name, Transform parent, UiTheme.Skin skin, Color tint, float ppu = 1f)
        {
            RectTransform rect = UiStretch(name, parent);
            Image image = UiImage(rect, tint, false);
            var tag = rect.gameObject.AddComponent<UiSkinTag>();
            tag.skin = skin; tag.tint = tint; tag.ppuMultiplier = ppu;
            return image;
        }

        private static Text Italic(Text text)
        {
            text.fontStyle = FontStyle.BoldAndItalic;
            return text;
        }

        private static Button HubButton(string name, Transform parent, string label, UiTheme.Skin skin, Vector2 position, Vector2 size, int fontSize)
        {
            RectTransform rect = UiRect(name, parent, Center, Center, Center, position, size);
            Image image = UiImage(rect, Color.white, true);
            var tag = rect.gameObject.AddComponent<UiSkinTag>();
            tag.skin = skin;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Italic(UiText("Label", rect, label, fontSize, TextAnchor.MiddleCenter, Color.white, Center, Vector2.zero, size));
            rect.gameObject.AddComponent<UiButtonSound>();
            rect.gameObject.AddComponent<UiPresentation>().button = true;
            return button;
        }

        private static Sprite UiSprite(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedUi + file);
        }

        private static void PanelHeader(Transform panel, string title, float y)
        {
            Italic(UiText("Header", panel, title, 44, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, y), new Vector2(520f, 60f)));
            Text left = Italic(UiText("SlashL", panel, "//", 44, TextAnchor.MiddleCenter, UiTheme.Red, Center, new Vector2(-232f, y), new Vector2(80f, 60f), false));
            Text right = Italic(UiText("SlashR", panel, "//", 44, TextAnchor.MiddleCenter, UiTheme.Red, Center, new Vector2(232f, y), new Vector2(80f, 60f), false));
            left.alignment = right.alignment = TextAnchor.MiddleCenter;
        }

        private static void BuildMainMenuUi()
        {
            GameObject canvas = CreateCanvas("UI");
            Transform root = canvas.transform;
            var menu = canvas.AddComponent<MainMenuController>();

            // ---- stage backdrop
            Image bg = SkinnedStretch("Backdrop", root, UiTheme.Skin.Gradient, Color.white);
            SkinnedStretch("LedGrid", root, UiTheme.Skin.Dots, new Color(0.35f, 0.65f, 1f, 0.11f), 0.5f);
            Image glowTop = Skinned("GlowLogo", root, UiTheme.Skin.Glow, Center, new Vector2(0f, 360f), new Vector2(1700f, 720f), new Color(0.2f, 0.5f, 1f, 0.7f));
            Image glowLeft = Skinned("GlowRedLeft", root, UiTheme.Skin.Glow, Center, new Vector2(-900f, 260f), new Vector2(900f, 900f), new Color(1f, 0.1f, 0.2f, 0.45f));
            Image glowRight = Skinned("GlowRedRight", root, UiTheme.Skin.Glow, Center, new Vector2(900f, 260f), new Vector2(900f, 900f), new Color(1f, 0.1f, 0.2f, 0.45f));
            Image water = Skinned("Water", root, UiTheme.Skin.Gradient, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(2400f, 380f), new Color(0.1f, 0.65f, 1f, 0.6f));
            Skinned("GlowWater", root, UiTheme.Skin.Glow, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(2400f, 420f), new Color(0.2f, 0.7f, 1f, 0.55f));
            var beams = new RectTransform[4];
            for (int i = 0; i < beams.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                beams[i] = Skinned("Beam" + i, root, UiTheme.Skin.Beam, new Vector2(0.5f, 1f), new Vector2(side * (380f + i * 230f), -120f),
                    new Vector2(1500f, 70f), new Color(0.8f, 0.92f, 1f, 0.10f), side * (62f - i * 6f)).rectTransform;
            }

            var shimmers = new RectTransform[6];
            for (int i = 0; i < shimmers.Length; i++)
            {
                shimmers[i] = Skinned("Shimmer" + i, root, UiTheme.Skin.Beam, new Vector2(0.5f, 0f), new Vector2(-700f + i * 280f, 40f + (i % 3) * 55f),
                    new Vector2(380f, 6f), new Color(0.8f, 0.95f, 1f, 0.35f)).rectTransform;
            }

            Skinned("PodiumLeft", root, UiTheme.Skin.ButtonRed, new Vector2(0f, 0f), new Vector2(120f, -30f), new Vector2(560f, 150f), Color.white, 10f);
            Skinned("PodiumRight", root, UiTheme.Skin.ButtonRed, new Vector2(1f, 0f), new Vector2(-120f, -30f), new Vector2(560f, 150f), Color.white, -10f);
            var ambience = bg.gameObject.AddComponent<MenuAmbience>();
            ambience.glows = new[] { glowTop, glowLeft, glowRight };
            ambience.beams = beams;
            ambience.shimmers = shimmers;

            // ---- logo
            RectTransform logo = UiRect("Logo", root, Center, Center, Center, new Vector2(0f, 432f), new Vector2(1500f, 220f));
            Text shadow = Italic(UiText("TitleShadow", logo, "SPLASHDOWN", 150, TextAnchor.MiddleCenter, new Color(0.02f, 0.12f, 0.5f, 0.95f), Center, new Vector2(7f, -8f), new Vector2(1500f, 190f), false));
            Text title = Italic(UiText("Title", logo, "SPLASHDOWN", 150, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, 0f), new Vector2(1500f, 190f), false));
            var titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.1f, 0.45f, 1f, 1f); titleOutline.effectDistance = new Vector2(5f, -5f);
            Skinned("Swoosh", logo, UiTheme.Skin.BannerRed, Center, new Vector2(0f, -100f), new Vector2(800f, 16f), Color.white, -1.5f);
            Italic(UiText("Subtitle", logo, "THE ULTIMATE OBSTACLE COURSE", 30, TextAnchor.MiddleCenter, UiTheme.Cyan, Center, new Vector2(0f, -136f), new Vector2(900f, 40f)));
            logo.gameObject.AddComponent<UiPresentation>();
            shadow.raycastTarget = false;

            // ---- navigation column
            RectTransform nav = Skinned("Navigation", root, UiTheme.Skin.Panel, Center, new Vector2(-700f, -20f), new Vector2(320f, 520f)).rectTransform;
            nav.gameObject.AddComponent<UiPresentation>();
            menu.playButton = HubButton("Play", nav, "PLAY", UiTheme.Skin.ButtonRed, new Vector2(0f, 170f), new Vector2(276f, 104f), 56);
            menu.characterButton = HubButton("Characters", nav, "CHARACTERS", UiTheme.Skin.ButtonBlue, new Vector2(0f, 50f), new Vector2(276f, 76f), 34);
            menu.courseButton = HubButton("Courses", nav, "COURSES", UiTheme.Skin.ButtonBlue, new Vector2(0f, -32f), new Vector2(276f, 76f), 34);
            menu.settingsButton = HubButton("Options", nav, "OPTIONS", UiTheme.Skin.ButtonBlue, new Vector2(0f, -114f), new Vector2(276f, 76f), 34);
            menu.quitButton = HubButton("Quit", nav, "QUIT", UiTheme.Skin.ButtonBlue, new Vector2(0f, -196f), new Vector2(276f, 76f), 34);

            // ---- character selection
            RectTransform characters = Skinned("CharacterSelect", root, UiTheme.Skin.Panel, Center, new Vector2(-180f, -20f), new Vector2(640f, 520f)).rectTransform;
            characters.gameObject.AddComponent<UiPresentation>();
            PanelHeader(characters, "SELECT CHARACTER", 218f);
            menu.characterCards = new MenuCharacterCard[2];
            for (int i = 0; i < 2; i++)
            {
                menu.characterCards[i] = BuildCharacterCard(characters, "Card" + i, new Vector2(i == 0 ? -125f : 125f, -22f));
            }

            menu.previousCharacterButton = HubButton("Previous", characters, "<", UiTheme.Skin.ButtonBlue, new Vector2(-292f, -22f), new Vector2(48f, 100f), 44);
            menu.nextCharacterButton = HubButton("Next", characters, ">", UiTheme.Skin.ButtonBlue, new Vector2(292f, -22f), new Vector2(48f, 100f), 44);

            // ---- course selection
            RectTransform courses = Skinned("CourseSelect", root, UiTheme.Skin.Panel, Center, new Vector2(520f, -20f), new Vector2(680f, 520f)).rectTransform;
            courses.gameObject.AddComponent<UiPresentation>();
            PanelHeader(courses, "SELECT COURSE", 218f);
            menu.courseRows = new MenuCourseRow[3];
            for (int i = 0; i < 3; i++)
            {
                menu.courseRows[i] = BuildCourseRow(courses, "Row" + i, new Vector2(0f, 128f - i * 142f));
            }

            menu.previousCoursePageButton = HubButton("PreviousPage", courses, "<", UiTheme.Skin.ButtonBlue, new Vector2(-60f, -240f), new Vector2(64f, 36f), 26);
            menu.nextCoursePageButton = HubButton("NextPage", courses, ">", UiTheme.Skin.ButtonBlue, new Vector2(60f, -240f), new Vector2(64f, 36f), 26);

            // ---- start
            menu.summaryText = UiText("Summary", root, "", 28, TextAnchor.MiddleCenter, UiTheme.Cyan, Center, new Vector2(0f, -318f), new Vector2(1200f, 40f));
            menu.startButton = HubButton("StartCourse", root, "START COURSE", UiTheme.Skin.ButtonRed, new Vector2(0f, -410f), new Vector2(640f, 112f), 58);
            UiText("Hint", root, "ARROW KEYS / D-PAD  NAVIGATE      ENTER / A  SELECT      ESC / B  BACK", 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f), Center, new Vector2(0f, -500f), new Vector2(1400f, 30f), false);

            // ---- options overlay
            RectTransform overlay = UiStretch("Options", root); menu.settingsPanel = overlay.gameObject;
            UiImage(overlay, new Color(0.01f, 0.03f, 0.1f, 0.82f), true);
            RectTransform box = Skinned("Box", overlay, UiTheme.Skin.Panel, Center, Vector2.zero, new Vector2(1300f, 780f)).rectTransform;
            box.gameObject.AddComponent<UiPresentation>();
            Italic(UiText("Title", box, "OPTIONS", 76, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, 325f), new Vector2(1000f, 100f)));
            BuildVolumeControls(box, new Vector2(-330f, 150f), 460f);
            var graphics = overlay.gameObject.AddComponent<GraphicsSettingsController>();
            graphics.qualityButton = HubButton("Quality", box, "CHANGE QUALITY", UiTheme.Skin.ButtonBlue, new Vector2(330f, 215f), new Vector2(480f, 76f), 30);
            graphics.resolutionButton = HubButton("Resolution", box, "CHANGE RESOLUTION", UiTheme.Skin.ButtonBlue, new Vector2(330f, 125f), new Vector2(480f, 76f), 30);
            graphics.fullscreenButton = HubButton("Fullscreen", box, "FULLSCREEN / WINDOWED", UiTheme.Skin.ButtonBlue, new Vector2(330f, 35f), new Vector2(480f, 76f), 30);
            graphics.label = UiText("Graphics", box, "", 28, TextAnchor.MiddleCenter, UiTheme.Cyan, Center, new Vector2(0f, -95f), new Vector2(1200f, 44f));
            UiText("Keyboard", box, ControlHints.DefaultKeyboardText, 23, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, -175f), new Vector2(1240f, 40f));
            UiText("Gamepad", box, ControlHints.DefaultGamepadText, 23, TextAnchor.MiddleCenter, Color.white, Center, new Vector2(0f, -220f), new Vector2(1240f, 40f));
            menu.settingsBackButton = HubButton("Back", box, "BACK", UiTheme.Skin.ButtonRed, new Vector2(0f, -305f), new Vector2(380f, 84f), 40);
            foreach (Image image in box.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Fill") image.color = UiTheme.Cyan;
                else if (image.name == "Background" && image.GetComponentInParent<Slider>() != null) image.color = new Color(0.02f, 0.06f, 0.2f, 0.95f);
            }

            overlay.gameObject.SetActive(false);

            // ---- entries: portraits and thumbnails are rendered from existing project assets (UiThumbnails)
            menu.characters[0].portrait = UiSprite("Portrait_Cowgirl.png");
            menu.characters[1].portrait = UiSprite("Portrait_StarsStripes.png");
            menu.courses[0].thumbnail = UiSprite("Thumb_Gauntlet.png");
            menu.courses[1].thumbnail = UiSprite("Thumb_ChaosCove.png");
            menu.courseSceneName = SceneNames.CourseGauntlet;
            menu.courseId = GauntletCourseId;
        }

        private static MenuCharacterCard BuildCharacterCard(Transform parent, string name, Vector2 position)
        {
            RectTransform rect = UiRect(name, parent, Center, Center, Center, position, new Vector2(262f, 412f));
            Image frame = UiImage(rect, Color.white, true);
            var tag = rect.gameObject.AddComponent<UiSkinTag>(); tag.skin = UiTheme.Skin.Card;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
            rect.gameObject.AddComponent<UiButtonSound>();
            rect.gameObject.AddComponent<UiPresentation>().button = true;

            RectTransform portraitRect = UiRect("Portrait", rect, Center, Center, Center, new Vector2(0f, 26f), new Vector2(238f, 340f));
            Image portrait = UiImage(portraitRect, Color.white, false);
            portrait.preserveAspect = true;

            Image plate = Skinned("NamePlate", rect, UiTheme.Skin.BannerBlue, Center, new Vector2(0f, -172f), new Vector2(238f, 50f));
            Text label = Italic(UiText("Name", plate.transform, "", 28, TextAnchor.MiddleCenter, Color.white, Center, Vector2.zero, new Vector2(238f, 50f)));

            Image check = Skinned("Check", rect, UiTheme.Skin.BannerRed, Center, new Vector2(96f, 172f), new Vector2(52f, 52f));
            Skinned("CheckShort", check.transform, UiTheme.Skin.Plain, Center, new Vector2(-7f, -5f), new Vector2(8f, 18f), Color.white, 45f);
            Skinned("CheckLong", check.transform, UiTheme.Skin.Plain, Center, new Vector2(5f, 1f), new Vector2(8f, 32f), Color.white, -45f);

            var card = rect.gameObject.AddComponent<MenuCharacterCard>();
            card.button = button; card.frame = frame; card.portrait = portrait; card.check = check; card.nameText = label;
            return card;
        }

        private static MenuCourseRow BuildCourseRow(Transform parent, string name, Vector2 position)
        {
            RectTransform rect = UiRect(name, parent, Center, Center, Center, position, new Vector2(648f, 130f));
            Image frame = UiImage(rect, Color.white, true);
            var tag = rect.gameObject.AddComponent<UiSkinTag>(); tag.skin = UiTheme.Skin.Row;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
            rect.gameObject.AddComponent<UiButtonSound>();
            rect.gameObject.AddComponent<UiPresentation>().button = true;

            RectTransform thumbRect = UiRect("Thumbnail", rect, Center, Center, Center, new Vector2(-203f, 0f), new Vector2(226f, 112f));
            Image thumbnail = UiImage(thumbRect, Color.white, false);
            thumbnail.preserveAspect = false;

            Text number = Italic(UiText("Number", rect, "", 40, TextAnchor.MiddleLeft, Color.white, Center, new Vector2(130f, 30f), new Vector2(380f, 50f)));
            Text title = UiText("Title", rect, "", 28, TextAnchor.MiddleLeft, UiTheme.Cyan, Center, new Vector2(130f, -10f), new Vector2(380f, 40f));
            Text subtitle = UiText("Subtitle", rect, "", 21, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f), Center, new Vector2(130f, -42f), new Vector2(380f, 34f), false);

            var row = rect.gameObject.AddComponent<MenuCourseRow>();
            row.button = button; row.frame = frame; row.thumbnail = thumbnail; row.numberText = number; row.titleText = title; row.subtitleText = subtitle;
            return row;
        }
    }
}
