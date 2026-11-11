using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>One character slot on the hub screen: portrait, name and a selected marker.</summary>
    public sealed class MenuCharacterCard : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image frame;
        [SerializeField] internal Image portrait;
        [SerializeField] internal Image check;
        [SerializeField] internal Text nameText;

        public void SetState(bool selected)
        {
            if (frame != null) frame.sprite = UiTheme.Get(selected ? UiTheme.Skin.CardSelected : UiTheme.Skin.Card);
            if (check != null) check.gameObject.SetActive(selected);
        }
    }

}
