using UnityEngine;

namespace Game.World
{
    /// <summary>A small camera-facing bar in the world (energy of the player or of a plot), built from two cubes.</summary>
    public sealed class MeterBar : MonoBehaviour
    {
        const float Padding = 0.05f;

        // Serialized because bars built by editor scripts (the player's) are saved into the scene and must survive it.
        [SerializeField] Transform fillPivot;
        [SerializeField] Renderer fillRenderer;
        [SerializeField] Material normalMaterial;
        [SerializeField] Material lowMaterial;
        [SerializeField] bool hideWhenFull;

        public static MeterBar Create(Transform parent, string name, Vector3 localPosition, float width, float height,
            Material back, Material normal, Material low, bool hideWhenFull)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.AddComponent<Billboard>();
            var bar = root.AddComponent<MeterBar>();

            Prim.NoShadow(Prim.Cube(root.transform, "Back", Vector3.zero, new Vector3(width, height, 0.06f), back));

            var innerWidth = width - Padding * 2f;
            bar.fillPivot = new GameObject("FillPivot").transform;
            bar.fillPivot.SetParent(root.transform, false);
            bar.fillPivot.localPosition = new Vector3(-innerWidth * 0.5f, 0f, -0.05f);
            var fill = Prim.NoShadow(Prim.Cube(bar.fillPivot, "Fill", new Vector3(innerWidth * 0.5f, 0f, 0f),
                new Vector3(innerWidth, height - Padding * 2f, 0.05f), normal));
            bar.fillRenderer = fill.GetComponent<Renderer>();
            bar.normalMaterial = normal;
            bar.lowMaterial = low;
            bar.hideWhenFull = hideWhenFull;
            return bar;
        }

        /// <summary>Stamina upgrades make the bar physically longer so the extra capacity is visible.</summary>
        public void SetWidthScale(float scale) => transform.localScale = new Vector3(scale, 1f, 1f);

        /// <summary>Red when nearly empty or exhausted so "I need to rest" is readable at a glance.</summary>
        public void Set(float fraction, bool exhausted)
        {
            fraction = Mathf.Clamp01(fraction);
            var visible = !hideWhenFull || fraction < 0.999f || exhausted;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) return;   // nobody sees it: skip the writes (characters call this every frame, most with a full bar)

            if (fraction != lastFraction)
            {
                lastFraction = fraction;
                fillPivot.localScale = new Vector3(Mathf.Max(0.0001f, fraction), 1f, 1f);
            }
            var low = exhausted || fraction < 0.25f;
            if (low != lastLow)
            {
                lastLow = low;
                fillRenderer.sharedMaterial = low ? lowMaterial : normalMaterial;
            }
        }

        float lastFraction = -1f;
        bool lastLow;
    }
}
