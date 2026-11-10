using BikiniWipeout.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Binds three sliders (0..1) to master / music / sound-effect volume and saves changes.</summary>
    [DisallowMultipleComponent]
    public sealed class VolumeSliders : MonoBehaviour
    {
        [SerializeField] internal Slider masterSlider;
        [SerializeField] internal Slider musicSlider;
        [SerializeField] internal Slider sfxSlider;

        private bool listening;

        private void OnEnable()
        {
            VolumeSettings volumes = AudioManager.Instance != null ? AudioManager.Instance.Volumes : null;
            if (volumes != null)
            {
                SetWithoutNotify(masterSlider, volumes.Master);
                SetWithoutNotify(musicSlider, volumes.Music);
                SetWithoutNotify(sfxSlider, volumes.Sfx);
            }

            if (!listening)
            {
                if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
                if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
                if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
                listening = true;
            }
        }

        private void OnDisable()
        {
            if (listening)
            {
                if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
                if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
                if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
                listening = false;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SaveVolumes();
            }
        }

        private void OnMasterChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Volumes.Master = value;
        }

        private void OnMusicChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Volumes.Music = value;
        }

        private void OnSfxChanged(float value)
        {
            if (AudioManager.Instance == null)
            {
                return;
            }

            AudioManager.Instance.Volumes.Sfx = value;
            // Audible feedback so the level can be judged while dragging.
            AudioManager.Play(AudioEventId.UiHover);
        }

        private static void SetWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.SetValueWithoutNotify(value);
            }
        }
    }
}
