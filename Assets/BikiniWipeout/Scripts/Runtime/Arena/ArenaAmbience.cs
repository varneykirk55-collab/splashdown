using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Looping arena atmosphere (crowd bed). The clip and base volume come from the central
    /// <see cref="AudioLibrary"/> entry for <see cref="AudioEventId.ArenaAmbience"/>, and the level
    /// follows the sound-effects volume slider. Silent, without errors, if no clip is assigned.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class ArenaAmbience : MonoBehaviour
    {
        [SerializeField] internal AudioEventId audioEvent = AudioEventId.ArenaAmbience;
        [SerializeField, Range(0f, 1f)] internal float volumeScale = 1f;

        private AudioSource source;
        private VolumeSettings volumes;
        private float baseVolume = 1f;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
        }

        private void Start()
        {
            AudioManager manager = AudioManager.Instance;
            AudioLibrary.SfxEntry entry = manager != null && manager.Library != null ? manager.Library.FindSfx(audioEvent) : null;
            if (entry == null || !entry.HasClip)
            {
                return;
            }

            foreach (AudioClip clip in entry.clips)
            {
                if (clip != null)
                {
                    source.clip = clip;
                    break;
                }
            }

            baseVolume = entry.volume;
            volumes = manager.Volumes;
            if (volumes != null)
            {
                volumes.Changed += ApplyVolume;
            }

            ApplyVolume();
            source.Play();
        }

        private void OnDestroy()
        {
            if (volumes != null)
            {
                volumes.Changed -= ApplyVolume;
            }
        }

        private void ApplyVolume()
        {
            source.volume = baseVolume * volumeScale * (volumes != null ? volumes.SfxGain : 1f);
        }
    }
}
