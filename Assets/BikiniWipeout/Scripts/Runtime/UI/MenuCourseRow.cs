using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>One course slot on the hub screen: thumbnail, number, title and a one-line description / status.</summary>
    public sealed class MenuCourseRow : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image frame;
        [SerializeField] internal Image thumbnail;
        [SerializeField] internal Text numberText;
        [SerializeField] internal Text titleText;
        [SerializeField] internal Text subtitleText;

        public void SetState(bool selected, bool available)
        {
            if (frame != null) frame.sprite = UiTheme.Get(selected ? UiTheme.Skin.RowSelected : UiTheme.Skin.Row);
            if (thumbnail != null) thumbnail.color = available ? Color.white : new Color(0.35f, 0.38f, 0.5f, 1f);
        }
    }
}
