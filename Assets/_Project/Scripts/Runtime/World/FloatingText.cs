using TMPro;
using UnityEngine;

namespace Game.World
{
    /// <summary>"+5" popups: rise, ease out, fade, then clean themselves up.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        const float Lifetime = 0.9f;
        const float Rise = 1.8f;

        [SerializeField] TMP_Text label;

        float age;
        Vector3 start;
        Color color;

        public void Show(string text, Color tint, float scale = 1f)
        {
            label.text = text;
            color = tint;
            label.color = tint;
            transform.localScale = Vector3.one * scale;
            start = transform.position;
        }

        void Update()
        {
            age += Time.deltaTime;
            var t = age / Lifetime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = start + Vector3.up * (Rise * Core.Easing.OutCubic(t));
            color.a = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
            label.color = color;
        }
    }
}
