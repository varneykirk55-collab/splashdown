using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>
    /// Fade-to-black scene transition used when launching a course. Created on first use and kept across scenes
    /// (a single overlay, no audio and no other global state); fades back in after every scene load.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private static ScreenFader instance;
        private CanvasGroup group;
        private Coroutine running;

        private static ScreenFader Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("ScreenFader");
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
                var image = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
                image.transform.SetParent(go.transform, false);
                var rect = image.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                image.color = Color.black;
                instance = go.AddComponent<ScreenFader>();
                instance.group = go.AddComponent<CanvasGroup>();
                instance.group.alpha = 0f;
                instance.group.blocksRaycasts = false;
                SceneManager.sceneLoaded += instance.OnSceneLoaded;
                return instance;
            }
        }

        /// <summary>Fades out, loads <paramref name="sceneName"/>, then fades the new scene in.</summary>
        public static void Load(string sceneName)
        {
            ScreenFader fader = Instance;
            if (fader.running != null) fader.StopCoroutine(fader.running);
            fader.running = fader.StartCoroutine(fader.FadeOutThenLoad(sceneName));
        }

        private IEnumerator FadeOutThenLoad(string sceneName)
        {
            group.blocksRaycasts = true;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / 0.3f;
                yield return null;
            }

            group.alpha = 1f;
            SceneManager.LoadScene(sceneName);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / 0.5f;
                yield return null;
            }

            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }

}
