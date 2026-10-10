using System.Collections;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// One decoration slot while it is being built: a small scaffold with a progress bar, and the helpers hammering around it. When the work is
    /// done the scaffold goes and the finished decoration pops up. Also builds the finished decoration directly (a restored save).
    /// </summary>
    public sealed class DecorationSite : MonoBehaviour
    {
        GameObject scaffold;
        Transform bar;
        ToonPalette palette;
        int kind;

        /// <summary>The finished decoration (null while it is still being built).</summary>
        public GameObject Model { get; private set; }
        public Vector3[] Hangouts { get; private set; } = new Vector3[0];

        /// <summary>Starts a scaffold at <paramref name="slot"/> for decoration <paramref name="decorationKind"/>.</summary>
        public static DecorationSite Begin(Transform slot, int decorationKind, ToonPalette p, float progress)
        {
            var go = new GameObject($"Decoration site {decorationKind + 1}");
            go.transform.SetParent(slot, false);
            var site = go.AddComponent<DecorationSite>();
            site.palette = p;
            site.kind = decorationKind;
            site.BuildScaffold();
            site.SetProgress(progress);
            return site;
        }

        /// <summary>The finished decoration straight away, without the pop (a save that already had it).</summary>
        public static DecorationSite Finished(Transform slot, int decorationKind, ToonPalette p)
        {
            var go = new GameObject($"Decoration {decorationKind + 1}");
            go.transform.SetParent(slot, false);
            var site = go.AddComponent<DecorationSite>();
            site.palette = p;
            site.kind = decorationKind;
            site.ShowModel(animate: false);
            return site;
        }

        void BuildScaffold()
        {
            scaffold = new GameObject("Scaffold");
            scaffold.transform.SetParent(transform, false);
            const float half = 2.6f, height = 2.8f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Prim.Cylinder(scaffold.transform, "Post", new Vector3(sx * half, height * 0.5f, sz * half), new Vector3(0.14f, height * 0.5f, 0.14f), palette.scaffold);
            foreach (var s in new[] { -1f, 1f })
            {
                Prim.Cube(scaffold.transform, "RailX", new Vector3(0f, height, s * half), new Vector3(half * 2f, 0.1f, 0.1f), palette.scaffold);
                Prim.Cube(scaffold.transform, "RailZ", new Vector3(s * half, height, 0f), new Vector3(0.1f, 0.1f, half * 2f), palette.scaffold);
            }
            Prim.Cylinder(scaffold.transform, "Ground", new Vector3(0f, 0.02f, 0f), new Vector3(6.4f, 0.02f, 6.4f), palette.dirt);
            var block = scaffold.AddComponent<BoxCollider>();   // solid while it is being built; the helpers work from outside
            block.center = new Vector3(0f, 1.2f, 0f);
            block.size = new Vector3(5.4f, 2.4f, 5.4f);
            foreach (var r in scaffold.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Progress bar floating over the middle, always facing the camera.
            var barRoot = new GameObject("ProgressBar").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, 4.4f, 0f);
            barRoot.gameObject.AddComponent<Billboard>();
            Prim.Cube(barRoot, "Back", Vector3.zero, new Vector3(3.2f, 0.34f, 0.08f), palette.barBack);
            var pivot = new GameObject("FillPivot").transform;
            pivot.SetParent(barRoot, false);
            pivot.localPosition = new Vector3(-1.45f, 0f, -0.06f);
            bar = pivot;
            Prim.Cube(pivot, "Fill", new Vector3(1.45f, 0f, 0f), new Vector3(2.9f, 0.2f, 0.06f), palette.barFill);
        }

        /// <summary>0..1.</summary>
        public void SetProgress(float fraction)
        {
            if (bar != null) bar.localScale = new Vector3(Mathf.Max(0.0001f, fraction), 1f, 1f);
        }

        /// <summary>Where the nth helper stands to work (spread around the scaffold).</summary>
        public Vector3 WorkSlot(int n)
        {
            // The scaffold is a 5.4 x 5.4 block: stand a gap outside its wall in that direction (a circle put the corner slots inside it).
            var angle = (n * 137.5f + 40f) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var flat = dir * (Core.FootprintMath.EdgeDistance(new Vector2(2.7f, 2.7f), dir) + 1.1f);
            return transform.position + new Vector3(flat.x, 0f, flat.y);
        }

        /// <summary>The work is done: the scaffold goes, the decoration appears.</summary>
        public void Finish() => ShowModel(animate: true);

        void ShowModel(bool animate)
        {
            if (scaffold != null) Destroy(scaffold);
            if (bar != null) Destroy(bar.parent.gameObject);
            bar = null;
            Model = DecorationKits.Build(transform, kind, palette, out var hangouts);
            // Hangout spots are local to the decoration; the helpers need world positions.
            Hangouts = hangouts;
            for (var i = 0; i < Hangouts.Length; i++) Hangouts[i] = transform.TransformPoint(Hangouts[i]);
            MeshCombiner.Combine(Model.transform);
            if (animate) StartCoroutine(PopIn(Model.transform));
        }

        static IEnumerator PopIn(Transform t)
        {
            for (var time = 0f; time < 0.6f; time += Time.deltaTime)
            {
                var k = Core.Easing.OutBack(time / 0.6f);
                t.localScale = new Vector3(1f + (1f - k) * -0.2f, Mathf.Max(0.001f, k), 1f + (1f - k) * -0.2f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }
    }
}
