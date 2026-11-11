using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Slow decorative motion for the menu stage: pulsing light glows, sweeping beams and drifting water shimmer.</summary>
    public sealed class MenuAmbience : MonoBehaviour
    {
        [SerializeField] internal Image[] glows;
        [SerializeField] internal RectTransform[] beams;
        [SerializeField] internal RectTransform[] shimmers;

        private float[] glowBase;
        private float[] beamBase;
        private Vector2[] shimmerBase;

        private void Awake()
        {
            glowBase = new float[glows != null ? glows.Length : 0];
            for (int i = 0; i < glowBase.Length; i++) glowBase[i] = glows[i] != null ? glows[i].color.a : 0f;
            beamBase = new float[beams != null ? beams.Length : 0];
            for (int i = 0; i < beamBase.Length; i++) beamBase[i] = beams[i] != null ? beams[i].localEulerAngles.z : 0f;
            shimmerBase = new Vector2[shimmers != null ? shimmers.Length : 0];
            for (int i = 0; i < shimmerBase.Length; i++) shimmerBase[i] = shimmers[i] != null ? shimmers[i].anchoredPosition : Vector2.zero;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < glowBase.Length; i++)
            {
                if (glows[i] == null) continue;
                Color c = glows[i].color;
                c.a = glowBase[i] * (0.8f + 0.2f * Mathf.Sin(t * (0.9f + i * 0.27f) + i));
                glows[i].color = c;
            }

            for (int i = 0; i < beamBase.Length; i++)
            {
                if (beams[i] == null) continue;
                beams[i].localEulerAngles = new Vector3(0f, 0f, beamBase[i] + Mathf.Sin(t * 0.35f + i * 1.7f) * 6f);
            }

            for (int i = 0; i < shimmerBase.Length; i++)
            {
                if (shimmers[i] == null) continue;
                shimmers[i].anchoredPosition = shimmerBase[i] + new Vector2(Mathf.Sin(t * 0.5f + i * 2.1f) * 90f, Mathf.Sin(t * 1.3f + i) * 3f);
            }
        }
    }
}
