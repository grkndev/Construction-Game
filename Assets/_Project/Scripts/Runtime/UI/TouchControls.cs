using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Shows the on-screen joystick on touch devices only. Phones show it right away; elsewhere (a phone browser running the
    /// WebGL build, a touch laptop) it appears on the first touch, so desktop players never see it.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [SerializeField] GameObject joystick;

        void Start() => joystick.SetActive(Application.isMobilePlatform);

        void Update()
        {
            if (joystick.activeSelf) return;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) joystick.SetActive(true);
        }
    }
}
