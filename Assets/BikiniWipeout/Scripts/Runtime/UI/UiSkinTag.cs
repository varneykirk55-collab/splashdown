using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>
    /// Applies a <see cref="UiTheme.Skin"/> to this object's Image (and button tints) at runtime. Lets generated
    /// scenes reference the theme without serializing generated sprites.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSkinTag : MonoBehaviour
    {
        public UiTheme.Skin skin = UiTheme.Skin.Panel;
        public Color tint = Color.white;
        public float ppuMultiplier = 1f;

        private void Awake() { Apply(); }

        public void Apply()
        {
            var image = GetComponent<Image>();
            if (image == null) return;
            image.sprite = UiTheme.Get(skin);
            image.color = tint;
            image.type = skin == UiTheme.Skin.Dots ? Image.Type.Tiled : (UiTheme.IsSliced(skin) ? Image.Type.Sliced : Image.Type.Simple);
            image.pixelsPerUnitMultiplier = ppuMultiplier;

            var button = GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.86f, 0.9f, 1f, 1f);
                colors.highlightedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.66f, 0.7f, 0.82f, 1f);
                colors.disabledColor = new Color(0.45f, 0.48f, 0.58f, 0.75f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }
        }
    }
}
