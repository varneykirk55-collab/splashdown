using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Shared persistent graphics controls for main menu and pause.</summary>
    public sealed class GraphicsSettingsController : MonoBehaviour
    {
        [SerializeField] internal Button qualityButton, resolutionButton, fullscreenButton;
        [SerializeField] internal Text label;
        private Resolution[] resolutions;
        private int index;
        private static bool applied;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { applied = false; }
        private void Awake()
        {
            if (qualityButton != null) qualityButton.onClick.AddListener(CycleQuality);
            if (resolutionButton != null) resolutionButton.onClick.AddListener(CycleResolution);
            if (fullscreenButton != null) fullscreenButton.onClick.AddListener(ToggleFullscreen);
            ApplySaved();
        }
        private void OnEnable() { resolutions = Screen.resolutions; SyncIndex(); Refresh(); }
        public static void ApplySaved()
        {
            if (applied) return;
            applied = true;
            if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("Splashdown.Quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1));
            int width = PlayerPrefs.GetInt("Splashdown.Width", Screen.width), height = PlayerPrefs.GetInt("Splashdown.Height", Screen.height);
            bool fullscreen = PlayerPrefs.GetInt("Splashdown.Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            if (width > 0 && height > 0) Screen.SetResolution(width, height, fullscreen);
        }
        private void SyncIndex() { if (resolutions != null) for (int i = 0; i < resolutions.Length; i++) if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height) index = i; }
        private void CycleQuality() { if (QualitySettings.names.Length == 0) return; QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length); PlayerPrefs.SetInt("Splashdown.Quality", QualitySettings.GetQualityLevel()); PlayerPrefs.Save(); Refresh(); }
        private void CycleResolution() { if (resolutions == null || resolutions.Length == 0) return; index = (index + 1) % resolutions.Length; var r = resolutions[index]; Screen.SetResolution(r.width, r.height, Screen.fullScreen); PlayerPrefs.SetInt("Splashdown.Width", r.width); PlayerPrefs.SetInt("Splashdown.Height", r.height); PlayerPrefs.Save(); Refresh(); }
        private void ToggleFullscreen() { bool value = !Screen.fullScreen; Screen.fullScreen = value; PlayerPrefs.SetInt("Splashdown.Fullscreen", value ? 1 : 0); PlayerPrefs.Save(); Refresh(); }
        private void Refresh() { if (label != null) label.text = "QUALITY: " + (QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "DEFAULT") + "    " + Screen.width + " × " + Screen.height + "    " + (Screen.fullScreen ? "FULLSCREEN" : "WINDOWED"); }
    }
}
