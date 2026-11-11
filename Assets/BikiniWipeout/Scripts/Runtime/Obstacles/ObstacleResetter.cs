using System.Collections.Generic;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Returns a group of obstacles to their starting state whenever the character respawns, so a
    /// checkpoint restart always meets the section as it was built (collapsed bridges are whole again).
    /// Works with anything that implements <see cref="IResettable"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObstacleResetter : MonoBehaviour
    {
        [Tooltip("Every IResettable under these objects is reset. Defaults to this object.")]
        [SerializeField] internal Transform[] roots;

        private readonly List<IResettable> targets = new List<IResettable>();
        private PlayerRespawner respawner;

        /// <summary>How many times the group has been reset.</summary>
        public int ResetCount { get; private set; }

        public int TargetCount => targets.Count;

        private void Start()
        {
            if (roots == null || roots.Length == 0)
            {
                roots = new[] { transform };
            }

            foreach (Transform root in roots)
            {
                if (root != null)
                {
                    targets.AddRange(root.GetComponentsInChildren<IResettable>(true));
                }
            }

            PlayerCharacter player = PlayerLocator.Current;
            respawner = player != null ? player.Respawner : null;
            if (respawner != null)
            {
                respawner.Respawned += ResetAll;
            }
        }

        private void OnDestroy()
        {
            if (respawner != null)
            {
                respawner.Respawned -= ResetAll;
            }
        }

        [ContextMenu("Reset All")]
        public void ResetAll()
        {
            ResetCount++;
            for (int i = 0; i < targets.Count; i++)
            {
                // Skip obstacles that have been destroyed since the list was built.
                if (targets[i] is Object target && target != null)
                {
                    targets[i].ResetState();
                }
            }
        }
    }
}
