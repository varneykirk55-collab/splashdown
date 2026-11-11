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

    [Serializable]
    public sealed class CharacterEntry
    {
        public string id;
        public string displayName;
        public bool ready;
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
        [SerializeField] internal Button previousCharacterButton, nextCharacterButton, previousCourseButton, nextCourseButton;
        [SerializeField] internal CharacterEntry[] characters = { new CharacterEntry { id = "existing-player", displayName = "CURRENT PLAYER", ready = true } };
        [SerializeField] internal CourseEntry[] courses = {
            new CourseEntry { title = "THE SPLASHDOWN GAUNTLET", sceneName = SceneNames.CourseGauntlet, ready = true },
            new CourseEntry { title = "COURSE 2", sceneName = "", ready = false }
        };
        private const string CharacterKey = "Splashdown.Character";
        private bool startingCourse, loading;
        private int selectedCourse;
        private int characterIndex, coursePage;
        private static bool openCourses;

        public static void OpenCourseSelectOnArrival() { openCourses = true; }
        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(ShowCourses);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (characterButton != null) characterButton.onClick.AddListener(ShowCharacterDirect);
            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
            if (courseOneButton != null) courseOneButton.onClick.AddListener(() => SelectCourse(coursePage * 2));
            if (courseTwoButton != null) courseTwoButton.onClick.AddListener(() => SelectCourse(coursePage * 2 + 1));
            if (courseBackButton != null) courseBackButton.onClick.AddListener(BackToMain);
            if (characterBackButton != null) characterBackButton.onClick.AddListener(CharacterBack);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmCharacter);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(BackToMain);
            if (previousCharacterButton != null) previousCharacterButton.onClick.AddListener(() => ChangeCharacter(-1));
            if (nextCharacterButton != null) nextCharacterButton.onClick.AddListener(() => ChangeCharacter(1));
            if (previousCourseButton != null) previousCourseButton.onClick.AddListener(() => ChangePage(-1));
            if (nextCourseButton != null) nextCourseButton.onClick.AddListener(() => ChangePage(1));
        }
        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioManager.PlayMusic(music);
            GraphicsSettingsController.ApplySaved();
            string saved = PlayerPrefs.GetString(CharacterKey, "existing-player");
            characterIndex = -1;
            if (characters != null) for (int i = 0; i < characters.Length; i++)
            {
                if (!IsCharacterAvailable(i)) continue;
                if (characterIndex < 0) characterIndex = i;
                if (characters[i].id == saved) { characterIndex = i; break; }
            }
            if (openCourses) { openCourses = false; ShowCourses(); } else ShowMain();
        }
        private void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                        (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (!back) return;
            if (characterPanel != null && characterPanel.activeSelf) CharacterBack();
            else if ((coursePanel != null && coursePanel.activeSelf) || (settingsPanel != null && settingsPanel.activeSelf)) BackToMain();
        }
        private void Show(GameObject panel, Button first)
        {
            AudioManager.Play(AudioEventId.UiTransition);
            if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
            if (coursePanel != null) coursePanel.SetActive(panel == coursePanel);
            if (characterPanel != null) characterPanel.SetActive(panel == characterPanel);
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (first != null && first.interactable) first.Select();
        }
        public void ShowMain() { Show(mainPanel, playButton); }
        private void BackToMain() { AudioManager.Play(AudioEventId.UiBack); ShowMain(); }
        public void ShowCourses()
        {
            SetCourseCard(coursePage * 2, courseOneButton, courseOneLabel);
            SetCourseCard(coursePage * 2 + 1, courseTwoButton, courseTwoLabel);
            if (previousCourseButton != null) previousCourseButton.interactable = coursePage > 0;
            if (nextCourseButton != null) nextCourseButton.interactable = courses != null && (coursePage + 1) * 2 < courses.Length;
            Show(coursePanel, courseOneButton != null && courseOneButton.gameObject.activeSelf && courseOneButton.interactable ? courseOneButton : courseBackButton);
        }
        private void SetCourseCard(int index, Button button, Text label)
        {
            if (button == null) return;
            bool exists = courses != null && index < courses.Length && courses[index] != null;
            button.gameObject.SetActive(exists);
            if (!exists) return;
            button.interactable = courses[index].Available;
            if (label != null) label.text = courses[index].title + (courses[index].Available ? "\nREADY TO PLAY" : (courses[index].ready ? "\nUNAVAILABLE" : "\nCOMING SOON"));
        }
        private void ChangePage(int delta) { coursePage = Mathf.Max(0, coursePage + delta); ShowCourses(); }
        private void SelectCourse(int index)
        {
            if (courses == null || index < 0 || index >= courses.Length || courses[index] == null || !courses[index].Available) return;
            AudioManager.Play(AudioEventId.UiCourseSelect);
            selectedCourse = index; startingCourse = true; ShowCharacter();
        }
        private void ShowCharacterDirect() { startingCourse = false; ShowCharacter(); }
        private bool IsCharacterAvailable(int index) { return characters != null && index >= 0 && index < characters.Length && characters[index] != null && characters[index].ready && characters[index].id == "existing-player"; }
        private void ShowCharacter() { RefreshCharacter(); Show(characterPanel, confirmButton); }
        private void RefreshCharacter()
        {
            bool valid = IsCharacterAvailable(characterIndex);
            if (characterLabel != null) characterLabel.text = valid ? characters[characterIndex].displayName + "\n\nSELECTED\nPREVIEW UNAVAILABLE" : "NO PLAYER AVAILABLE";
            if (confirmButton != null) confirmButton.interactable = valid;
            int count = 0; if (characters != null) for (int i = 0; i < characters.Length; i++) if (IsCharacterAvailable(i)) count++;
            if (previousCharacterButton != null) previousCharacterButton.interactable = count > 1;
            if (nextCharacterButton != null) nextCharacterButton.interactable = count > 1;
        }
        private void ChangeCharacter(int direction)
        {
            if (characters == null || characters.Length == 0) return;
            for (int i = 1; i <= characters.Length; i++) { int candidate = (characterIndex + direction * i + characters.Length * i) % characters.Length; if (IsCharacterAvailable(candidate)) { characterIndex = candidate; break; } }
            RefreshCharacter();
        }
        private void CharacterBack() { AudioManager.Play(AudioEventId.UiBack); if (startingCourse) ShowCourses(); else ShowMain(); }
        private void ConfirmCharacter()
        {
            if (!IsCharacterAvailable(characterIndex)) return;
            AudioManager.Play(AudioEventId.UiCharacterConfirm);
            PlayerPrefs.SetString(CharacterKey, characters[characterIndex].id); PlayerPrefs.Save();
            if (!startingCourse) { ShowMain(); return; }
            if (loading || courses == null || selectedCourse < 0 || selectedCourse >= courses.Length || courses[selectedCourse] == null || !courses[selectedCourse].Available) { ShowCourses(); return; }
            loading = true; Time.timeScale = 1f; SceneManager.LoadScene(courses[selectedCourse].sceneName);
        }
        public void StartCourse() { ShowCourses(); }
        private void ShowSettings() { Show(settingsPanel, settingsBackButton); }
        public void Quit() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
