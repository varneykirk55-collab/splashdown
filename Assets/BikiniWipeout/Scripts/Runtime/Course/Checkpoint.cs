using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// A respawn point. The trigger volume activates it; <see cref="spawnPoint"/> (which must sit on
    /// static, obstacle-free ground) is where the character reappears.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Checkpoint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Order along the course. 0 is the start.")]
        [SerializeField, Min(0)] internal int index;

        [SerializeField] internal Transform spawnPoint;
        [SerializeField] internal CheckpointManager manager;

        [Header("Visual feedback (optional)")]
        [SerializeField] internal Renderer[] indicators;
        [SerializeField] internal Color inactiveColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] internal Color activeColor = new Color(0.2f, 1f, 0.45f, 1f);

        private MaterialPropertyBlock block;

        public int Index => index;
        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
        public Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        public bool IsActivated { get; private set; }

        private void Awake()
        {
            if (manager == null)
            {
                manager = GetComponentInParent<CheckpointManager>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (manager != null && other.GetComponentInParent<PlayerCharacter>() != null)
            {
                manager.TryActivate(this);
            }
        }

        internal void SetActivated(bool activated)
        {
            IsActivated = activated;
            if (indicators == null)
            {
                return;
            }

            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            for (int i = 0; i < indicators.Length; i++)
            {
                if (indicators[i] == null)
                {
                    continue;
                }

                indicators[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, activated ? activeColor : inactiveColor);
                indicators[i].SetPropertyBlock(block);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
            Vector3 p = SpawnPosition;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.3f, 0.3f);
            Gizmos.DrawLine(p + Vector3.up * 0.3f, p + Vector3.up * 0.3f + SpawnRotation * Vector3.forward);
        }
    }
}
