using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.World
{
    public enum SiteState { Active, Done }

    /// <summary>
    /// One construction plot. The building is assembled from primitives at startup (floors, windows,
    /// roof, scaffolding) and floors pop in as <see cref="BuildProgress"/> crosses each threshold.
    /// </summary>
    public sealed class BuildSite : MonoBehaviour
    {
        public const float WorkRadius = 5.4f;
        const float FloorHeight = 1.1f;
        const float BaseHeight = 0.3f;
        const float Width = 3.4f;
        const float MaxScaffoldHeight = 4.6f;   // tall towers would otherwise hide the whole screen behind poles

        [SerializeField] ToonPalette palette;
        [SerializeField] ParticleSystem hitFx;
        [SerializeField] ParticleSystem completeFx;

        BuildPlan plan;
        BuildProgress progress;
        int index;
        SiteState state;

        Transform body;                 // everything that squashes when hit
        Transform barRoot, barFill, marker;
        TMP_Text label;
        GameObject scaffold;
        Transform roof;
        readonly List<Transform> floors = new List<Transform>();
        float punch;

        public BuildPlan Plan => plan;
        public SiteState State => state;
        public bool IsComplete => progress != null && progress.IsComplete;
        public float Fraction => progress?.Fraction ?? 0f;
        public int FloorsBuilt => progress?.FloorsBuilt ?? 0;
        public int Work => progress?.Work ?? 0;

        public event Action<BuildSite> Completed;

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
        }

        void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            var wall = palette.walls[index % palette.walls.Length];
            var totalHeight = BaseHeight + plan.Floors * FloorHeight;

            // Footprint collider so the player can't walk through the building.
            var footprint = gameObject.AddComponent<BoxCollider>();
            footprint.center = new Vector3(0f, 1.2f, 0f);
            footprint.size = new Vector3(Width + 0.3f, 2.4f, Width + 0.3f);

            Prim.Cube(body, "Slab", new Vector3(0f, BaseHeight * 0.5f, 0f), new Vector3(Width + 0.5f, BaseHeight, Width + 0.5f), palette.stone);

            for (var i = 0; i < plan.Floors; i++)
            {
                var floor = new GameObject($"Floor {i + 1}").transform;
                floor.SetParent(body, false);
                floor.localPosition = new Vector3(0f, BaseHeight + i * FloorHeight, 0f);
                Prim.Cube(floor, "Wall", new Vector3(0f, FloorHeight * 0.5f, 0f), new Vector3(Width, FloorHeight - 0.02f, Width), wall);

                // Two windows per face; a door replaces the middle of the front (camera-facing) side on floor 1.
                for (var side = 0; side < 4; side++)
                {
                    var rot = Quaternion.Euler(0f, side * 90f, 0f);
                    var isDoorSide = i == 0 && side == 0;
                    if (isDoorSide)
                    {
                        Prim.Cube(floor, "Door", rot * new Vector3(0f, 0.42f, -(Width * 0.5f + 0.01f)),
                            new Vector3(0.75f, 0.84f, 0.1f), palette.door).transform.localRotation = rot;
                        continue;
                    }
                    foreach (var dx in new[] { -0.8f, 0.8f })
                    {
                        Prim.Cube(floor, "Window", rot * new Vector3(dx, 0.58f, -(Width * 0.5f + 0.01f)),
                            new Vector3(0.62f, 0.5f, 0.1f), palette.window).transform.localRotation = rot;
                    }
                }
                floor.gameObject.SetActive(false);
                floors.Add(floor);
            }

            roof = new GameObject("Roof").transform;
            roof.SetParent(body, false);
            roof.localPosition = new Vector3(0f, totalHeight, 0f);
            Prim.Cube(roof, "Cap", new Vector3(0f, 0.17f, 0f), new Vector3(Width + 0.5f, 0.34f, Width + 0.5f), palette.roof);
            Prim.Cylinder(roof, "Pole", new Vector3(0.9f, 0.9f, 0.9f), new Vector3(0.08f, 0.55f, 0.08f), palette.scaffold);
            Prim.Cube(roof, "Flag", new Vector3(1.2f, 1.25f, 0.9f), new Vector3(0.55f, 0.32f, 0.05f), palette.flag);
            roof.gameObject.SetActive(false);

            BuildScaffold(Mathf.Min(totalHeight + 0.35f, MaxScaffoldHeight));
            BuildBar();
        }

        void BuildScaffold(float height)
        {
            scaffold = new GameObject("Scaffold");
            scaffold.transform.SetParent(transform, false);
            var half = Width * 0.5f + 0.15f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Prim.Cylinder(scaffold.transform, "Post", new Vector3(sx * half, height * 0.5f, sz * half),
                    new Vector3(0.14f, height * 0.5f, 0.14f), palette.scaffold);

            // Top rails make the future building's silhouette readable from the start.
            foreach (var s in new[] { -1f, 1f })
            {
                Prim.Cube(scaffold.transform, "RailX", new Vector3(0f, height, s * half), new Vector3(half * 2f, 0.1f, 0.1f), palette.scaffold);
                Prim.Cube(scaffold.transform, "RailZ", new Vector3(s * half, height, 0f), new Vector3(0.1f, 0.1f, half * 2f), palette.scaffold);
            }

            // Thin poles cast long, noisy shadow lines across the island; the scaffold reads fine without them.
            foreach (var r in scaffold.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildBar()
        {
            // Floats in front of the corner facing the camera (not above the centre), so a growing tower can't hide it.
            barRoot = new GameObject("ProgressBar").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(-2.3f, 4.9f, -2.3f);
            barRoot.gameObject.AddComponent<Billboard>();

            Prim.Cube(barRoot, "Back", Vector3.zero, new Vector3(3.2f, 0.34f, 0.08f), palette.barBack);
            var pivot = new GameObject("FillPivot").transform;
            pivot.SetParent(barRoot, false);
            pivot.localPosition = new Vector3(-1.45f, 0f, -0.06f);
            barFill = pivot;
            Prim.Cube(pivot, "Fill", new Vector3(1.45f, 0f, 0f), new Vector3(2.9f, 0.2f, 0.06f), palette.barFill);

            if (label != null)
            {
                label.transform.SetParent(barRoot, false);
                label.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                label.text = plan.Name;
            }

            marker = Prim.Cube(barRoot, "Marker", new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), palette.marker).transform;
            marker.localRotation = Quaternion.Euler(45f, 0f, 45f);
            SetBarFraction(0f);
        }

        public void SetState(SiteState next)
        {
            state = next;
            var active = next == SiteState.Active;
            var done = next == SiteState.Done;
            barRoot.gameObject.SetActive(!done);
            marker.gameObject.SetActive(active);
            scaffold.SetActive(!done);
            if (label != null) label.alpha = 1f;
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

        void SetBarFraction(float f)
        {
            barFill.localScale = new Vector3(Mathf.Max(0.0001f, f), 1f, 1f);
        }

        void OnFloorBuilt(int floorNumber)
        {
            var floor = floors[floorNumber - 1];
            floor.gameObject.SetActive(true);
            StartCoroutine(PopIn(floor, 0.5f));
        }

        void OnCompleted()
        {
            roof.gameObject.SetActive(true);
            StartCoroutine(PopIn(roof, 0.6f));
            if (completeFx != null) completeFx.Play();
            SetState(SiteState.Done);
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

            // Squash-and-stretch reaction to each hammer hit, around the base of the building.
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 6f);
            var s = Mathf.Sin(punch * Mathf.PI);
            body.localScale = new Vector3(1f + s * 0.05f, 1f - s * 0.06f, 1f + s * 0.05f);

            if (marker != null && marker.gameObject.activeSelf)
            {
                marker.localPosition = new Vector3(0f, 1.5f + Mathf.Sin(Time.time * 4f) * 0.18f, 0f);
                marker.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            }
        }

        /// <summary>Where the nth auto-worker should stand (spread around the building).</summary>
        public Vector3 WorkSlot(int n)
        {
            var angle = (n * 137.5f + 200f) * Mathf.Deg2Rad;
            var p = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3.3f;
            p.y = 0f;
            return p;
        }
    }
}
