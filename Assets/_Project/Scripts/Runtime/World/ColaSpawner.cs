using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>Drops a Sarı Kola somewhere on the island every so often and lets the player pick it up by walking over it.</summary>
    public sealed class ColaSpawner : MonoBehaviour
    {
        const float PickupRadius = 1.7f;
        const float MinPlotDistance = 5.6f;
        const float MinColaDistance = 4f;

        [SerializeField] GameController game;
        [SerializeField] Transform player;
        [SerializeField] ToonPalette palette;

        readonly List<ColaPickup> alive = new List<ColaPickup>();
        float timer;
        bool announced;

        void Start()
        {
            timer = Balance.ColaFirstSpawn;
            game.ModeChanged += OnModeChanged;
        }

        void OnDestroy()
        {
            if (game != null) game.ModeChanged -= OnModeChanged;
        }

        // Cans belong to the district they were dropped in: leaving (or entering) starts from a clean slate.
        void OnModeChanged()
        {
            foreach (var pickup in alive) if (pickup != null) Destroy(pickup.gameObject);
            alive.Clear();
            timer = Balance.ColaFirstSpawn;
        }

        void Update()
        {
            if (!game.InDistrict) return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = Random.Range(Balance.ColaSpawnMin, Balance.ColaSpawnMax);
                if (alive.Count < Balance.MaxColaOnMap) TrySpawn();
            }

            for (var i = alive.Count - 1; i >= 0; i--)
            {
                var pickup = alive[i];
                if (pickup == null) { alive.RemoveAt(i); continue; }
                var d = Vector3.ProjectOnPlane(pickup.transform.position - player.position, Vector3.up).magnitude;
                if (d > PickupRadius) continue;
                if (!game.TryAddCola()) continue;   // inventory full: leave it lying there
                alive.RemoveAt(i);
                Destroy(pickup.gameObject);
            }
        }

        void TrySpawn()
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var angle = Random.value * Mathf.PI * 2f;
                var radius = Mathf.Sqrt(Random.Range(0.1f, 1f)) * game.CurrentDistrict.WalkRadius * 0.9f;
                var point = game.CurrentDistrict.Center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!IsFree(point)) continue;

                var pickup = ColaPickup.Spawn(point, palette);
                pickup.Expired += p => alive.Remove(p);
                alive.Add(pickup);
                if (!announced)
                {
                    announced = true;
                    game.ShowHint("Haritada bir Sarı Kola belirdi! Üstünden geç, sonra iç.");
                }
                return;
            }
        }

        bool IsFree(Vector3 point)
        {
            foreach (var site in game.Sites)
                if ((site.transform.position - point).magnitude < MinPlotDistance) return false;
            foreach (var other in alive)
                if (other != null && (other.transform.position - point).magnitude < MinColaDistance) return false;
            return (player.position - point).magnitude > 3f;   // never right under the player's feet
        }
    }
}
