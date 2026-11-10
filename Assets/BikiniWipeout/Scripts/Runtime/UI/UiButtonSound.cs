using UnityEngine;
using UnityEngine.EventSystems;

namespace BikiniWipeout
{
    /// <summary>Plays UI audio events for hover / select and click / submit on any selectable.</summary>
    [DisallowMultipleComponent]
    public sealed class UiButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            AudioManager.Play(AudioEventId.UiHover);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            AudioManager.Play(AudioEventId.UiClick);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            AudioManager.Play(AudioEventId.UiClick);
        }
    }
}
