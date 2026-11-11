using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BikiniWipeout
{
    [Serializable]
    public sealed class CourseEntry
    {
        public string title;
        public string sceneName;
        public bool ready;
        [TextArea] public string description;
        public Sprite thumbnail;
        public bool Available => ready && !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    [Serializable]
    public sealed class CharacterEntry
    {
        public string id;
        public string displayName;
        public bool ready;
        /// <summary>Appended to a course scene name to load this character's copy of the course (empty = the course scene itself).</summary>
        public string sceneSuffix;
        public Sprite portrait;
    }

    /// <summary>
    /// The one-screen hub: navigation column, character selection, course selection and START COURSE.
    /// Selections are remembered between sessions; the chosen character decides which course scene is loaded
    /// (see <see cref="CharacterEntry.sceneSuffix"/>). Options opens as an overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] internal Button playButton, quitButton, characterButton, courseButton, settingsButton, startButton;
        [SerializeField] internal Text bestTimeText;
        // Retained for compatibility with previously generated scenes and the Course 1 builder.
        [SerializeField] internal string courseSceneName = SceneNames.CourseFoundation1;
        [SerializeField] internal string courseId = "foundation1";
        [SerializeField] internal MusicTrackId music = MusicTrackId.MainMenu;
        [SerializeField] internal GameObject settingsPanel;
        [SerializeField] internal Button settingsBackButton;
        [SerializeField] internal Button previousCharacterButton, nextCharacterButton, previousCoursePageButton, nextCoursePageButton;
        [SerializeField] internal MenuCharacterCard[] characterCards;
        [SerializeField] internal MenuCourseRow[] courseRows;
        [SerializeField] internal Text summaryText, statusText;
        [SerializeField] internal CharacterEntry[] characters = {
            new CharacterEntry { id = "existing-player", displayName = "Original", ready = true },
            new CharacterEntry { id = "charD", displayName = "Stars & Stripes", ready = true, sceneSuffix = "_StarsStripes" }
        };
        [SerializeField] internal CourseEntry[] courses = {
            new CourseEntry { title = "The Splashdown Gauntlet", sceneName = SceneNames.CourseGauntlet, ready = true,
                description = "A U-shaped run through the stadium" },
            new CourseEntry { title = "Chaos Cove", sceneName = SceneNames.CourseChaosCove, ready = true,
                description = "Bounce pads, water jets and big balls" }
        };

        private const string CharacterKey = "Splashdown.Character";
        private const string CourseKey = "Splashdown.Course";
        private bool loading;
        private int selectedCourse = -1;
        private int characterIndex = -1;
        private int characterPage, coursePage;
        private static bool openCourses;

        public static void OpenCourseSelectOnArrival() { openCourses = true; }

        private void Awake()
        {
            Wire(playButton, Launch);
            Wire(startButton, Launch);
            Wire(quitButton, Quit);
            Wire(characterButton, FocusCharacters);
            Wire(courseButton, FocusCourses);
            Wire(settingsButton, ShowSettings);
            Wire(settingsBackButton, HideSettings);
            Wire(previousCharacterButton, () => CycleCharacter(-1));
            Wire(nextCharacterButton, () => CycleCharacter(1));
            Wire(previousCoursePageButton, () => ChangeCoursePage(-1));
            Wire(nextCoursePageButton, () => ChangeCoursePage(1));
            if (characterCards != null)
                for (int i = 0; i < characterCards.Length; i++) { int slot = i; Wire(characterCards[i].button, () => ClickCharacter(slot)); }
            if (courseRows != null)
                for (int i = 0; i < courseRows.Length; i++) { int slot = i; Wire(courseRows[i].button, () => ClickCourse(slot)); }
            UiTheme.ApplyFonts(gameObject);
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioManager.PlayMusic(music);
            GraphicsSettingsController.ApplySaved();
            if (settingsPanel != null) settingsPanel.SetActive(false);

            string saved = PlayerPrefs.GetString(CharacterKey, "existing-player");
            characterIndex = -1;
            if (characters != null) for (int i = 0; i < characters.Length; i++)
            {
                if (!IsCharacterAvailable(i)) continue;
                if (characterIndex < 0) characterIndex = i;
                if (characters[i].id == saved) { characterIndex = i; break; }
            }

            string savedCourse = PlayerPrefs.GetString(CourseKey, string.Empty);
            selectedCourse = -1;
            if (courses != null) for (int i = 0; i < courses.Length; i++)
            {
                if (courses[i] == null || !courses[i].Available) continue;
                if (selectedCourse < 0) selectedCourse = i;
                if (courses[i].sceneName == savedCourse) { selectedCourse = i; break; }
            }

            RefreshAll();
            if (openCourses) { openCourses = false; FocusCourses(); }
            else Focus(playButton != null ? playButton.gameObject : null);
        }

        private void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                        (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back && settingsPanel != null && settingsPanel.activeSelf) HideSettings();
        }

        // ------------------------------------------------------------------ availability / resolution

        private bool IsCharacterAvailable(int index)
        {
            return characters != null && index >= 0 && index < characters.Length && characters[index] != null && characters[index].ready &&
                   (string.IsNullOrEmpty(characters[index].sceneSuffix) || AnyVariantLoadable(characters[index].sceneSuffix));
        }

        private bool AnyVariantLoadable(string suffix)
        {
            if (courses == null) return false;
            foreach (CourseEntry c in courses)
                if (c != null && c.ready && !string.IsNullOrEmpty(c.sceneName) && Application.CanStreamedLevelBeLoaded(c.sceneName + suffix)) return true;
            return false;
        }

        /// <summary>Scene to load for a course with the selected character; falls back to the base course scene.</summary>
        private string ResolveCourseScene(CourseEntry course)
        {
            CharacterEntry c = characters != null && characterIndex >= 0 && characterIndex < characters.Length ? characters[characterIndex] : null;
            if (c != null && !string.IsNullOrEmpty(c.sceneSuffix) && Application.CanStreamedLevelBeLoaded(course.sceneName + c.sceneSuffix)) return course.sceneName + c.sceneSuffix;
            return course.sceneName;
        }

        // ------------------------------------------------------------------ refresh

        private int[] AvailableCharacters()
        {
            var list = new System.Collections.Generic.List<int>();
            if (characters != null) for (int i = 0; i < characters.Length; i++) if (IsCharacterAvailable(i)) list.Add(i);
            return list.ToArray();
        }

        private void RefreshAll()
        {
            RefreshCharacters();
            RefreshCourses();
            RefreshSummary();
        }

        private void RefreshCharacters()
        {
            int[] list = AvailableCharacters();
            int slots = characterCards != null ? characterCards.Length : 0;
            int position = Array.IndexOf(list, characterIndex);
            if (position >= 0 && slots > 0) characterPage = position / slots;
            for (int i = 0; i < slots; i++)
            {
                MenuCharacterCard card = characterCards[i];
                int at = characterPage * slots + i;
                bool exists = at < list.Length;
                card.gameObject.SetActive(exists);
                if (!exists) continue;
                CharacterEntry entry = characters[list[at]];
                if (card.nameText != null) card.nameText.text = entry.displayName;
                if (card.portrait != null) { card.portrait.sprite = entry.portrait; card.portrait.enabled = entry.portrait != null; }
                card.SetState(list[at] == characterIndex);
            }

            bool cycle = list.Length > 1;
            if (previousCharacterButton != null) previousCharacterButton.interactable = cycle;
            if (nextCharacterButton != null) nextCharacterButton.interactable = cycle;
        }

        private void RefreshCourses()
        {
            int slots = courseRows != null ? courseRows.Length : 0;
            int count = courses != null ? courses.Length : 0;
            int visible = Mathf.Clamp(count - coursePage * slots, 0, slots);
            for (int i = 0; i < slots; i++)
            {
                MenuCourseRow row = courseRows[i];
                int at = coursePage * slots + i;
                // Keep the visible rows vertically centred in the panel when there are fewer courses than slots.
                var rowRect = (RectTransform)row.transform;
                rowRect.anchoredPosition = new Vector2(rowRect.anchoredPosition.x, -10f + ((visible - 1) * 0.5f - i) * 144f);
                bool exists = at < count && courses[at] != null;
                row.gameObject.SetActive(exists);
                if (!exists) continue;
                CourseEntry entry = courses[at];
                bool available = entry.Available;
                if (row.numberText != null) row.numberText.text = "Course " + (at + 1);
                if (row.titleText != null) row.titleText.text = entry.title;
                if (row.subtitleText != null) row.subtitleText.text = available ? entry.description : (entry.ready ? "Unavailable" : "Coming soon");
                if (row.thumbnail != null) { row.thumbnail.sprite = entry.thumbnail; row.thumbnail.enabled = entry.thumbnail != null; }
                if (row.button != null) row.button.interactable = available;
                row.SetState(at == selectedCourse, available);
            }

            bool paged = slots > 0 && count > slots;
            if (previousCoursePageButton != null) { previousCoursePageButton.gameObject.SetActive(paged); previousCoursePageButton.interactable = coursePage > 0; }
            if (nextCoursePageButton != null) { nextCoursePageButton.gameObject.SetActive(paged); nextCoursePageButton.interactable = (coursePage + 1) * slots < count; }
        }

        private void RefreshSummary()
        {
            bool ready = characterIndex >= 0 && selectedCourse >= 0;
            if (startButton != null) startButton.interactable = ready;
            if (playButton != null) playButton.interactable = ready;
            if (summaryText != null)
            {
                summaryText.text = ready
                    ? characters[characterIndex].displayName.ToUpperInvariant() + "   •   " + courses[selectedCourse].title.ToUpperInvariant()
                    : "NO PLAYABLE COURSE AVAILABLE";
            }
        }

        // ------------------------------------------------------------------ selection

        private void ClickCharacter(int slot)
        {
            int[] list = AvailableCharacters();
            int at = characterPage * (characterCards != null ? characterCards.Length : 0) + slot;
            if (at < 0 || at >= list.Length) return;
            SelectCharacter(list[at]);
        }

        private void SelectCharacter(int index)
        {
            if (!IsCharacterAvailable(index)) return;
            bool changed = index != characterIndex;
            characterIndex = index;
            PlayerPrefs.SetString(CharacterKey, characters[index].id); PlayerPrefs.Save();
            if (changed) AudioManager.Play(AudioEventId.UiCharacterConfirm);
            RefreshCharacters(); RefreshSummary();
        }

        private void CycleCharacter(int direction)
        {
            int[] list = AvailableCharacters();
            if (list.Length < 2) return;
            int position = Mathf.Max(0, Array.IndexOf(list, characterIndex));
            SelectCharacter(list[(position + direction + list.Length) % list.Length]);
        }

        private void ClickCourse(int slot)
        {
            int at = coursePage * (courseRows != null ? courseRows.Length : 0) + slot;
            if (courses == null || at < 0 || at >= courses.Length || courses[at] == null || !courses[at].Available) return;
            if (at != selectedCourse) AudioManager.Play(AudioEventId.UiCourseSelect);
            selectedCourse = at;
            PlayerPrefs.SetString(CourseKey, courses[at].sceneName); PlayerPrefs.Save();
            RefreshCourses(); RefreshSummary();
        }

        private void ChangeCoursePage(int delta)
        {
            coursePage = Mathf.Max(0, coursePage + delta);
            RefreshCourses();
        }

        // ------------------------------------------------------------------ navigation

        private static void Focus(GameObject target)
        {
            if (target != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }

        private void FocusCharacters()
        {
            AudioManager.Play(AudioEventId.UiTransition);
            if (characterCards == null) return;
            foreach (MenuCharacterCard card in characterCards)
                if (card != null && card.gameObject.activeSelf && card.button != null && (card.check == null || card.check.gameObject.activeSelf)) { Focus(card.button.gameObject); return; }
            if (characterCards.Length > 0 && characterCards[0] != null && characterCards[0].button != null) Focus(characterCards[0].button.gameObject);
        }

        private void FocusCourses()
        {
            AudioManager.Play(AudioEventId.UiTransition);
            if (courseRows == null) return;
            int slots = courseRows.Length;
            int slot = selectedCourse >= 0 ? selectedCourse - coursePage * slots : 0;
            if (slot < 0 || slot >= slots) slot = 0;
            if (courseRows[slot] != null && courseRows[slot].button != null && courseRows[slot].button.interactable) Focus(courseRows[slot].button.gameObject);
        }

        private void ShowSettings()
        {
            AudioManager.Play(AudioEventId.UiTransition);
            if (settingsPanel == null) return;
            settingsPanel.SetActive(true);
            if (settingsBackButton != null) Focus(settingsBackButton.gameObject);
        }

        private void HideSettings()
        {
            AudioManager.Play(AudioEventId.UiBack);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            Focus(settingsButton != null ? settingsButton.gameObject : null);
        }

        /// <summary>Starts the selected course with the selected character.</summary>
        public void Launch()
        {
            if (loading || characterIndex < 0 || selectedCourse < 0 || courses == null || selectedCourse >= courses.Length) return;
            CourseEntry course = courses[selectedCourse];
            if (course == null || !course.Available) return;
            loading = true;
            Time.timeScale = 1f;
            AudioManager.Play(AudioEventId.UiTransition);
            PlayerPrefs.SetString(CharacterKey, characters[characterIndex].id);
            PlayerPrefs.SetString(CourseKey, course.sceneName);
            PlayerPrefs.Save();
            ScreenFader.Load(ResolveCourseScene(course));
        }

        public void StartCourse() { Launch(); }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
