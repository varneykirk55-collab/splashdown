using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace BikiniWipeout
{
    /// <summary>Pause overlay: resume, restart, return to menu. Lives on an always-active object and toggles <see cref="panel"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] internal CourseManager course;
        [SerializeField] internal GameObject panel;
        [SerializeField] internal Button resumeButton;
        [SerializeField] internal Button restartButton;
        [SerializeField] internal Button mainMenuButton;
        [SerializeField] internal Button settingsButton, settingsBackButton;
        [SerializeField] internal GameObject settingsPanel, pauseContent;

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        private void Start()
        {
            if (course == null)
            {
                course = CourseManager.Current;
            }

            if (course != null)
            {
                course.Session.PauseChanged += OnPauseChanged;
            }
        }

        private void Update()
        {
            if (course != null && course.Session.IsPaused && settingsPanel != null && settingsPanel.activeSelf &&
                Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) TryCloseSettings();
        }

        private void OnDestroy()
        {
            if (course != null && course.Session != null)
            {
                course.Session.PauseChanged -= OnPauseChanged;
            }
        }

        private void OnPauseChanged(bool paused)
        {
            if (panel == null)
            {
                return;
            }

            if (!paused && settingsPanel != null) settingsPanel.SetActive(false);
            if (pauseContent != null) pauseContent.SetActive(true);
            panel.SetActive(paused);
            if (paused && resumeButton != null)
            {
                resumeButton.Select();
            }
        }

        private void OpenSettings()
        {
            if (settingsPanel == null) return;
            if (pauseContent != null) pauseContent.SetActive(false);
            settingsPanel.SetActive(true);
            AudioManager.Play(AudioEventId.UiTransition);
            if (settingsBackButton != null) settingsBackButton.Select();
        }
        public bool TryCloseSettings()
        {
            if (settingsPanel == null || !settingsPanel.activeSelf) return false;
            CloseSettings();
            return true;
        }
        private void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pauseContent != null) pauseContent.SetActive(true);
            AudioManager.Play(AudioEventId.UiBack);
            if (settingsButton != null) settingsButton.Select();
        }

        private void OnResume()
        {
            if (course != null) course.SetPaused(false);
        }

        private void OnRestart()
        {
            if (course != null) course.RestartCourse();
        }

        private void OnMainMenu()
        {
            if (course != null) course.ReturnToMainMenu();
        }
    }
}
