using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Confetti and crowd reaction when the course is completed.</summary>
    [DisallowMultipleComponent]
    public sealed class FinishCelebration : MonoBehaviour
    {
        [SerializeField] internal CourseManager course;

        [Tooltip("Particle systems to fire. Defaults to all particle systems under this object.")]
        [SerializeField] internal ParticleSystem[] confetti;

        public bool HasFired { get; private set; }

        private void Awake()
        {
            if (confetti == null || confetti.Length == 0)
            {
                confetti = GetComponentsInChildren<ParticleSystem>(true);
            }

            foreach (ParticleSystem system in confetti)
            {
                if (system != null)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void Start()
        {
            if (course == null) course = CourseManager.Current;
            if (course != null) course.Finished += OnFinished;
        }

        private void OnDestroy()
        {
            if (course != null) course.Finished -= OnFinished;
        }

        private void OnFinished(CourseResult result)
        {
            HasFired = true;
            foreach (ParticleSystem system in confetti)
            {
                if (system != null)
                {
                    system.Play(true);
                }
            }

            AudioManager.Play(AudioEventId.ConfettiPop, transform.position);
            AudioManager.Play(AudioEventId.CrowdCheer);
        }
    }
}
