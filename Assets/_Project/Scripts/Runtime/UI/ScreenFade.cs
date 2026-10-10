using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A full-screen cover that starts opaque and fades away every time the game changes view (map &lt;-&gt; district), so the jump
    /// between the two far-apart scenes reads as a soft transition instead of a cut. It never blocks input.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        const float Seconds = 0.35f;

        [SerializeField] Image cover;

        float alpha;

        public void Bind(GameController game)
        {
            game.ViewChanged += Begin;
            cover.raycastTarget = false;
            Begin();   // the game opens with a fade-in too
        }

        void Begin()
        {
            alpha = 1f;
            Apply();
        }

        void Update()
        {
            if (alpha <= 0f) return;
            alpha = Mathf.MoveTowards(alpha, 0f, Time.unscaledDeltaTime / Seconds);
            Apply();
        }

        void Apply()
        {
            var colour = cover.color;
            colour.a = Mathf.SmoothStep(0f, 1f, alpha);
            cover.color = colour;
            cover.enabled = alpha > 0f;
        }
    }
}
