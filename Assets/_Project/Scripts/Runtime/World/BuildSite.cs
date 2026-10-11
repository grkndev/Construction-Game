using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.World
{
    public enum SiteState { Active, Done, Broken }

    /// <summary>
    /// One construction plot. The building is assembled from primitives at startup (see <see cref="BuildingKits"/>: a model
    /// per building kind, scaffolding around it) and its stages pop in as <see cref="BuildProgress"/> crosses each threshold.
    /// </summary>
    public sealed class BuildSite : MonoBehaviour
    {
        public const float WorkRadius = 6.2f;
        const float BaseHeight = BuildingKits.Base;
        const float MaxScaffoldHeight = 4.6f;   // tall towers would otherwise hide the whole screen behind poles

        [SerializeField] ToonPalette palette;
        [SerializeField] ParticleSystem hitFx;
        [SerializeField] ParticleSystem completeFx;

        BuildPlan plan;
        BuildProgress progress;
        int index;
        SiteState state;

        Transform body;                 // everything that squashes when hit
        Transform model;                // the building itself (slab, floors, roof), scaled by the plan's size
        bool fused;                     // a finished building's stages have been merged into one set of meshes
        Vector2 footprint;              // x/z size of the finished building, world units
        Transform barRoot, barFill, marker;
        TMP_Text label;
        GameObject scaffold;
        Transform roof;
        Transform flag;
        Renderer markerRenderer;
        readonly List<Transform> floors = new List<Transform>();
        float punch;
        float barPulse;   // 1 right after a floor is built, fades to 0 (the progress bar swells)

        public BuildPlan Plan => plan;
        public SiteState State => state;
        public bool IsComplete => progress != null && progress.IsComplete;
        public float Fraction => progress?.Fraction ?? 0f;
        public int FloorsBuilt => progress?.FloorsBuilt ?? 0;
        public int Work => progress?.Work ?? 0;
        /// <summary>Under construction or waiting for a repair (false once the building is finished and fine).</summary>
        public bool NeedsWork => state != SiteState.Done;
        /// <summary>Kind of the current problem (1..Balance.RepairKinds), 0 when the building is fine.</summary>
        public int Issue { get; private set; }

        public event Action<BuildSite> Completed;
        /// <summary>A floor just appeared (not raised when a saved building is restored).</summary>
        public event Action<BuildSite, int> FloorBuilt;   // with the floor number, counting from 1

        /// <summary>How many auto-workers currently work here; the dispatcher sends new ones to the emptiest plot.</summary>
        public int AssignedWorkers { get; private set; }
        int nextSlot;

        public int ClaimSlot()
        {
            AssignedWorkers++;
            return nextSlot++;
        }

        public void ReleaseWorker() => AssignedWorkers = Mathf.Max(0, AssignedWorkers - 1);

        public void Initialize(BuildPlan buildPlan, int plotIndex, TMP_Text nameLabel)
        {
            plan = buildPlan;
            index = plotIndex;
            label = nameLabel;
            progress = new BuildProgress(plan);
            progress.FloorBuilt += OnFloorBuilt;
            progress.Completed += OnCompleted;

            Build();
            SetState(SiteState.Active);
        }

        /// <summary>Puts a saved building back as it was: floors (and the roof when finished) are simply there, no pop-in or confetti.</summary>
        public void Restore(int work)
        {
            progress.Restore(work);
            for (var i = 0; i < progress.FloorsBuilt; i++) floors[i].gameObject.SetActive(true);
            SetBarFraction(progress.Fraction);
            if (!progress.IsComplete) return;
            roof.gameObject.SetActive(true);
            SetState(SiteState.Done);
            FuseFinished();
        }

        // A finished building never changes again, so its slab, floors and roof (merged separately so they could pop in one by one) become one
        // mesh per colour: a 25-draw building is then ~6. The flag (it droops when broken) and any turning blades stay separate.
        void FuseFinished()
        {
            if (fused || model == null) return;
            fused = true;
            foreach (var floor in floors) floor.localScale = Vector3.one;   // never bake a pop-in that was cut short
            roof.localScale = Vector3.one;
            MeshCombiner.Combine(model, t => t == flag || t.GetComponent<Spin>() != null);
        }

        // The pop-in coroutine stops when a district is hidden: if a building was finished and left in the middle of it, tidy up on return.
        void OnEnable()
        {
            if (state != SiteState.Active && IsComplete) FuseFinished();
        }

        IEnumerator FuseAfterPop()
        {
            yield return new WaitForSeconds(1f);   // the roof's pop-in takes 0.6 s
            FuseFinished();
        }

        void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            model = new GameObject("Model").transform;
            model.SetParent(body, false);
            model.localScale = Vector3.one * plan.Size;

            var wall = palette.walls[index % palette.walls.Length];
            var dim = BuildingKits.BaseDim(plan.Kind);
            footprint = dim * plan.Size;

            // Footprint collider so the player can't walk through the building.
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.2f, 0f);
            box.size = new Vector3(footprint.x + 0.3f, 2.4f, footprint.y + 0.3f);

            // Ships are built in a wooden dry dock, everything else on a stone slab.
            Prim.Cube(model, "Slab", new Vector3(0f, BaseHeight * 0.5f, 0f), new Vector3(dim.x + 0.5f, BaseHeight, dim.y + 0.5f),
                plan.Kind == BuildingKind.Ship ? palette.crate : palette.stone);

            BuildingKits.Build(model, plan, palette, wall, floors, out roof, out flag, out var top);

            // ~70 primitives per building become a few merged meshes per stage; the flag (droops when broken) and turning blades stay separate.
            foreach (var stage in floors) MeshCombiner.Combine(stage, t => t == flag);
            MeshCombiner.Combine(roof, t => t == flag || t.GetComponent<Spin>() != null);

            BuildScaffold(Mathf.Min(top * plan.Size + 0.35f, MaxScaffoldHeight));
            BuildBar();
        }

        void BuildScaffold(float height)
        {
            scaffold = new GameObject("Scaffold");
            scaffold.transform.SetParent(transform, false);
            var hx = footprint.x * 0.5f + 0.15f;
            var hz = footprint.y * 0.5f + 0.15f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Prim.Cylinder(scaffold.transform, "Post", new Vector3(sx * hx, height * 0.5f, sz * hz),
                    new Vector3(0.14f, height * 0.5f, 0.14f), palette.scaffold);

            // Top rails make the future building's silhouette readable from the start.
            foreach (var s in new[] { -1f, 1f })
            {
                Prim.Cube(scaffold.transform, "RailX", new Vector3(0f, height, s * hz), new Vector3(hx * 2f, 0.1f, 0.1f), palette.scaffold);
                Prim.Cube(scaffold.transform, "RailZ", new Vector3(s * hx, height, 0f), new Vector3(0.1f, 0.1f, hz * 2f), palette.scaffold);
            }

            // Thin poles cast long, noisy shadow lines across the island; the scaffold reads fine without them.
            foreach (var r in scaffold.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshCombiner.Combine(scaffold.transform);   // eight poles and rails: one mesh
        }

        void BuildBar()
        {
            // Floats in front of the corner facing the camera (not above the centre), so a growing tower can't hide it.
            barRoot = new GameObject("ProgressBar").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(-(footprint.x * 0.5f + 0.6f), 4.9f, -(footprint.y * 0.5f + 0.6f));
            barRoot.gameObject.AddComponent<Billboard>();

            Prim.NoShadow(Prim.Cube(barRoot, "Back", Vector3.zero, new Vector3(3.2f, 0.34f, 0.08f), palette.barBack));
            var pivot = new GameObject("FillPivot").transform;
            pivot.SetParent(barRoot, false);
            pivot.localPosition = new Vector3(-1.45f, 0f, -0.06f);
            barFill = pivot;
            Prim.NoShadow(Prim.Cube(pivot, "Fill", new Vector3(1.45f, 0f, 0f), new Vector3(2.9f, 0.2f, 0.06f), palette.barFill));

            if (label != null)
            {
                label.transform.SetParent(barRoot, false);
                label.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                label.text = plan.Name;
            }

            marker = Prim.NoShadow(Prim.Cube(barRoot, "Marker", new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), palette.marker)).transform;
            markerRenderer = marker.GetComponent<Renderer>();
            marker.localRotation = Quaternion.Euler(45f, 0f, 45f);
            SetBarFraction(0f);
        }

        public void SetState(SiteState next)
        {
            state = next;
            var active = next == SiteState.Active;
            var done = next == SiteState.Done;
            var broken = next == SiteState.Broken;
            barRoot.gameObject.SetActive(!done);
            marker.gameObject.SetActive(active || broken);
            scaffold.SetActive(active);
            flag.localRotation = broken ? Quaternion.Euler(0f, 0f, 35f) : Quaternion.identity;   // a drooping flag reads as "something is wrong"
            if (label != null)
            {
                label.alpha = 1f;
                label.text = broken ? StoryBeats.Problem(plan.Name, Issue) : plan.Name;
            }
        }

        /// <summary>
        /// Gives a finished building a problem (kind 1..N) or fixes it (0). A broken building shows a coloured marker and a
        /// repair bar, and is worked on like a plot under construction.
        /// </summary>
        public void SetIssue(int kind)
        {
            Issue = progress != null && progress.IsComplete ? kind : 0;
            if (Issue == 0)
            {
                if (state == SiteState.Broken) SetState(SiteState.Done);
                return;
            }
            markerRenderer.sharedMaterial = IssueMaterial(Issue);
            SetBarFraction(0f);
            SetState(SiteState.Broken);
        }

        /// <summary>One hammer hit while repairing: shows the repair progress (0..1) and the hit effect.</summary>
        public void RepairHit(float fraction, Vector3 hitPoint)
        {
            SetBarFraction(fraction);
            punch = 1f;
            if (hitFx == null) return;
            hitFx.transform.position = hitPoint;
            hitFx.Emit(7);
        }

        Material IssueMaterial(int kind)
        {
            switch (kind)
            {
                case 1: return palette.window;   // leak: light blue
                case 2: return palette.sea;      // broken pipe: deep blue
                case 3: return palette.marker;   // wiring: yellow
                default: return palette.cone;    // cracked wall: orange
            }
        }

        /// <summary>Applies work and returns how much was used. 0 if the plot isn't the active one.</summary>
        public int AddWork(int work, Vector3 hitPoint)
        {
            if (state != SiteState.Active) return 0;
            var applied = progress.Add(work);
            if (applied == 0) return 0;

            SetBarFraction(progress.Fraction);
            punch = 1f;
            if (hitFx != null)
            {
                hitFx.transform.position = hitPoint;
                hitFx.Emit(7);
            }
            return applied;
        }

        /// <summary>A short swell of the progress bar (called when a floor is built).</summary>
        public void PulseBar() => barPulse = 1f;

        /// <summary>The middle of the building's base and how far it reaches, for effects that surround it (floor rings, sparkles).</summary>
        public Vector3 BasePoint => transform.position;
        /// <summary>Half the footprint (x, z): the finished building stands within this rectangle around the plot's centre.</summary>
        public Vector2 FootprintHalf => footprint * 0.5f;

        /// <summary>A spot just outside the building's wall in the given flat direction, <paramref name="gap"/> away from it.</summary>
        public Vector3 SpotOutside(Vector3 direction, float gap)
        {
            var dir = new Vector2(direction.x, direction.z);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
            var distance = Core.FootprintMath.EdgeDistance(FootprintHalf, dir) + gap;
            var flat = dir.normalized * distance;
            return transform.position + new Vector3(flat.x, 0f, flat.y);
        }

        public float EffectRadius => Mathf.Max(footprint.x, footprint.y) * 0.55f;

        void SetBarFraction(float f)
        {
            barFill.localScale = new Vector3(Mathf.Max(0.0001f, f), 1f, 1f);
        }

        void OnFloorBuilt(int floorNumber)
        {
            var floor = floors[floorNumber - 1];
            floor.gameObject.SetActive(true);
            StartCoroutine(PopIn(floor, 0.5f));
            FloorBuilt?.Invoke(this, floorNumber);
        }

        void OnCompleted()
        {
            roof.gameObject.SetActive(true);
            StartCoroutine(PopIn(roof, 0.6f));
            if (completeFx != null) completeFx.Play();
            SetState(SiteState.Done);
            StartCoroutine(FuseAfterPop());
            Completed?.Invoke(this);
        }

        // Scales Y from 0 to 1 with an overshoot (the cartoony "stack" pop) while squeezing X/Z slightly.
        static IEnumerator PopIn(Transform t, float duration)
        {
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var k = Easing.OutBack(time / duration);
                t.localScale = new Vector3(1f + (1f - k) * -0.15f, Mathf.Max(0.001f, k), 1f + (1f - k) * -0.15f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        void Update()
        {
            if (body == null) return;

            // Squash-and-stretch reaction to each hammer hit, around the base of the building. Only while it plays: rewriting the scale every frame
            // of every idle building dirties its transform (and so its bounds and renderer data) for nothing.
            if (punch > 0f)
            {
                punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 6f);
                var s = Mathf.Sin(punch * Mathf.PI);
                body.localScale = new Vector3(1f + s * 0.05f, 1f - s * 0.06f, 1f + s * 0.05f);
            }

            if (barPulse > 0f)
            {
                barPulse = Mathf.MoveTowards(barPulse, 0f, Time.deltaTime * 3.5f);
                barRoot.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(barPulse * Mathf.PI));
            }

            if (marker != null && marker.gameObject.activeSelf)
            {
                marker.localPosition = new Vector3(0f, 1.5f + Mathf.Sin(Time.time * 4f) * 0.18f, 0f);
                marker.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            }
        }

        /// <summary>Where the nth auto-worker should stand (spread around the building).</summary>
        public Vector3 WorkSlot(int n)
        {
            // Around the plot, a fixed gap outside the wall in that direction (an ellipse put the corner slots of wide buildings inside the walls).
            var angle = (n * 137.5f + 200f) * Mathf.Deg2Rad;
            return SpotOutside(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)), 1.3f);
        }
    }
}
