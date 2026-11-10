using System;
using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Owns the ordered list of checkpoints of a course and tracks which one is active.</summary>
    [DisallowMultipleComponent]
    public sealed class CheckpointManager : MonoBehaviour
    {
        [Tooltip("Checkpoints in course order. Element 0 is the start.")]
        [SerializeField] internal Checkpoint[] checkpoints = new Checkpoint[0];

        private CheckpointTracker tracker;

        public int Count => checkpoints != null ? checkpoints.Length : 0;
        public int CurrentIndex => tracker != null ? tracker.CurrentIndex : 0;
        public Checkpoint Current => GetCheckpoint(CurrentIndex);

        /// <summary>Raised when a new (further) checkpoint is reached.</summary>
        public event Action<Checkpoint> CheckpointActivated;

        private void Awake()
        {
            if (checkpoints == null || checkpoints.Length == 0)
            {
                checkpoints = GetComponentsInChildren<Checkpoint>();
                Array.Sort(checkpoints, (a, b) => a.Index.CompareTo(b.Index));
            }

            tracker = new CheckpointTracker(Mathf.Max(1, checkpoints.Length));
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (checkpoints[i] != null)
                {
                    checkpoints[i].SetActivated(i == 0);
                }
            }
        }

        public Checkpoint GetCheckpoint(int index)
        {
            if (checkpoints == null || index < 0 || index >= checkpoints.Length)
            {
                return null;
            }

            return checkpoints[index];
        }

        public bool TryActivate(Checkpoint checkpoint)
        {
            if (checkpoint == null || tracker == null)
            {
                return false;
            }

            int index = Array.IndexOf(checkpoints, checkpoint);
            if (index < 0 || !tracker.TryActivate(index))
            {
                return false;
            }

            checkpoint.SetActivated(true);
            CheckpointActivated?.Invoke(checkpoint);
            return true;
        }
    }
}
