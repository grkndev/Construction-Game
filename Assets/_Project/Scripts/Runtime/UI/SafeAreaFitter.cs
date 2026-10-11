using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Keeps the HUD out of the notch, the Dynamic Island, rounded corners and the home indicator: everything on the canvas lives under this
    /// rectangle, which is stretched to <see cref="Screen.safeArea"/>. (Full-screen things, like the fade, stay on the canvas itself.)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect lastSafe;
        Vector2Int lastScreen;

        /// <summary>The anchors that make a rectangle cover the safe area of a screen of the given size.</summary>
        public static void Anchors(Rect safe, int screenWidth, int screenHeight, out Vector2 min, out Vector2 max)
        {
            if (screenWidth <= 0 || screenHeight <= 0 || safe.width <= 0f || safe.height <= 0f)
            {
                min = Vector2.zero;
                max = Vector2.one;
                return;
            }
            min = new Vector2(Mathf.Clamp01(safe.xMin / screenWidth), Mathf.Clamp01(safe.yMin / screenHeight));
            max = new Vector2(Mathf.Clamp01(safe.xMax / screenWidth), Mathf.Clamp01(safe.yMax / screenHeight));
        }

        void Awake() => rect = (RectTransform)transform;

        // The safe area changes with the device orientation (landscape left or right puts the notch on the other side) and with window resizes.
        void Update()
        {
            var safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == lastSafe && screen == lastScreen) return;
            lastSafe = safe;
            lastScreen = screen;
            Anchors(safe, screen.x, screen.y, out var min, out var max);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
