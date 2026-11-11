#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BikiniWipeout.Tests
{
    /// <summary>
    /// Phase 4B: the hub menu lists both characters and courses, the chosen character and course decide the
    /// scene that loads, and the in-course UI skin attaches without disturbing gameplay objects.
    /// Also writes review screenshots to Logs/ui-shots (BMP) - these are automated captures, not a playtest.
    /// </summary>
    public class UiOverhaulPlayTests
    {
        private const string MenuPath = "Assets/BikiniWipeout/Scenes/MainMenu.unity";
        private const string ShotFolder = "Logs/ui-shots";

        [SetUp]
        public void ClearPrefs()
        {
            PlayerPrefs.DeleteKey("Splashdown.Character");
            PlayerPrefs.DeleteKey("Splashdown.Course");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PlayerPrefs.DeleteKey("Splashdown.Character");
            PlayerPrefs.DeleteKey("Splashdown.Course");
            yield return null;
        }

        private static IEnumerator LoadMenu()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(MenuPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Menu_ListsBothCharacters_AndBothCourses()
        {
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(menu, "MainMenuController");
            var cards = menu.characterCards.Where(c => c.gameObject.activeSelf).ToArray();
            Assert.AreEqual(2, cards.Length, "visible character cards");
            CollectionAssert.AreEqual(new[] { "Original", "Stars & Stripes" }, cards.Select(c => c.nameText.text).ToArray());
            Assert.IsTrue(cards.All(c => c.portrait.sprite != null), "portraits assigned");
            var rows = menu.courseRows.Where(r => r.gameObject.activeSelf).ToArray();
            Assert.AreEqual(2, rows.Length, "visible course rows");
            Assert.IsTrue(rows.All(r => r.button.interactable && r.thumbnail.sprite != null), "both courses available with thumbnails");
            Assert.IsTrue(menu.startButton.interactable, "Start enabled");
            Assert.IsNotNull(menu.settingsPanel);
            Assert.IsFalse(menu.settingsPanel.activeSelf, "options closed initially");
        }

        [UnityTest]
        public IEnumerator Menu_Options_OpensAndBackCloses()
        {
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.settingsButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(menu.settingsPanel.activeSelf, "options open");
            Assert.IsTrue(menu.settingsPanel.GetComponentsInChildren<Slider>(true).Length >= 3, "volume sliders present");
            menu.settingsBackButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(menu.settingsPanel.activeSelf, "options closed by Back");
        }

        [UnityTest]
        public IEnumerator Menu_CharacterAndCourseSelection_Persists()
        {
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.characterCards[1].button.onClick.Invoke();
            menu.courseRows[1].button.onClick.Invoke();
            yield return null;
            Assert.AreEqual("charD", PlayerPrefs.GetString("Splashdown.Character"));
            Assert.AreEqual(SceneNames.CourseChaosCove, PlayerPrefs.GetString("Splashdown.Course"));
            Assert.IsTrue(menu.characterCards[1].check.gameObject.activeSelf && !menu.characterCards[0].check.gameObject.activeSelf, "check mark follows selection");

            // Reloading the menu restores the saved choices.
            yield return LoadMenu();
            menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsTrue(menu.characterCards[1].check.gameObject.activeSelf, "Stars & Stripes still selected after reload");
            Assert.IsTrue(menu.summaryText.text.Contains("CHAOS COVE") && menu.summaryText.text.Contains("STARS"), menu.summaryText.text);
        }

        // character card index, course row index, expected scene, expected model-name fragment
        [UnityTest]
        public IEnumerator Launch_OriginalOnGauntlet() { yield return LaunchCase(0, 0, "Course_SplashdownGauntlet", "charA"); }

        [UnityTest]
        public IEnumerator Launch_StarsStripesOnGauntlet() { yield return LaunchCase(1, 0, "Course_SplashdownGauntlet_StarsStripes", "charD"); }

        [UnityTest]
        public IEnumerator Launch_OriginalOnChaosCove() { yield return LaunchCase(0, 1, "Course_ChaosCove", "charA"); }

        [UnityTest]
        public IEnumerator Launch_StarsStripesOnChaosCove() { yield return LaunchCase(1, 1, "Course_ChaosCove_StarsStripes", "charD"); }

        private static IEnumerator LaunchCase(int card, int row, string expectedScene, string modelFragment)
        {
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.characterCards[card].button.onClick.Invoke();
            menu.courseRows[row].button.onClick.Invoke();
            menu.startButton.onClick.Invoke();
            float timeout = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != expectedScene && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual(expectedScene, SceneManager.GetActiveScene().name, "loaded scene");
            yield return null; yield return null;
            var course = CourseManager.Current;
            Assert.IsNotNull(course, "CourseManager in " + expectedScene);
            Assert.IsNotNull(course.Player, "player in " + expectedScene);
            var names = course.Player.GetComponentsInChildren<Transform>(true).Select(t => t.name);
            Assert.IsTrue(names.Any(n => n.Contains(modelFragment)), "player model contains '" + modelFragment + "'");
            Assert.IsNotNull(Object.FindAnyObjectByType<HudSkin>(), "HUD skin attached");
            Assert.IsNotNull(course.Player.GetComponent<CharacterMotor>(), "motor intact");
        }

        // ------------------------------------------------------------------ review screenshots

        [UnityTest]
        public IEnumerator Screenshots_MenuAndHud()
        {
            Directory.CreateDirectory(ShotFolder);
            yield return LoadMenu();
            yield return new WaitForSeconds(1.2f);
            Capture("01_menu_hub");
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.characterCards[1].button.onClick.Invoke();
            menu.courseRows[1].button.onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Capture("02_menu_stars_chaoscove");
            menu.settingsButton.onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Capture("03_menu_options");
            menu.settingsBackButton.onClick.Invoke();

            PlayerPrefs.SetString("Splashdown.Character", "existing-player");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(1.5f);
            Capture("04_hud_countdown");
            yield return new WaitForSeconds(4f);
            Capture("05_hud_running");
            var pause = Object.FindAnyObjectByType<PauseMenuController>();
            pause.panel.SetActive(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Capture("06_pause");
            pause.panel.SetActive(false);
            var results = Object.FindAnyObjectByType<ResultsScreenController>();
            results.timeText.text = "TIME  1:23.45"; results.bestText.text = "NEW BEST TIME!"; results.statsText.text = "CHECKPOINTS  5 / 5    WIPEOUTS  2    FALLS  1";
            results.panel.SetActive(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Capture("07_results");
            results.panel.SetActive(false);
        }

        private static void Capture(string name)
        {
            Camera cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;
            var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay && c.rootCanvas == c).ToArray();
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }
            var rt = new RenderTexture(1920, 1080, 24);
            var previousTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = previousTarget;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            WriteBmp(ShotFolder + "/" + name + ".bmp", tex);
            Object.Destroy(rt); Object.Destroy(tex);
        }

        private static void WriteBmp(string path, Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Color32[] pixels = tex.GetPixels32();
            int rowSize = (w * 3 + 3) & ~3;
            using (var stream = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(stream))
            {
                bw.Write((byte)'B'); bw.Write((byte)'M'); bw.Write(54 + rowSize * h); bw.Write(0); bw.Write(54);
                bw.Write(40); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)24); bw.Write(0); bw.Write(rowSize * h); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
                var row = new byte[rowSize];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++) { Color32 p = pixels[y * w + x]; row[x * 3] = p.b; row[x * 3 + 1] = p.g; row[x * 3 + 2] = p.r; }
                    bw.Write(row);
                }
            }
        }
    }
}
#endif
