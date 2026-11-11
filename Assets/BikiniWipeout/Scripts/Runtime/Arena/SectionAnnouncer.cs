using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>Trigger volume at the start of a course section: shows its name on the HUD the first time the player enters.</summary>
    [DisallowMultipleComponent]
    public sealed class SectionAnnouncer : MonoBehaviour
    {
        [SerializeField] internal string sectionName = "SECTION";
        [SerializeField] internal HudController hud;

        public string SectionName => sectionName;
        public bool Announced { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (Announced || other.GetComponentInParent<PlayerCharacter>() == null)
            {
                return;
            }

            Announced = true;
            if (hud == null)
            {
                hud = FindAnyObjectByType<HudController>();
            }

            if (hud != null)
            {
                hud.ShowMessage(sectionName);
            }
        }
    }
}
