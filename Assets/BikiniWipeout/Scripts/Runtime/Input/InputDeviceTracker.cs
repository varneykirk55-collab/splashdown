using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BikiniWipeout
{
    /// <summary>
    /// Remembers whether the player is currently using a gamepad or keyboard/mouse, so on-screen
    /// hints and rumble can follow the device that was touched last. It updates itself after every
    /// Input System update; there is nothing to add to a scene.
    /// </summary>
    public static class InputDeviceTracker
    {
        private static bool initialized;

        /// <summary>True when the most recent real input came from a gamepad.</summary>
        public static bool UsingGamepad { get; private set; }

        /// <summary>Raised when the active device family changes. Argument: using a gamepad.</summary>
        public static event System.Action<bool> Changed;

        /// <summary>Default for the very first frame, before any input: a connected gamepad counts as active only once used.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            UsingGamepad = false;
            Changed = null;
            if (!initialized)
            {
                InputSystem.onAfterUpdate += OnAfterUpdate;
                initialized = true;
            }
        }

        /// <summary>Test hook: force the active device family.</summary>
        public static void Report(bool gamepad)
        {
            Set(gamepad);
        }

        private static void OnAfterUpdate()
        {
            Gamepad pad = Gamepad.current;
            if (pad != null && pad.wasUpdatedThisFrame && !pad.CheckStateIsAtDefaultIgnoringNoise())
            {
                Set(true);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.wasUpdatedThisFrame && keyboard.anyKey.wasPressedThisFrame)
            {
                Set(false);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.wasUpdatedThisFrame &&
                (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame ||
                 mouse.rightButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0f))
            {
                Set(false);
            }
        }

        private static void Set(bool gamepad)
        {
            if (UsingGamepad == gamepad)
            {
                return;
            }

            UsingGamepad = gamepad;
            Changed?.Invoke(gamepad);
        }
    }
}
