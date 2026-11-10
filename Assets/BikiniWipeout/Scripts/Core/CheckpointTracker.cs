using System;

namespace BikiniWipeout.Core
{
    /// <summary>
    /// Tracks the furthest checkpoint reached. Checkpoints are ordered by index along the course and
    /// can only advance, so running back over an earlier checkpoint never moves the respawn point backwards.
    /// </summary>
    public sealed class CheckpointTracker
    {
        public CheckpointTracker(int checkpointCount)
        {
            if (checkpointCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(checkpointCount), "A course needs at least one checkpoint (the start).");
            }

            Count = checkpointCount;
            CurrentIndex = 0;
        }

        public int Count { get; }

        /// <summary>Index of the active respawn checkpoint. Index 0 is the start platform.</summary>
        public int CurrentIndex { get; private set; }

        public event Action<int> Activated;

        /// <summary>Activates a checkpoint if it is further along than the current one.</summary>
        public bool TryActivate(int index)
        {
            if (index < 0 || index >= Count || index <= CurrentIndex)
            {
                return false;
            }

            CurrentIndex = index;
            Activated?.Invoke(index);
            return true;
        }

        public void Reset()
        {
            CurrentIndex = 0;
        }
    }
}
