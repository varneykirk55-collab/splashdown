using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>
    /// Gives the in-course UI (HUD, pause, settings, results) the Splashdown broadcast look at runtime. The course
    /// scenes are not edited: this attaches itself to any canvas that holds a <see cref="HudController"/> and only
    /// restyles and adds decoration. Gameplay data and logic stay with the existing controllers.
    /// </summary>
    public static class GameplayUiSkin
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Attach(); }

        private static void Attach()
        {
            foreach (HudController hud in Object.FindObjectsByType<HudController>())
            {
                if (hud.GetComponent<HudSkin>() == null) hud.gameObject.AddComponent<HudSkin>();
            }
        }
    }

}
