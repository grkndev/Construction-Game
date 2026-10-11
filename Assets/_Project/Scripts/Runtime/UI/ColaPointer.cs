using Game.Core;
using Game.World;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// An arrow at the edge of the screen pointing towards the nearest collectable of a <see cref="PickupSource"/> (a Sarı Kola, a treasure chest)
    /// while it is out of sight, with its distance. Hidden when it is on screen, when none exists, or when collecting makes no sense.
    /// </summary>
    public sealed class ColaPointer : MonoBehaviour
    {
        const float EdgeMarginX = 0.08f, EdgeMarginY = 0.13f;   // fractions of the screen: how far from the edge the arrow sits

        [SerializeField] GameController game;
        [SerializeField] PickupSource spawner;
        [SerializeField] string label = "Kola";
        [SerializeField] RectTransform root;     // moved to the screen edge
        [SerializeField] RectTransform arrow;    // turned towards the can
        [SerializeField] TMP_Text distance;

        Camera cam;
        int lastDistance = -1;

        void Update()
        {
            var show = false;
            if (game.InDistrict && spawner.Wanted && spawner.TryNearest(out var can, out var metres))
            {
                if (cam == null) cam = Camera.main;
                if (cam != null) show = Place(can, metres);
            }
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        }

        bool Place(Vector3 can, float metres)
        {
            var view = cam.WorldToViewportPoint(can + Vector3.up);
            var onScreen = view.z > 0f && view.x > 0.04f && view.x < 0.96f && view.y > 0.08f && view.y < 0.92f;
            if (onScreen) return false;   // the can itself is visible: no arrow needed

            // Direction from the middle of the screen; a point behind the camera is mirrored so the arrow still points the right way.
            var from = new Vector2(0.5f, 0.5f);
            var dir = new Vector2(view.x - from.x, view.y - from.y);
            if (view.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
            dir.Normalize();

            // Around the middle of the safe area (not the whole screen), so the arrow never hides under a notch or the home indicator.
            var safe = Screen.safeArea;
            var halfW = safe.width * (0.5f - EdgeMarginX);
            var halfH = safe.height * (0.5f - EdgeMarginY);
            var t = Mathf.Min(Mathf.Abs(dir.x) > 1e-4f ? halfW / Mathf.Abs(dir.x) : float.MaxValue, Mathf.Abs(dir.y) > 1e-4f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
            root.position = safe.center + dir * t;
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            root.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));

            var rounded = Mathf.RoundToInt(metres);
            if (rounded != lastDistance)
            {
                lastDistance = rounded;
                distance.text = $"{label}  {rounded} m";
            }
            return true;
        }
    }
}
