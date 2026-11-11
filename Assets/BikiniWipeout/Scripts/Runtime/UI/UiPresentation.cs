using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Unscaled, brief entrance and focus effects; works while the course is paused.</summary>
    public sealed class UiPresentation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] internal bool button;
        private float entrance;
        private bool hovered, selected, pressed;
        private Vector3 initialScale;
        private CanvasGroup group;
        private static Sprite rounded;

        private void Awake()
        {
            initialScale = transform.localScale;
            if (button)
            {
                var image = GetComponent<Image>();
                if (image != null) { image.sprite = RoundedSprite(); image.type = Image.Type.Sliced; }
            }
            else group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }
        private void OnEnable() { entrance = 0f; hovered = selected = pressed = false; }
        private void Update()
        {
            entrance = Mathf.Min(1f, entrance + Time.unscaledDeltaTime * 7f);
            float easing = 1f - (1f - entrance) * (1f - entrance);
            float target = pressed ? 0.96f : (hovered || selected ? 1.045f : 1f);
            transform.localScale = Vector3.Lerp(transform.localScale, initialScale * (button ? target : 0.97f + 0.03f * easing), Time.unscaledDeltaTime * 14f);
            if (group != null) group.alpha = easing;
        }
        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }
        private static Sprite RoundedSprite()
        {
            if (rounded != null) return rounded;
            const int size = 64, radius = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI rounded corners", hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, 0, x - (size - 1 - radius));
                float dy = Mathf.Max(radius - y, 0, y - (size - 1 - radius));
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            rounded.hideFlags = HideFlags.HideAndDontSave;
            return rounded;
        }
    }
}
