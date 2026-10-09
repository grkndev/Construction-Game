using System;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>A "Sarı Kola" can lying on the island: bobs, spins, blinks before it vanishes.</summary>
    public sealed class ColaPickup : MonoBehaviour
    {
        const float BlinkTime = 5f;

        float age;
        Transform model;
        Renderer[] renderers;
        public event Action<ColaPickup> Expired;

        public static ColaPickup Spawn(Vector3 position, ToonPalette palette)
        {
            var root = new GameObject("Sarı Kola");
            root.transform.position = position;
            var pickup = root.AddComponent<ColaPickup>();

            // Ground ring so it reads from far away, then the can: yellow body, red band, silver top and bottom.
            Prim.Cylinder(root.transform, "Ring", new Vector3(0f, 0.03f, 0f), new Vector3(1.5f, 0.02f, 1.5f), palette.marker);
            pickup.model = new GameObject("Can").transform;
            pickup.model.SetParent(root.transform, false);
            Prim.Cylinder(pickup.model, "Body", new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 0.38f, 0.5f), palette.marker);
            Prim.Cylinder(pickup.model, "Band", new Vector3(0f, 0.75f, 0f), new Vector3(0.53f, 0.12f, 0.53f), palette.roof);
            Prim.Cylinder(pickup.model, "Top", new Vector3(0f, 1.16f, 0f), new Vector3(0.42f, 0.03f, 0.42f), palette.hammerHead);
            Prim.Cylinder(pickup.model, "Bottom", new Vector3(0f, 0.34f, 0f), new Vector3(0.42f, 0.03f, 0.42f), palette.hammerHead);
            pickup.renderers = root.GetComponentsInChildren<Renderer>();
            return pickup;
        }

        void Update()
        {
            age += Time.deltaTime;
            var remaining = Balance.ColaLifetime - age;
            if (remaining <= 0f)
            {
                Expired?.Invoke(this);
                Destroy(gameObject);
                return;
            }

            // Pop in at the start; spin and bob forever.
            var pop = Easing.OutBack(age / 0.4f);
            model.localScale = Vector3.one * Mathf.Max(0.001f, pop);
            model.localPosition = Vector3.up * (0.12f + Mathf.Sin(Time.time * 3f) * 0.12f);
            model.Rotate(0f, 150f * Time.deltaTime, 0f, Space.Self);

            var visible = remaining > BlinkTime || Mathf.FloorToInt(remaining * 6f) % 2 == 0;
            foreach (var r in renderers) r.enabled = visible;
        }
    }
}
