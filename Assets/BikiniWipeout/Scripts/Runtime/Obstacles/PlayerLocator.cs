using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// How course obstacles find the contestant. They talk to the shared player systems
    /// (<see cref="PlayerCharacter"/>, <see cref="CharacterMotor"/>), never to a particular model,
    /// so any character built on those systems works with every obstacle.
    /// </summary>
    public static class PlayerLocator
    {
        private static PlayerCharacter cached;
        private static float nextSearchTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            nextSearchTime = 0f;
        }

        /// <summary>The character of the running course, or the first one in the scene if there is no course.</summary>
        public static PlayerCharacter Current
        {
            get
            {
                CourseManager course = CourseManager.Current;
                if (course != null && course.Player != null)
                {
                    return course.Player;
                }

                if (cached == null && Time.unscaledTime >= nextSearchTime)
                {
                    nextSearchTime = Time.unscaledTime + 1f;
                    cached = Object.FindAnyObjectByType<PlayerCharacter>();
                }

                return cached;
            }
        }

        /// <summary>True when the character is on its feet under motor control (not ragdolled, hidden or respawning).</summary>
        public static bool TryGetActiveMotor(out PlayerCharacter player, out CharacterMotor motor)
        {
            player = Current;
            motor = player != null ? player.Motor : null;
            return motor != null && motor.isActiveAndEnabled && !motor.IsSuspended
                && motor.Controller != null && motor.Controller.enabled;
        }

        /// <summary>True when the character is grounded on <paramref name="root"/> or one of its children.</summary>
        public static bool IsStandingOn(CharacterMotor motor, Transform root)
        {
            if (motor == null || root == null || !motor.IsGrounded)
            {
                return false;
            }

            Transform ground = motor.GroundTransform;
            return ground != null && (ground == root || ground.IsChildOf(root));
        }

        /// <summary>The ragdoll, but only while it is simulating.</summary>
        public static RagdollController ActiveRagdoll(PlayerCharacter player)
        {
            RagdollController ragdoll = player != null ? player.Ragdoll : null;
            return ragdoll != null && ragdoll.IsActive ? ragdoll : null;
        }
    }
}
