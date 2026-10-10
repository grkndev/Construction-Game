using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Now and then something small happens in the district being visited: a treasure chest appears (the screen-edge arrow points to it), a flock
    /// of birds flies over or lands, a stray cat or dog wanders by. At most one of each at a time; everything goes away when the district is left.
    /// </summary>
    public sealed class EventSpawner : PickupSource
    {
        const float MinPlotDistance = 5.6f;

        [SerializeField] GameController game;
        [SerializeField] Transform player;
        [SerializeField] ToonPalette palette;

        readonly System.Random random = new System.Random();
        TreasureChest chest;
        BirdFlock birds;
        Pet pet;
        float timer;
        bool announcedChest;

        void Start()
        {
            timer = EventPicker.Delay(random, first: true);
            game.ModeChanged += OnModeChanged;
        }

        void OnDestroy()
        {
            if (game != null) game.ModeChanged -= OnModeChanged;
        }

        // Events belong to the district they happened in: leaving (or entering) starts clean.
        void OnModeChanged()
        {
            if (chest != null) Destroy(chest.gameObject);
            if (birds != null) Destroy(birds.gameObject);
            if (pet != null) Destroy(pet.gameObject);
            timer = EventPicker.Delay(random, first: true);
        }

        void Update()
        {
            if (!game.InDistrict) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            var next = EventPicker.Pick(random, chest != null, birds != null, pet != null);
            if (next == null)
            {
                timer = 10f;   // everything is already out and about: look again soon
                return;
            }
            Spawn(next.Value);
            timer = EventPicker.Delay(random, first: false);
        }

        void Spawn(EventKind kind)
        {
            var district = game.CurrentDistrict;
            switch (kind)
            {
                case EventKind.Chest:
                    if (!SpawnPoints.TryFind(district, MinPlotDistance, point => (player.position - point).magnitude > 4f, out var spot)) return;
                    chest = TreasureChest.Spawn(spot, palette, game, player);
                    if (!announcedChest)
                    {
                        announcedChest = true;
                        game.ShowHint("Bir hazine sandığı belirdi! Üstünden geç, aç.");
                    }
                    break;
                case EventKind.Birds:
                    birds = BirdFlock.Spawn(district, palette, game);
                    break;
                default:
                    if (!SpawnPoints.TryFind(district, 4f, point => (player.position - point).magnitude > 6f, out var home)) return;
                    pet = Pet.Spawn(home, district, palette, game, player);
                    break;
            }
        }

        /// <summary>Starts one event right now (see DevCommands -event chest|birds|pet).</summary>
        public void Spawn(string name)
        {
            if (!game.InDistrict) return;
            switch (name)
            {
                case "chest": Spawn(EventKind.Chest); break;
                case "birds": Spawn(EventKind.Birds); break;
                case "pet": Spawn(EventKind.Pet); break;
            }
        }

        // The arrow points to the chest (the other events find the player's eye on their own).
        public override bool TryNearest(out Vector3 position, out float distance)
        {
            position = default;
            distance = float.MaxValue;
            if (chest == null || chest.IsOpen) return false;
            position = chest.transform.position;
            distance = Vector3.ProjectOnPlane(position - player.position, Vector3.up).magnitude;
            return true;
        }
    }
}
