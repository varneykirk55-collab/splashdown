using System;
using System.Collections.Generic;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Maps audio events and music tracks to clips. Replace the placeholder clips here; no code changes
    /// are needed. Events without clips stay silent and are reported once at startup.
    /// </summary>
    [CreateAssetMenu(menuName = "Bikini Wipeout/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class SfxEntry
        {
            public AudioEventId id;

            [Tooltip("One is picked at random each time.")]
            public AudioClip[] clips = new AudioClip[0];

            [Range(0f, 1f)] public float volume = 1f;
            public Vector2 pitchRange = new Vector2(0.96f, 1.04f);

            [Tooltip("0 = plain 2D, 1 = fully positioned in the world.")]
            [Range(0f, 1f)] public float spatialBlend;

            [Tooltip("Minimum seconds between two plays of this event.")]
            [Min(0f)] public float minInterval;

            public bool HasClip
            {
                get
                {
                    if (clips == null)
                    {
                        return false;
                    }

                    for (int i = 0; i < clips.Length; i++)
                    {
                        if (clips[i] != null)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        [Serializable]
        public sealed class MusicEntry
        {
            public MusicTrackId id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            public bool loop = true;
        }

        public List<SfxEntry> sfx = new List<SfxEntry>();
        public List<MusicEntry> music = new List<MusicEntry>();

        public SfxEntry FindSfx(AudioEventId id)
        {
            for (int i = 0; i < sfx.Count; i++)
            {
                if (sfx[i] != null && sfx[i].id == id)
                {
                    return sfx[i];
                }
            }

            return null;
        }

        public MusicEntry FindMusic(MusicTrackId id)
        {
            for (int i = 0; i < music.Count; i++)
            {
                if (music[i] != null && music[i].id == id)
                {
                    return music[i];
                }
            }

            return null;
        }

        /// <summary>Names of all events and tracks that have no clip assigned.</summary>
        public List<string> GetMissingClipReport()
        {
            var missing = new List<string>();
            foreach (AudioEventId id in Enum.GetValues(typeof(AudioEventId)))
            {
                if (id == AudioEventId.None)
                {
                    continue;
                }

                SfxEntry entry = FindSfx(id);
                if (entry == null || !entry.HasClip)
                {
                    missing.Add("SFX: " + id);
                }
            }

            foreach (MusicTrackId id in Enum.GetValues(typeof(MusicTrackId)))
            {
                if (id == MusicTrackId.None)
                {
                    continue;
                }

                MusicEntry entry = FindMusic(id);
                if (entry == null || entry.clip == null)
                {
                    missing.Add("Music: " + id);
                }
            }

            return missing;
        }
    }
}
