using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout
{
    /// <summary>Shows the control legend for the device in use: keyboard + mouse or gamepad.</summary>
    [DisallowMultipleComponent]
    public sealed class ControlHints : MonoBehaviour
    {
        public const string DefaultKeyboardText =
            "WASD move    MOUSE look    WHEEL zoom    SPACE jump    SHIFT run    ESC pause";

        public const string DefaultGamepadText =
            "L-STICK move    R-STICK look    D-PAD UP/DOWN zoom    A jump    L-TRIGGER run    START pause";

        [SerializeField] internal Text target;
        [SerializeField, TextArea] internal string keyboardText = DefaultKeyboardText;
        [SerializeField, TextArea] internal string gamepadText = DefaultGamepadText;

        private void OnEnable()
        {
            InputDeviceTracker.Changed += Apply;
            Apply(InputDeviceTracker.UsingGamepad);
        }

        private void OnDisable()
        {
            InputDeviceTracker.Changed -= Apply;
        }

        private void Apply(bool gamepad)
        {
            if (target != null)
            {
                target.text = gamepad ? gamepadText : keyboardText;
            }
        }
    }
}
