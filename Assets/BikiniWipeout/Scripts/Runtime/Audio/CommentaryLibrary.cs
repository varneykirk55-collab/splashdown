using System;
using System.Collections.Generic;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// The recorded announcer exchanges: each is a setup line followed by a response line, tagged with the
    /// gameplay topics it suits. Add recordings here (no code changes) to widen the pool.
    /// </summary>
    [CreateAssetMenu(menuName = "Bikini Wipeout/Commentary Library", fileName = "CommentaryLibrary")]
    public sealed class CommentaryLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Line
        {
            public CommentarySpeaker speaker;
            public AudioClip clip;

            [Tooltip("What the recording says (for reference and tests).")]
            public string transcript;
        }

        [Serializable]
        public sealed class ExchangeEntry
        {
            public string id;
            public Line setup = new Line();
            public Line response = new Line();
            public List<CommentaryTopic> topics = new List<CommentaryTopic>();
            [Min(0.01f)] public float weight = 1f;
            public bool enabled = true;

            [TextArea] public string notes;

            public bool HasClips => setup != null && response != null && setup.clip != null && response.clip != null;
        }

        public List<ExchangeEntry> exchanges = new List<ExchangeEntry>();
        public CommentarySettings settings = new CommentarySettings();

        /// <summary>Scheduler descriptions, index-aligned with <see cref="exchanges"/>. Entries without both clips are disabled.</summary>
        public List<CommentaryExchange> BuildSchedulerExchanges()
        {
            var list = new List<CommentaryExchange>(exchanges.Count);
            foreach (ExchangeEntry entry in exchanges)
            {
                bool usable = entry != null && entry.enabled && entry.HasClips;
                list.Add(new CommentaryExchange(
                    entry != null ? entry.id : "(null)",
                    entry != null ? entry.topics : new List<CommentaryTopic>(),
                    usable ? entry.setup.clip.length : 1f,
                    usable ? entry.response.clip.length : 1f,
                    entry != null ? entry.weight : 1f,
                    usable));
            }

            return list;
        }
    }
}
