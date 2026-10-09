using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>Turns the Build input into hammer hits on the active plot when the player stands close enough.</summary>
    [RequireComponent(typeof(GameInput))]
    public sealed class PlayerHammer : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] WorkerVisual visual;

        GameInput input;
        float cooldown;

        void Awake() => input = GetComponent<GameInput>();

        void Update()
        {
            cooldown -= Time.deltaTime;

            // The plot the player can currently work on (nearest unfinished one in reach); the HUD shows its progress.
            var site = game.NearestWorkableSite(transform.position, BuildSite.WorkRadius);
            game.FocusSite = site;

            // Holding repeats at the cooldown rate; fresh presses may interrupt a little early so rapid clicking feels responsive.
            var wants = input.BuildPressedThisFrame ? cooldown <= Balance.HitCooldown * 0.5f
                                                    : input.BuildHeld && cooldown <= 0f;
            if (!wants) return;
            if (input.BuildDevice is Mouse && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (site == null)
            {
                if (input.BuildPressedThisFrame && !game.AllDone) game.ShowHint("Bir inşaata yaklaş!");
                return;
            }

            cooldown = Balance.HitCooldown;
            // Turn toward the building so the swing reads clearly.
            var toSite = site.transform.position - transform.position;
            toSite.y = 0f;
            if (toSite.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toSite.normalized);
            visual.Swing();
            var point = Vector3.Lerp(transform.position, site.transform.position, 0.55f) + Vector3.up * 1.2f;
            game.TryHit(site, game.HammerPower, point);
        }
    }
}
