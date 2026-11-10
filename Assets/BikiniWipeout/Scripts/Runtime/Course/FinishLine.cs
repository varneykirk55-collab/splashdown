using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Trigger volume that ends the run when the character crosses it.</summary>
    [DisallowMultipleComponent]
    public sealed class FinishLine : MonoBehaviour
    {
        [SerializeField] internal CourseManager course;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerCharacter>() == null)
            {
                return;
            }

            CourseManager manager = course != null ? course : CourseManager.Current;
            if (manager != null)
            {
                manager.NotifyFinishReached();
            }
        }
    }
}
