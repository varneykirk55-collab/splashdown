using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Placeholder splash: plays any particle systems on the object, then removes itself.</summary>
    [DisallowMultipleComponent]
    public sealed class SplashEffect : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] internal float lifetime = 2f;

        private void Start()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>();
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Play();
            }

            Destroy(gameObject, lifetime);
        }
    }
}
