using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Forwards a bone's collisions to its <see cref="RagdollController"/>. Added automatically at runtime.</summary>
    [AddComponentMenu("")]
    public sealed class RagdollBoneCollisionReporter : MonoBehaviour
    {
        internal RagdollController owner;

        private void OnCollisionEnter(Collision collision)
        {
            if (owner != null)
            {
                owner.ReportBoneCollision(collision);
            }
        }
    }
}
