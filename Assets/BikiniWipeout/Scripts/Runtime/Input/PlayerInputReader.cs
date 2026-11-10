using UnityEngine;
using UnityEngine.InputSystem;

namespace BikiniWipeout
{
    /// <summary>
    /// Reads the "Player" action map of the Input System and exposes it as <see cref="ICharacterInput"/>.
    /// Resolution order for the actions: the assigned asset, then the project-wide actions asset,
    /// then a built-in set of default bindings, so input always works.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, ICharacterInput
    {
        public const string ActionMapName = "Player";
        public const string MoveActionName = "Move";
        public const string LookActionName = "Look";
        public const string JumpActionName = "Jump";
        public const string RunActionName = "Sprint";
        public const string PauseActionName = "Pause";

        [Tooltip("Input actions asset containing a 'Player' map. Optional: falls back to project-wide actions, then to built-in defaults.")]
        [SerializeField] internal InputActionAsset actionsAsset;

        private InputActionAsset runtimeAsset;
        private InputActionMap map;
        private bool ownsMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction runAction;
        private InputAction pauseAction;

        public Vector2 Move
        {
            get
            {
                if (moveAction == null)
                {
                    return Vector2.zero;
                }

                return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            }
        }

        public Vector2 Look => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        public bool LookIsPointerDelta
        {
            get
            {
                InputControl control = lookAction != null ? lookAction.activeControl : null;
                return control == null || control.device is Pointer;
            }
        }

        public bool RunHeld => runAction != null && runAction.IsPressed();
        public bool JumpHeld => jumpAction != null && jumpAction.IsPressed();
        public bool JumpPressedThisFrame => jumpAction != null && jumpAction.WasPressedThisFrame();
        public bool PausePressedThisFrame => pauseAction != null && pauseAction.WasPressedThisFrame();

        /// <summary>Where the active bindings came from. Shown by the validation tools.</summary>
        public string BindingSource { get; private set; } = "none";

        private void OnEnable()
        {
            if (map == null)
            {
                ResolveActions();
            }

            map.Enable();
        }

        private void OnDisable()
        {
            map?.Disable();
        }

        private void OnDestroy()
        {
            if (runtimeAsset != null)
            {
                Destroy(runtimeAsset);
                runtimeAsset = null;
            }

            if (ownsMap)
            {
                map?.Dispose();
            }

            map = null;
        }

        private void ResolveActions()
        {
            InputActionAsset source = actionsAsset;
            BindingSource = "assigned asset";

            if (source == null || source.FindActionMap(ActionMapName) == null)
            {
                source = InputSystem.actions;
                BindingSource = "project-wide actions";
            }

            if (source != null && source.FindActionMap(ActionMapName) != null)
            {
                // Private copy: enabling or disabling it never affects other users of the asset.
                runtimeAsset = Instantiate(source);
                map = runtimeAsset.FindActionMap(ActionMapName);
            }
            else
            {
                map = CreateDefaultMap();
                ownsMap = true;
                BindingSource = "built-in defaults";
            }

            moveAction = map.FindAction(MoveActionName);
            lookAction = map.FindAction(LookActionName);
            jumpAction = map.FindAction(JumpActionName);
            runAction = map.FindAction(RunActionName);
            pauseAction = map.FindAction(PauseActionName);

            if (moveAction == null || lookAction == null || jumpAction == null || runAction == null || pauseAction == null)
            {
                Debug.LogWarning(
                    $"[PlayerInputReader] Action map '{ActionMapName}' from {BindingSource} is missing one of: " +
                    $"{MoveActionName}, {LookActionName}, {JumpActionName}, {RunActionName}, {PauseActionName}.", this);
            }
        }

        /// <summary>Default keyboard/mouse + gamepad bindings, mirroring BikiniWipeoutControls.inputactions.</summary>
        public static InputActionMap CreateDefaultMap()
        {
            var defaults = new InputActionMap(ActionMapName);

            InputAction move = defaults.AddAction(MoveActionName, InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            InputAction look = defaults.AddAction(LookActionName, InputActionType.Value);
            look.expectedControlType = "Vector2";
            look.AddBinding("<Pointer>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            InputAction jump = defaults.AddAction(JumpActionName, InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            InputAction run = defaults.AddAction(RunActionName, InputActionType.Button);
            run.AddBinding("<Keyboard>/leftShift");
            run.AddBinding("<Gamepad>/leftStickPress");
            run.AddBinding("<Gamepad>/leftTrigger");

            InputAction pause = defaults.AddAction(PauseActionName, InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Keyboard>/p");
            pause.AddBinding("<Gamepad>/start");

            return defaults;
        }
    }
}
