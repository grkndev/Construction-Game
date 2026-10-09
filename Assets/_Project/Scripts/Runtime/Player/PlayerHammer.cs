using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>Turns the Build input into hammer hits on the nearest plot in reach, and Use into drinking a cola.</summary>
    [RequireComponent(typeof(GameInput))]
    public sealed class PlayerHammer : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] WorkerVisual visual;
        [SerializeField] MeterBar energyBar;

        GameInput input;
        float cooldown;
        float lastHintTime = -10f;

        void Awake() => input = GetComponent<GameInput>();

        void Update()
        {
            cooldown -= Time.deltaTime;

            var tired = game.PlayerEnergy.IsExhausted && !game.Boost.IsActive;
            visual.SetTired(tired);
            energyBar.Set(game.PlayerEnergy.Fraction, tired);

            if (input.UsePressedThisFrame) game.UseCola();

            // The plot the player can currently work on (nearest unfinished one in reach); the HUD shows its progress.
            var site = game.NearestWorkableSite(transform.position, BuildSite.WorkRadius);
            game.FocusSite = site;

            // Holding repeats at the cooldown rate; fresh presses may interrupt a little early so rapid clicking feels responsive.
            var cooldownLimit = Balance.HitCooldown * (game.Boost.IsActive ? 0.7f : 1f);
            var wants = input.BuildPressedThisFrame ? cooldown <= cooldownLimit * 0.5f
                                                    : input.BuildHeld && cooldown <= 0f;
            if (!wants) return;
            if (input.BuildDevice is Mouse && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (site == null)
            {
                if (input.BuildPressedThisFrame && !game.AllDone) Hint("Bir inşaata yaklaş!");
                return;
            }

            var point = Vector3.Lerp(transform.position, site.transform.position, 0.55f) + Vector3.up * 1.2f;
            var result = game.TryHit(site, game.HammerPower, point, byPlayer: true);
            switch (result)
            {
                case HitResult.Hit:
                    cooldown = cooldownLimit;
                    // Turn toward the building so the swing reads clearly.
                    var toSite = site.transform.position - transform.position;
                    toSite.y = 0f;
                    if (toSite.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toSite.normalized);
                    visual.Swing();
                    break;
                case HitResult.PlayerTired:
                    Hint("Yoruldun! Biraz dinlen (ya da Sarı Kola iç: E)");
                    break;
                case HitResult.SiteTired:
                    Hint("Şantiye dinleniyor, başka bir inşaata geç!");
                    break;
            }
        }

        // Held-button hints would flash every frame; at most one per 1.5 s.
        void Hint(string text)
        {
            if (Time.time - lastHintTime < 1.5f) return;
            lastHintTime = Time.time;
            game.ShowHint(text);
        }
    }
}
