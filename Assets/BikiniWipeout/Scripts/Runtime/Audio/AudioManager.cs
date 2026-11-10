using System.Collections.Generic;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Central audio system. Gameplay calls the static <see cref="Play(AudioEventId)"/> /
    /// <see cref="PlayMusic"/> hooks; the manager looks the event up in the <see cref="AudioLibrary"/>,
    /// applies master / music / sound-effect volume, and plays it on a pooled AudioSource.
    ///
    /// The hooks always work: if no manager or no clip exists, the request is counted and ignored,
    /// and missing clips are reported once. Final sounds can be dropped into the library later.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] internal AudioLibrary library;

        [Tooltip("Maximum number of sound effects that can overlap.")]
        [SerializeField, Range(1, 32)] internal int sfxVoices = 12;

        [Tooltip("Keep one manager alive across scene loads so music continues seamlessly.")]
        [SerializeField] internal bool persistAcrossScenes = true;

        private static readonly Dictionary<AudioEventId, int> RequestCounts = new Dictionary<AudioEventId, int>();
        private static bool warnedNoManager;

        private readonly Dictionary<AudioEventId, float> lastPlayTime = new Dictionary<AudioEventId, float>();
        private readonly HashSet<AudioEventId> warnedMissing = new HashSet<AudioEventId>();
        private readonly List<string> missingClips = new List<string>();
        private AudioSource[] sfxSources;
        private AudioSource musicSource;
        private AudioLibrary.MusicEntry currentMusic;
        private IKeyValueStore store;
        private int nextVoice;

        public static AudioManager Instance { get; private set; }

        /// <summary>Master / music / sound-effect volume. Changes apply immediately; call <see cref="SaveVolumes"/> to persist.</summary>
        public VolumeSettings Volumes { get; private set; }

        public bool IsInitialized { get; private set; }
        public AudioLibrary Library => library;
        public MusicTrackId CurrentMusicTrack => currentMusic != null ? currentMusic.id : MusicTrackId.None;

        /// <summary>Events and tracks that had no clip when the manager started.</summary>
        public IReadOnlyList<string> MissingClips => missingClips;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            warnedNoManager = false;
            RequestCounts.Clear();
        }

        /// <summary>Plays a non-positional sound effect.</summary>
        public static void Play(AudioEventId id)
        {
            Request(id, Vector3.zero, false, 1f);
        }

        /// <summary>Plays a sound effect at a world position.</summary>
        public static void Play(AudioEventId id, Vector3 position, float volumeScale = 1f)
        {
            Request(id, position, true, volumeScale);
        }

        public static void PlayMusic(MusicTrackId track)
        {
            if (Instance != null)
            {
                Instance.PlayMusicInternal(track);
            }
            else
            {
                WarnNoManager();
            }
        }

        public static void StopMusic()
        {
            if (Instance != null)
            {
                Instance.PlayMusicInternal(MusicTrackId.None);
            }
        }

        /// <summary>How many times an event has been requested since startup. Lets tests verify hooks fire even without clips.</summary>
        public static int GetRequestCount(AudioEventId id)
        {
            return RequestCounts.TryGetValue(id, out int count) ? count : 0;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A manager from an earlier scene is already running.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            CreateSources();

            store = new PlayerPrefsStore();
            Volumes = new VolumeSettings();
            Volumes.Load(store);
            Volumes.Changed += ApplyVolumes;
            ApplyVolumes();

            ReportMissingClips();
            IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (Volumes != null)
            {
                Volumes.Changed -= ApplyVolumes;
            }
        }

        public void SaveVolumes()
        {
            if (Volumes != null)
            {
                Volumes.Save(store);
            }
        }

        private static void Request(AudioEventId id, Vector3 position, bool positional, float volumeScale)
        {
            if (id == AudioEventId.None)
            {
                return;
            }

            RequestCounts.TryGetValue(id, out int count);
            RequestCounts[id] = count + 1;

            if (Instance == null)
            {
                WarnNoManager();
                return;
            }

            Instance.PlayInternal(id, position, positional, volumeScale);
        }

        private static void WarnNoManager()
        {
            if (!warnedNoManager)
            {
                warnedNoManager = true;
                Debug.LogWarning("[AudioManager] Audio was requested but no AudioManager exists in the scene. Requests are ignored.");
            }
        }

        private void PlayInternal(AudioEventId id, Vector3 position, bool positional, float volumeScale)
        {
            AudioLibrary.SfxEntry entry = library != null ? library.FindSfx(id) : null;
            if (entry == null || !entry.HasClip)
            {
                if (warnedMissing.Add(id))
                {
                    Debug.LogWarning($"[AudioManager] No clip assigned for audio event '{id}'. The hook is active but silent.", this);
                }

                return;
            }

            // Unscaled time so UI sounds still obey their interval while the game is paused.
            float now = Time.unscaledTime;
            if (entry.minInterval > 0f && lastPlayTime.TryGetValue(id, out float last) && now - last < entry.minInterval)
            {
                return;
            }

            lastPlayTime[id] = now;

            AudioClip clip = PickClip(entry);
            if (clip == null)
            {
                return;
            }

            AudioSource source = NextVoice();
            source.transform.position = positional ? position : transform.position;
            source.spatialBlend = positional ? entry.spatialBlend : 0f;
            source.pitch = Random.Range(Mathf.Min(entry.pitchRange.x, entry.pitchRange.y), Mathf.Max(entry.pitchRange.x, entry.pitchRange.y));
            source.clip = clip;
            source.volume = Mathf.Clamp01(entry.volume * volumeScale) * Volumes.SfxGain;
            source.Play();
        }

        private void PlayMusicInternal(MusicTrackId track)
        {
            if (track == MusicTrackId.None)
            {
                musicSource.Stop();
                musicSource.clip = null;
                currentMusic = null;
                return;
            }

            AudioLibrary.MusicEntry entry = library != null ? library.FindMusic(track) : null;
            if (entry == null || entry.clip == null)
            {
                // Missing music was already reported at startup; stop whatever belonged to the previous scene.
                musicSource.Stop();
                musicSource.clip = null;
                currentMusic = null;
                return;
            }

            if (currentMusic == entry && musicSource.isPlaying)
            {
                return;
            }

            currentMusic = entry;
            musicSource.clip = entry.clip;
            musicSource.loop = entry.loop;
            ApplyVolumes();
            musicSource.Play();
        }

        private void ApplyVolumes()
        {
            // Master goes through the listener so it also covers any AudioSource outside this manager.
            AudioListener.volume = Volumes.Master;
            if (musicSource != null)
            {
                musicSource.volume = (currentMusic != null ? currentMusic.volume : 1f) * Volumes.MusicGain;
            }
        }

        private void CreateSources()
        {
            var musicObject = new GameObject("Music");
            musicObject.transform.SetParent(transform, false);
            musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            // Music and UI feedback keep playing while the game is paused.
            musicSource.ignoreListenerPause = true;

            sfxSources = new AudioSource[Mathf.Max(1, sfxVoices)];
            for (int i = 0; i < sfxSources.Length; i++)
            {
                var voice = new GameObject("SfxVoice" + i);
                voice.transform.SetParent(transform, false);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4f;
                source.maxDistance = 60f;
                source.dopplerLevel = 0f;
                sfxSources[i] = source;
            }
        }

        private AudioSource NextVoice()
        {
            // Prefer an idle voice; otherwise steal round-robin.
            for (int i = 0; i < sfxSources.Length; i++)
            {
                int index = (nextVoice + i) % sfxSources.Length;
                if (!sfxSources[index].isPlaying)
                {
                    nextVoice = (index + 1) % sfxSources.Length;
                    return sfxSources[index];
                }
            }

            AudioSource stolen = sfxSources[nextVoice];
            nextVoice = (nextVoice + 1) % sfxSources.Length;
            return stolen;
        }

        private static AudioClip PickClip(AudioLibrary.SfxEntry entry)
        {
            int start = Random.Range(0, entry.clips.Length);
            for (int i = 0; i < entry.clips.Length; i++)
            {
                AudioClip clip = entry.clips[(start + i) % entry.clips.Length];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private void ReportMissingClips()
        {
            missingClips.Clear();
            if (library == null)
            {
                missingClips.Add("AudioLibrary asset (none assigned)");
                Debug.LogWarning("[AudioManager] No AudioLibrary assigned. All audio hooks are active but silent.", this);
                return;
            }

            missingClips.AddRange(library.GetMissingClipReport());
            if (missingClips.Count > 0)
            {
                Debug.LogWarning("[AudioManager] Audio events without clips: " + string.Join(", ", missingClips), this);
            }
        }
    }
}
