using UnityEngine;
using UnityEngine.UI;

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

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

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

            panel.SetActive(paused);
            if (paused && resumeButton != null)
            {
                resumeButton.Select();
            }
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
