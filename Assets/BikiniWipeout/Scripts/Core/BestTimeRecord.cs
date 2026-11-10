namespace BikiniWipeout.Core
{
    /// <summary>Stores the best completion time per course.</summary>
    public sealed class BestTimeRecord
    {
        private readonly IKeyValueStore store;
        private readonly string key;

        public BestTimeRecord(IKeyValueStore store, string courseId)
        {
            this.store = store;
            key = "bw.best." + (string.IsNullOrEmpty(courseId) ? "default" : courseId);
        }

        public bool HasRecord => BestSeconds > 0f;

        /// <summary>Best time in seconds, or 0 when no run has been recorded.</summary>
        public float BestSeconds => store != null ? store.GetFloat(key, 0f) : 0f;

        /// <summary>Records a finished run. Returns true if it is a new best.</summary>
        public bool Submit(float seconds)
        {
            if (store == null || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return false;
            }

            float best = BestSeconds;
            if (best > 0f && seconds >= best)
            {
                return false;
            }

            store.SetFloat(key, seconds);
            store.Save();
            return true;
        }
    }
}
