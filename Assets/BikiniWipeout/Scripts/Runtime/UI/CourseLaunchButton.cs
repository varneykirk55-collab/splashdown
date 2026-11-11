using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Menu button that starts a specific course scene. Lets the menu offer more than one course.</summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class CourseLaunchButton : MonoBehaviour
    {
        [Tooltip("Scene to load. Must be in Build Settings.")]
        [SerializeField] internal string sceneName = SceneNames.CourseChaosCove;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Launch);
        }

        public void Launch()
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[Splashdown] Scene '" + sceneName + "' is not in Build Settings. Build the course first.", this);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }
}
