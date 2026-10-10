using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>A treasure chest lying in the district: bobs and winks until someone walks over it, then the lid flips open and coins fly out. Vanishes if nobody comes.</summary>
    public sealed class TreasureChest : MonoBehaviour
    {
        const float Lifetime = 45f, BlinkTime = 6f, OpenRadius = 1.9f, OpenSeconds = 0.45f, StaySeconds = 1.3f;

        GameController game;
        Transform player;
        Transform model;
        Transform lid;
        Renderer[] renderers;
        float age;
        float openTime = -1f;

        public bool IsOpen => openTime >= 0f;

        public static TreasureChest Spawn(Vector3 position, ToonPalette p, GameController owner, Transform playerTransform)
        {
            var root = new GameObject("Hazine sandığı");
            root.transform.position = position;
            var chest = root.AddComponent<TreasureChest>();
            chest.game = owner;
            chest.player = playerTransform;

            // A ground ring so it reads from far away, like the cola's.
            Prim.Cylinder(root.transform, "Ring", new Vector3(0f, 0.03f, 0f), new Vector3(2.4f, 0.02f, 2.4f), p.yellow);
            chest.model = new GameObject("Chest").transform;
            chest.model.SetParent(root.transform, false);
            Prim.Cube(chest.model, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.9f, 1.05f), p.crate);
            Prim.Cube(chest.model, "BandL", new Vector3(-0.45f, 0.46f, 0f), new Vector3(0.14f, 0.94f, 1.1f), p.yellow);
            Prim.Cube(chest.model, "BandR", new Vector3(0.45f, 0.46f, 0f), new Vector3(0.14f, 0.94f, 1.1f), p.yellow);

            // The lid hangs on a hinge along the back edge.
            chest.lid = new GameObject("Lid").transform;
            chest.lid.SetParent(chest.model, false);
            chest.lid.localPosition = new Vector3(0f, 0.9f, 0.5f);
            Prim.Cube(chest.lid, "LidTop", new Vector3(0f, 0.17f, -0.5f), new Vector3(1.56f, 0.34f, 1.1f), p.trunk);
            Prim.Cube(chest.lid, "LidBandL", new Vector3(-0.45f, 0.18f, -0.5f), new Vector3(0.14f, 0.38f, 1.14f), p.yellow);
            Prim.Cube(chest.lid, "LidBandR", new Vector3(0.45f, 0.18f, -0.5f), new Vector3(0.14f, 0.38f, 1.14f), p.yellow);
            Prim.Sphere(chest.lid, "Lock", new Vector3(0f, 0.05f, -1.06f), Vector3.one * 0.3f, p.yellow);
            chest.renderers = root.GetComponentsInChildren<Renderer>();
            return chest;
        }

        void Update()
        {
            age += Time.deltaTime;

            if (IsOpen)
            {
                openTime += Time.deltaTime;
                lid.localRotation = Quaternion.Euler(-Mathf.Lerp(0f, 105f, Easing.OutBack(openTime / OpenSeconds)), 0f, 0f);
                if (openTime > StaySeconds) Destroy(gameObject);
                return;
            }

            var remaining = Lifetime - age;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            var pop = Easing.OutBack(age / 0.4f);
            model.localScale = Vector3.one * Mathf.Max(0.001f, pop);
            model.localPosition = Vector3.up * (0.05f + Mathf.Abs(Mathf.Sin(Time.time * 2.4f)) * 0.14f);
            lid.localRotation = Quaternion.Euler(-Mathf.Max(0f, Mathf.Sin(Time.time * 2.4f * 3f) - 0.6f) * 22f, 0f, 0f);   // a little rattle, as if it wants out

            var visible = remaining > BlinkTime || Mathf.FloorToInt(remaining * 6f) % 2 == 0;
            foreach (var r in renderers) r.enabled = visible;

            if (Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).magnitude < OpenRadius) Open();
        }

        void Open()
        {
            openTime = 0f;
            foreach (var r in renderers) r.enabled = true;
            var point = transform.position + Vector3.up * 1.2f;
            game.GrantCoins(point, game.ChestReward);
            game.CurrentDistrict?.Fx?.Sparkles(transform.position, 2.4f, 1.8f);
            game.PlaySfx(SfxKind.Buy);
        }
    }
}
