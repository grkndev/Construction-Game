using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Keyboard and gamepad (the on-screen stick appears as a virtual gamepad) bound to the game's actions.
    /// Hammering is automatic, so there is no build action. Defined in code (not an .inputactions asset)
    /// because the game only needs these three; a full action map can replace this later.
    /// </summary>
    public sealed class GameInput : MonoBehaviour
    {
        InputAction move;
        InputAction use;
        InputAction shop;

        public Vector2 Move => move.ReadValue<Vector2>();
        public bool UsePressedThisFrame => use.WasPressedThisFrame();
        public bool ShopPressedThisFrame => shop.WasPressedThisFrame();

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

            use = new InputAction("UseCola", InputActionType.Button);
            use.AddBinding("<Keyboard>/e");
            use.AddBinding("<Gamepad>/buttonWest");

            shop = new InputAction("Shop", InputActionType.Button);
            shop.AddBinding("<Keyboard>/b");
            shop.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable()
        {
            move.Enable();
            use.Enable();
            shop.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            use.Disable();
            shop.Disable();
        }

        void OnDestroy()
        {
            move.Dispose();
            use.Dispose();
            shop.Dispose();
        }
    }
}
