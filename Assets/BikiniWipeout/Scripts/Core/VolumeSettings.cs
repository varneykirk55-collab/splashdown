using System;

namespace BikiniWipeout.Core
{
    /// <summary>Minimal persistence abstraction so settings logic can be tested without PlayerPrefs.</summary>
    public interface IKeyValueStore
    {
        float GetFloat(string key, float defaultValue);
        void SetFloat(string key, float value);
        void Save();
    }

    /// <summary>Master / music / sound-effect volume model (linear 0..1) with persistence.</summary>
    public sealed class VolumeSettings
    {
        public const string MasterKey = "bw.audio.master";
        public const string MusicKey = "bw.audio.music";
        public const string SfxKey = "bw.audio.sfx";
        public const string AnnouncerKey = "bw.audio.announcer";

        public const float DefaultMaster = 1f;
        public const float DefaultMusic = 0.6f;
        public const float DefaultSfx = 0.9f;
        public const float DefaultAnnouncer = 1f;

        private float master = DefaultMaster;
        private float music = DefaultMusic;
        private float sfx = DefaultSfx;
        private float announcer = DefaultAnnouncer;

        public event Action Changed;

        public float Master
        {
            get => master;
            set => Set(ref master, value);
        }

        public float Music
        {
            get => music;
            set => Set(ref music, value);
        }

        public float Sfx
        {
            get => sfx;
            set => Set(ref sfx, value);
        }

        public float Announcer
        {
            get => announcer;
            set => Set(ref announcer, value);
        }

        /// <summary>
        /// Per-category gain. Master is applied separately on the listener, so these are not
        /// pre-multiplied by it.
        /// </summary>
        public float MusicGain => music;

        public float SfxGain => sfx;

        public float AnnouncerGain => announcer;

        /// <summary>Final audible level of each category, for display and tests.</summary>
        public float EffectiveMusic => master * music;

        public float EffectiveSfx => master * sfx;

        public float EffectiveAnnouncer => master * announcer;

        public void Load(IKeyValueStore store)
        {
            if (store == null)
            {
                return;
            }

            master = Clamp01(store.GetFloat(MasterKey, DefaultMaster));
            music = Clamp01(store.GetFloat(MusicKey, DefaultMusic));
            sfx = Clamp01(store.GetFloat(SfxKey, DefaultSfx));
            announcer = Clamp01(store.GetFloat(AnnouncerKey, DefaultAnnouncer));
            Changed?.Invoke();
        }

        public void Save(IKeyValueStore store)
        {
            if (store == null)
            {
                return;
            }

            store.SetFloat(MasterKey, master);
            store.SetFloat(MusicKey, music);
            store.SetFloat(SfxKey, sfx);
            store.SetFloat(AnnouncerKey, announcer);
            store.Save();
        }

        /// <summary>Linear gain to decibels, for a future AudioMixer hookup. Silence maps to -80 dB.</summary>
        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f)
            {
                return -80f;
            }

            return (float)(20.0 * Math.Log10(linear));
        }

        private void Set(ref float field, float value)
        {
            float clamped = Clamp01(value);
            if (Math.Abs(clamped - field) < 0.00001f)
            {
                return;
            }

            field = clamped;
            Changed?.Invoke();
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value))
            {
                return 0f;
            }

            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }
    }
}
