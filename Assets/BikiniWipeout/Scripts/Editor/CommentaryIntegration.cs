using System;
using System.Collections.Generic;
using BikiniWipeout.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Wires the recorded announcer lines and the soundtrack into the game. Idempotent: re-running updates the
    /// importer settings, the CommentaryLibrary, the AudioLibrary music entries, the AudioManager prefab and
    /// adds the announcer slider to the volume panels that lack one. Source recordings on the Desktop are never touched.
    /// </summary>
    public static class CommentaryIntegration
    {
        public const string CommentaryFolder = "Assets/BikiniWipeout/Audio/Commentary";
        public const string MusicFolder = "Assets/BikiniWipeout/Audio/Music";
        public const string CommentaryLibraryPath = "Assets/BikiniWipeout/Settings/CommentaryLibrary.asset";

        private sealed class Pair
        {
            public string Id;
            public string SetupClip, ResponseClip;
            public CommentarySpeaker SetupSpeaker, ResponseSpeaker;
            public string SetupText, ResponseText;
            public CommentaryTopic[] Topics;
            public float Weight = 1f;
            public bool Enabled = true;
            public string Notes;
        }

        // Recorded pairings: johnN/alexN. In pair 2 the recorded order is Alex first, then John.
        private static readonly Pair[] Pairs =
        {
            new Pair { Id = "01_trolley", SetupClip = "john01", ResponseClip = "alex01", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "She's airborne with the grace of a shopping trolley pushed off a cliff.", ResponseText = "And the trolley had a better landing.",
                Topics = new[] { CommentaryTopic.Launch, CommentaryTopic.BallHit, CommentaryTopic.SweeperHit, CommentaryTopic.HardImpact },
                Notes = "Airborne: best for violent launches." },
            new Pair { Id = "02_crowd_on_fire", SetupClip = "alex2", ResponseClip = "john2", SetupSpeaker = CommentarySpeaker.Alex, ResponseSpeaker = CommentarySpeaker.John,
                SetupText = "The crowd is on its feet!", ResponseText = "Mostly because the seats are on fire.",
                Topics = new[] { CommentaryTopic.Checkpoint, CommentaryTopic.Finish, CommentaryTopic.Background },
                Notes = "Alex sets up, John delivers the punchline. Crowd/celebration line." },
            new Pair { Id = "03_netting_fish", SetupClip = "john3", ResponseClip = "alex3", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "She's tangled in the netting like a very enthusiastic fish.", ResponseText = "Except the fish isn't crying.",
                Topics = new[] { CommentaryTopic.WaterFall }, Enabled = false,
                Notes = "DISABLED: refers to netting, and no net exists on any course. Enable if a net/entanglement hazard is added (or accept it for water falls)." },
            new Pair { Id = "04_medical", SetupClip = "john4", ResponseClip = "alex4", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "Medical attention is on the way.", ResponseText = "So is the coroner, just in case.",
                Topics = new[] { CommentaryTopic.BallHit, CommentaryTopic.SweeperHit, CommentaryTopic.HardImpact, CommentaryTopic.RepeatedFailure },
                Notes = "Hard hits and repeated failure." },
            new Pair { Id = "05_athleticism", SetupClip = "john5", ResponseClip = "alex5", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "That was a spectacular display of athleticism!", ResponseText = "Athleticism's a strong word for what just happened.",
                Topics = new[] { CommentaryTopic.Launch, CommentaryTopic.BallHit, CommentaryTopic.SweeperHit },
                Notes = "Spectacular wipeouts." },
            new Pair { Id = "06_judges", SetupClip = "john6", ResponseClip = "alex6", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "The judges are deliberating.", ResponseText = "They're deciding if that counts as a wipeout or a cry for help.",
                Topics = new[] { CommentaryTopic.RepeatedFailure, CommentaryTopic.Finish, CommentaryTopic.Background },
                Notes = "Repeated failure / end of run / quiet filler." },
            new Pair { Id = "07_waving", SetupClip = "john7", ResponseClip = "alex7", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "She's waving to the audience!", ResponseText = "That's not waving. That's a distress signal with better lighting.",
                Topics = new[] { CommentaryTopic.Recovery },
                Notes = "Getting back up / respawning. There is no swimming mechanic, so no swimming line is used." },
            new Pair { Id = "08_water_lovely", SetupClip = "john8", ResponseClip = "alex8", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "The water's lovely this time of year!", ResponseText = "She didn't ask for a weather report.",
                Topics = new[] { CommentaryTopic.WaterFall }, Notes = "Water falls." },
            new Pair { Id = "09_replay", SetupClip = "john9", ResponseClip = "alex9", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "The replay shows it all in glorious detail.", ResponseText = "\"Glorious\" isn't the word her lawyer's using.",
                Topics = new[] { CommentaryTopic.Launch, CommentaryTopic.BallHit, CommentaryTopic.SweeperHit, CommentaryTopic.RepeatedFailure },
                Notes = "Aftermath of a big wipeout. The game has no replay feature; the line is TV-show framing." },
            new Pair { Id = "10_sign_off", SetupClip = "john10", ResponseClip = "alex10", SetupSpeaker = CommentarySpeaker.John, ResponseSpeaker = CommentarySpeaker.Alex,
                SetupText = "And that's Splashdown for today.", ResponseText = "Tune in next week when someone actually survives!",
                Topics = new[] { CommentaryTopic.Finish }, Weight = 3f, Notes = "Sign-off: course completion." }
        };

        // Recordings are named johnNN.mp3 / alexNN.mp3 (pair 1 is "01").
        private static readonly string[] RecordingNames =
        {
            "john01", "john2", "john3", "john4", "john5", "john6", "john7", "john8", "john9", "john10",
            "alex01", "alex2", "alex3", "alex4", "alex5", "alex6", "alex7", "alex8", "alex9", "alex10"
        };

        private static readonly string[] MusicNames =
        {
            "music_tide_of_ku", "music_obsidian_surge", "music_ancestral_fury", "music_obsidian_edge", "music_basalt_teeth"
        };

        [MenuItem("Bikini Wipeout/Audio/Integrate Commentary and Music", priority = 70)]
        public static void IntegrateMenu()
        {
            Integrate();
        }

        /// <summary>Batch entry point: -executeMethod BikiniWipeout.EditorTools.CommentaryIntegration.IntegrateBatch</summary>
        public static void IntegrateBatch()
        {
            try
            {
                Integrate();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[CommentaryIntegration] " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Integrate()
        {
            AssetDatabase.Refresh();
            ConfigureImporters();
            CommentaryLibrary commentary = BuildCommentaryLibrary();
            UpdateAudioLibrary();
            AttachDirectorToAudioManagerPrefab(commentary);
            AddAnnouncerSliders();
            AssetDatabase.SaveAssets();
            Debug.Log("[CommentaryIntegration] Done.");
        }

        private static void ConfigureImporters()
        {
            foreach (string name in RecordingNames)
            {
                SetImporter(CommentaryFolder + "/" + name + ".mp3", AudioClipLoadType.DecompressOnLoad, true);
            }

            foreach (string name in MusicNames)
            {
                SetImporter(MusicFolder + "/" + name + ".ogg", AudioClipLoadType.Streaming, false);
            }
        }

        private static void SetImporter(string path, AudioClipLoadType loadType, bool mono)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Audio file did not import: " + path);
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.75f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = mono;
            importer.loadInBackground = loadType == AudioClipLoadType.Streaming;
            importer.SaveAndReimport();
        }

        private static AudioClip Clip(string folder, string name, string extension)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "/" + name + extension);
            if (clip == null)
            {
                throw new InvalidOperationException("Missing audio clip: " + folder + "/" + name + extension);
            }

            return clip;
        }

        private static CommentaryLibrary BuildCommentaryLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<CommentaryLibrary>(CommentaryLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CommentaryLibrary>();
                AssetDatabase.CreateAsset(library, CommentaryLibraryPath);
            }

            library.exchanges = new List<CommentaryLibrary.ExchangeEntry>();
            foreach (Pair pair in Pairs)
            {
                library.exchanges.Add(new CommentaryLibrary.ExchangeEntry
                {
                    id = pair.Id,
                    setup = new CommentaryLibrary.Line { speaker = pair.SetupSpeaker, clip = Clip(CommentaryFolder, pair.SetupClip, ".mp3"), transcript = pair.SetupText },
                    response = new CommentaryLibrary.Line { speaker = pair.ResponseSpeaker, clip = Clip(CommentaryFolder, pair.ResponseClip, ".mp3"), transcript = pair.ResponseText },
                    topics = new List<CommentaryTopic>(pair.Topics),
                    weight = pair.Weight,
                    enabled = pair.Enabled,
                    notes = pair.Notes
                });
            }

            EditorUtility.SetDirty(library);
            return library;
        }

        private static void UpdateAudioLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(Foundation1Builder.AudioLibraryPath);
            if (library == null)
            {
                throw new InvalidOperationException("AudioLibrary asset not found: " + Foundation1Builder.AudioLibraryPath);
            }

            SetMusic(library, MusicTrackId.MainMenu, "music_tide_of_ku", 0.7f);
            SetMusic(library, MusicTrackId.Course, "music_obsidian_surge", 0.6f);
            SetMusic(library, MusicTrackId.Results, "music_ancestral_fury", 0.7f);
            EditorUtility.SetDirty(library);
        }

        private static void SetMusic(AudioLibrary library, MusicTrackId id, string name, float volume)
        {
            AudioLibrary.MusicEntry entry = library.FindMusic(id);
            if (entry == null)
            {
                entry = new AudioLibrary.MusicEntry { id = id };
                library.music.Add(entry);
            }

            entry.clip = Clip(MusicFolder, name, ".ogg");
            entry.volume = volume;
            entry.loop = true;
        }

        private static void AttachDirectorToAudioManagerPrefab(CommentaryLibrary commentary)
        {
            string path = Foundation1Builder.AudioManagerPrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var director = root.GetComponent<CommentaryDirector>();
                if (director == null)
                {
                    director = root.AddComponent<CommentaryDirector>();
                }

                director.library = commentary;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ announcer slider in existing scenes

        private static void AddAnnouncerSliders()
        {
            string[] scenePaths =
            {
                Foundation1Builder.MainMenuScenePath, Foundation1Builder.CourseScenePath,
                Foundation1Builder.ScenesFolder + "/Course_Foundation1_Cowgirl.unity", Foundation1Builder.GauntletScenePath
            };

            foreach (string scenePath in scenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool changed = false;
                foreach (VolumeSliders sliders in UnityEngine.Object.FindObjectsByType<VolumeSliders>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    changed |= EnsureAnnouncerSlider(sliders);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }

        private static bool EnsureAnnouncerSlider(VolumeSliders sliders)
        {
            if (sliders.announcerSlider != null || sliders.sfxSlider == null)
            {
                return false;
            }

            Slider clone = UnityEngine.Object.Instantiate(sliders.sfxSlider, sliders.sfxSlider.transform.parent);
            clone.gameObject.name = "AnnouncerVolume";
            Text label = clone.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "ANNOUNCERS";
            }

            Place(sliders.masterSlider, 90f);
            Place(sliders.musicSlider, 20f);
            Place(sliders.sfxSlider, -50f);
            Place(clone, -120f);
            sliders.announcerSlider = clone;
            EditorUtility.SetDirty(sliders);
            return true;
        }

        private static void Place(Slider slider, float y)
        {
            if (slider == null)
            {
                return;
            }

            var rect = (RectTransform)slider.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }
    }
}
