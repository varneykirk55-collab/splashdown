using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Entry point to a contestant. Other systems (camera, course, UI) talk to the character through
    /// this facade instead of reaching into individual components, so the internals can be replaced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] internal CharacterMotor motor;
        [SerializeField] internal KnockdownController knockdown;
        [SerializeField] internal RagdollController ragdoll;
        [SerializeField] internal PlayerRespawner respawner;
        [SerializeField] internal AnimationStateController animationState;

        [Tooltip("Root of the visual model. Hidden while waiting to respawn.")]
        [SerializeField] internal Transform modelRoot;

        [Tooltip("Height above the feet that the camera looks at while the character is standing.")]
        [SerializeField, Min(0f)] internal float focusHeight = 1.05f;

        private Renderer[] renderers;

        public CharacterMotor Motor => motor;
        public KnockdownController Knockdown => knockdown;
        public RagdollController Ragdoll => ragdoll;
        public PlayerRespawner Respawner => respawner;
        public AnimationStateController AnimationState => animationState;
        public ICharacterInput Input => motor != null ? motor.InputSource : null;
        public bool IsVisible { get; private set; } = true;

        /// <summary>The point the camera should follow: the hips while ragdolled, otherwise chest height.</summary>
        public Vector3 FocusPosition
        {
            get
            {
                if (ragdoll != null && ragdoll.IsActive)
                {
                    return ragdoll.RootBonePosition;
                }

                return transform.position + Vector3.up * focusHeight;
            }
        }

        private void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (knockdown == null) knockdown = GetComponent<KnockdownController>();
            if (ragdoll == null) ragdoll = GetComponent<RagdollController>();
            if (respawner == null) respawner = GetComponent<PlayerRespawner>();
            if (animationState == null) animationState = GetComponentInChildren<AnimationStateController>();
        }

        public void SetVisible(bool visible)
        {
            if (renderers == null)
            {
                Transform root = modelRoot != null ? modelRoot : transform;
                renderers = root.GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }

            IsVisible = visible;
        }
    }
}
