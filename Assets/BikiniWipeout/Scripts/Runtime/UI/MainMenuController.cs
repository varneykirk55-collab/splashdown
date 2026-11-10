using BikiniWipeout.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Main menu: start the course, show the best time, quit.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] internal Button playButton;
        [SerializeField] internal Button quitButton;
        [SerializeField] internal Text bestTimeText;
        [SerializeField] internal string courseSceneName = SceneNames.CourseFoundation1;
        [SerializeField] internal string courseId = "foundation1";
        [SerializeField] internal MusicTrackId music = MusicTrackId.MainMenu;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(StartCourse);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioManager.PlayMusic(music);

            if (bestTimeText != null)
            {
                var best = new BestTimeRecord(new PlayerPrefsStore(), courseId);
                bestTimeText.text = best.HasRecord ? "BEST TIME  " + TimeFormatter.Format(best.BestSeconds) : "NO RUN RECORDED YET";
            }

            if (playButton != null)
            {
                playButton.Select();
            }
        }

        public void StartCourse()
        {
            SceneManager.LoadScene(courseSceneName);
        }

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
