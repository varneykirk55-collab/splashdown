using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>A slow, unscaled drift for decorative menu shapes.</summary>
    public sealed class MenuBackgroundMotion : MonoBehaviour
    {
        private Vector3 origin;
        private void Awake() { origin = transform.localPosition; }
        private void Update() { transform.localPosition = origin + new Vector3(Mathf.Sin(Time.unscaledTime * .4f) * 25f, Mathf.Cos(Time.unscaledTime * .3f) * 12f, 0f); }
    }
}
