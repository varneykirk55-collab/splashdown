using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikiniWipeout
{
    [Serializable]
    public sealed class CourseEntry
    {
        public string title;
        public string sceneName;
        public bool ready;
        public bool Available => ready && !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] internal Button playButton, quitButton, characterButton, settingsButton;
        [SerializeField] internal Text bestTimeText;
        // Retained for compatibility with previously generated scenes and the Course 1 builder.
        [SerializeField] internal string courseSceneName = SceneNames.CourseFoundation1;
        [SerializeField] internal string courseId = "foundation1";
        [SerializeField] internal MusicTrackId music = MusicTrackId.MainMenu;
        [SerializeField] internal GameObject mainPanel, coursePanel, characterPanel, settingsPanel;
        [SerializeField] internal Button courseOneButton, courseTwoButton, courseBackButton, confirmButton, characterBackButton, settingsBackButton;
        [SerializeField] internal Text courseOneLabel, courseTwoLabel, characterLabel, settingsLabel;
        [SerializeField] internal Button qualityButton, resolutionButton, fullscreenButton;
        [SerializeField] internal CourseEntry[] courses = {
            new CourseEntry { title = "THE SPLASHDOWN GAUNTLET", sceneName = SceneNames.CourseGauntlet, ready = true },
            new CourseEntry { title = "COURSE 2", sceneName = "", ready = false }
        };
        private const string CharacterKey = "Splashdown.Character";
        private bool startingCourse, loading;
        private int selectedCourse;
        private int resolutionIndex;
        private Resolution[] resolutions;
        private static bool openCourses;

        public static void OpenCourseSelectOnArrival() { openCourses = true; }
        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(ShowCourses);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (characterButton != null) characterButton.onClick.AddListener(ShowCharacterDirect);
            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
            if (courseOneButton != null) courseOneButton.onClick.AddListener(() => SelectCourse(0));
            if (courseTwoButton != null) courseTwoButton.onClick.AddListener(() => SelectCourse(1));
            if (courseBackButton != null) courseBackButton.onClick.AddListener(ShowMain);
            if (characterBackButton != null) characterBackButton.onClick.AddListener(CharacterBack);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmCharacter);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(ShowMain);
            if (qualityButton != null) qualityButton.onClick.AddListener(CycleQuality);
            if (resolutionButton != null) resolutionButton.onClick.AddListener(CycleResolution);
            if (fullscreenButton != null) fullscreenButton.onClick.AddListener(ToggleFullscreen);
        }
        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioManager.PlayMusic(music);
            resolutions = Screen.resolutions;
            int quality = Mathf.Clamp(PlayerPrefs.GetInt("Splashdown.Quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(quality);
            int width = PlayerPrefs.GetInt("Splashdown.Width", Screen.width);
            int height = PlayerPrefs.GetInt("Splashdown.Height", Screen.height);
            bool fullscreen = PlayerPrefs.GetInt("Splashdown.Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            Screen.SetResolution(width, height, fullscreen);
            if (resolutions != null) for (int i = 0; i < resolutions.Length; i++) if (resolutions[i].width == width && resolutions[i].height == height) resolutionIndex = i;
            // Only the existing generated player is currently supported.
            PlayerPrefs.SetString(CharacterKey, "existing-player");
            if (openCourses) { openCourses = false; ShowCourses(); } else ShowMain();
        }
        private void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                        (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (!back) return;
            if (characterPanel != null && characterPanel.activeSelf) CharacterBack();
            else if ((coursePanel != null && coursePanel.activeSelf) || (settingsPanel != null && settingsPanel.activeSelf)) ShowMain();
        }
        private void Show(GameObject panel, Button first)
        {
            if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
            if (coursePanel != null) coursePanel.SetActive(panel == coursePanel);
            if (characterPanel != null) characterPanel.SetActive(panel == characterPanel);
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (first != null && first.interactable) first.Select();
        }
        public void ShowMain() { Show(mainPanel, playButton); }
        public void ShowCourses()
        {
            if (courses != null && courses.Length > 0 && courseOneButton != null) { courseOneButton.interactable = courses[0].Available; if (courseOneLabel != null) courseOneLabel.text = courses[0].title + (courses[0].Available ? "  •  PLAY" : "  •  UNAVAILABLE"); }
            if (courses != null && courses.Length > 1 && courseTwoButton != null) { courseTwoButton.interactable = courses[1].Available; if (courseTwoLabel != null) courseTwoLabel.text = courses[1].title + (courses[1].Available ? "  •  PLAY" : "  •  COMING SOON"); }
            Show(coursePanel, courseOneButton != null && courseOneButton.interactable ? courseOneButton : courseBackButton);
        }
        private void SelectCourse(int index)
        {
            if (courses == null || index < 0 || index >= courses.Length || !courses[index].Available) return;
            selectedCourse = index; startingCourse = true; ShowCharacter();
        }
        private void ShowCharacterDirect() { startingCourse = false; ShowCharacter(); }
        private void ShowCharacter() { if (characterLabel != null) characterLabel.text = "CURRENT PLAYER\n\nREADY TO SPLASH!"; Show(characterPanel, confirmButton); }
        private void CharacterBack() { if (startingCourse) ShowCourses(); else ShowMain(); }
        private void ConfirmCharacter()
        {
            PlayerPrefs.SetString(CharacterKey, "existing-player"); PlayerPrefs.Save();
            if (!startingCourse) { ShowMain(); return; }
            if (loading || courses == null || selectedCourse >= courses.Length || !courses[selectedCourse].Available) { ShowCourses(); return; }
            loading = true; Time.timeScale = 1f; SceneManager.LoadScene(courses[selectedCourse].sceneName);
        }
        public void StartCourse() { ShowCourses(); }
        private void ShowSettings() { RefreshSettings(); Show(settingsPanel, settingsBackButton); }
        private void CycleQuality() { QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length); PlayerPrefs.SetInt("Splashdown.Quality", QualitySettings.GetQualityLevel()); PlayerPrefs.Save(); RefreshSettings(); }
        private void CycleResolution() { if (resolutions == null || resolutions.Length == 0) return; resolutionIndex = (resolutionIndex + 1) % resolutions.Length; var r = resolutions[resolutionIndex]; Screen.SetResolution(r.width, r.height, Screen.fullScreen); PlayerPrefs.SetInt("Splashdown.Width", r.width); PlayerPrefs.SetInt("Splashdown.Height", r.height); PlayerPrefs.Save(); RefreshSettings(); }
        private void ToggleFullscreen() { bool value = !Screen.fullScreen; Screen.fullScreen = value; PlayerPrefs.SetInt("Splashdown.Fullscreen", value ? 1 : 0); PlayerPrefs.Save(); RefreshSettings(); }
        private void RefreshSettings() { if (settingsLabel != null) settingsLabel.text = "QUALITY: " + QualitySettings.names[QualitySettings.GetQualityLevel()] + "    RESOLUTION: " + Screen.width + " × " + Screen.height + "    " + (Screen.fullScreen ? "FULLSCREEN" : "WINDOWED"); }
        public void Quit() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
