using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Player
{
    /// <summary>Hammers the nearest plot in reach automatically (no button), and turns the Use input into drinking a cola.</summary>
    [RequireComponent(typeof(GameInput), typeof(Character))]
    public sealed class PlayerHammer : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] WorkerVisual visual;

        GameInput input;
        Character character;
        float cooldown;
        float lastHintTime = -10f;

        void Awake()
        {
            input = GetComponent<GameInput>();
            character = GetComponent<Character>();
        }

        void Update()
        {
            cooldown -= Time.deltaTime;

            if (input.UsePressedThisFrame) game.UseCola();
            if (input.ShopPressedThisFrame) game.ToggleShop();

            // The plot the player can currently work on (nearest unfinished one in reach); the HUD shows its progress.
            var site = game.NearestWorkableSite(transform.position, BuildSite.WorkRadius);
            game.FocusSite = site;

            if (site == null || cooldown > 0f) return;

            var point = Vector3.Lerp(transform.position, site.transform.position, 0.55f) + Vector3.up * 1.2f;
            var result = game.TryHit(site, game.HammerPower, point, character);
            switch (result)
            {
                case HitResult.Hit:
                    cooldown = Balance.HitCooldown / character.SpeedMultiplier;
                    // Turn toward the building so the swing reads clearly (not while the player is walking: the stick decides then).
                    var toSite = site.transform.position - transform.position;
                    toSite.y = 0f;
                    if (input.Move.sqrMagnitude < 0.01f && toSite.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(toSite.normalized);
                    visual.Swing();
                    break;
                case HitResult.Tired:
                    Hint("Yoruldun! Biraz dinlen (ya da Sarı Kola iç)");
                    break;
            }
        }

        // Hits are attempted every frame while in reach, so hints would flash constantly; at most one per 1.5 s.
        void Hint(string text)
        {
            if (Time.time - lastHintTime < 1.5f) return;
            lastHintTime = Time.time;
            game.ShowHint(text);
        }
    }
}
