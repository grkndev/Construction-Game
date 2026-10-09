using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Keyboard/mouse and gamepad bound to the same two actions. Defined in code (not an .inputactions
    /// asset) because the game only needs Move and Build; a full action map can replace this later.
    /// </summary>
    public sealed class GameInput : MonoBehaviour
    {
        InputAction move;
        InputAction build;
        InputAction use;

        public Vector2 Move => move.ReadValue<Vector2>();
        public bool BuildHeld => build.IsPressed();
        public bool BuildPressedThisFrame => build.WasPressedThisFrame();
        public bool UsePressedThisFrame => use.WasPressedThisFrame();

        /// <summary>The device that triggered the last Build input, used to ignore mouse clicks on UI buttons.</summary>
        public InputDevice BuildDevice => build.activeControl?.device;

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            build = new InputAction("Build", InputActionType.Button);
            build.AddBinding("<Keyboard>/space");
            build.AddBinding("<Mouse>/leftButton");
            build.AddBinding("<Gamepad>/buttonSouth");

            use = new InputAction("UseCola", InputActionType.Button);
            use.AddBinding("<Keyboard>/e");
            use.AddBinding("<Gamepad>/buttonWest");
        }

        void OnEnable()
        {
            move.Enable();
            build.Enable();
            use.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            build.Disable();
            use.Disable();
        }

        void OnDestroy()
        {
            move.Dispose();
            build.Dispose();
            use.Dispose();
        }
    }
}
