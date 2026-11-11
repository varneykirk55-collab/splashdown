using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Scene-level visual feedback for hits: spawns a burst effect where an obstacle knocks the
    /// player down. Lives in the course scene, so no character prefab has to change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactEffects : MonoBehaviour
    {
        [SerializeField] internal CourseManager course;

        [Tooltip("Instantiated at the impact point. Should destroy itself (see SplashEffect).")]
        [SerializeField] internal GameObject knockdownBurstPrefab;

        private KnockdownController knockdown;

        private void Start()
        {
            if (course == null) course = CourseManager.Current;
            knockdown = course != null && course.Player != null ? course.Player.Knockdown : null;
            if (knockdown != null) knockdown.KnockedDown += OnKnockedDown;
        }

        private void OnDestroy()
        {
            if (knockdown != null) knockdown.KnockedDown -= OnKnockedDown;
        }

        private void OnKnockedDown(ImpactData impact)
        {
            if (knockdownBurstPrefab != null)
            {
                Instantiate(knockdownBurstPrefab, impact.Point, Quaternion.identity);
            }
        }
    }
}
