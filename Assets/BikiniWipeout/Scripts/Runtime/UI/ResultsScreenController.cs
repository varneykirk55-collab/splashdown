using BikiniWipeout.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Completion screen shown when the finish line is crossed: time, best time, counters, restart / menu.</summary>
    [DisallowMultipleComponent]
    public sealed class ResultsScreenController : MonoBehaviour
    {
        [SerializeField] internal CourseManager course;
        [SerializeField] internal GameObject panel;
        [SerializeField] internal Text timeText;
        [SerializeField] internal Text bestText;
        [SerializeField] internal Text statsText;
        [SerializeField] internal Button restartButton;
        [SerializeField] internal Button mainMenuButton;
        [SerializeField] internal Button courseSelectButton;

        public bool IsShowing => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
            if (courseSelectButton != null) courseSelectButton.onClick.AddListener(OnCourseSelect);
        }

        private void Start()
        {
            if (course == null)
            {
                course = CourseManager.Current;
            }

            if (course != null)
            {
                course.Finished += OnFinished;
            }
        }

        private void OnDestroy()
        {
            if (course != null)
            {
                course.Finished -= OnFinished;
            }
        }

        private void OnFinished(CourseResult result)
        {
            AudioManager.Play(AudioEventId.UiCourseComplete);
            if (timeText != null)
            {
                timeText.text = "TIME  " + TimeFormatter.Format(result.TimeSeconds);
            }

            if (bestText != null)
            {
                bestText.text = result.IsNewBest
                    ? "NEW BEST TIME!"
                    : "BEST  " + TimeFormatter.Format(result.BestSeconds);
            }

            if (statsText != null)
            {
                statsText.text = "CHECKPOINTS  " + (course.Checkpoints != null ? course.Checkpoints.CurrentIndex : 0) + " / " + (course.Checkpoints != null ? Mathf.Max(0, course.Checkpoints.Count - 1) : 0) + "    WIPEOUTS  " + result.Wipeouts + "    FALLS  " + result.Falls;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            if (restartButton != null)
            {
                restartButton.Select();
            }
        }

        private void OnRestart()
        {
            if (course != null) course.RestartCourse();
        }

        private void OnCourseSelect()
        {
            MainMenuController.OpenCourseSelectOnArrival();
            if (course != null) course.ReturnToMainMenu();
        }

        private void OnMainMenu()
        {
            if (course != null) course.ReturnToMainMenu();
        }
    }
}
