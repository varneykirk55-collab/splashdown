using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Input as the gameplay code sees it. <see cref="PlayerInputReader"/> implements it with the
    /// Input System; tests and future AI contestants can supply their own implementation.
    /// </summary>
    public interface ICharacterInput
    {
        /// <summary>Movement stick: x = strafe, y = forward. Magnitude 0..1.</summary>
        Vector2 Move { get; }

        /// <summary>Camera look input for this frame.</summary>
        Vector2 Look { get; }

        /// <summary>True when <see cref="Look"/> is a pointer delta in pixels; false when it is a stick value (-1..1).</summary>
        bool LookIsPointerDelta { get; }

        bool RunHeld { get; }
        bool JumpHeld { get; }

        /// <summary>True only on the frame the jump button went down.</summary>
        bool JumpPressedThisFrame { get; }

        /// <summary>True only on the frame the pause button went down.</summary>
        bool PausePressedThisFrame { get; }
    }
}
