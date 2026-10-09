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
        [SerializeField] Transform hitOrigin;

        GameInput input;
        float cooldown;

        void Awake() => input = GetComponent<GameInput>();

        void Update()
        {
            cooldown -= Time.deltaTime;

            // Holding repeats at the cooldown rate; fresh presses may interrupt a little early so rapid clicking feels responsive.
            var wants = input.BuildPressedThisFrame ? cooldown <= Balance.HitCooldown * 0.5f
                                                    : input.BuildHeld && cooldown <= 0f;
            if (!wants) return;
            if (input.BuildDevice is Mouse && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var site = game.ActiveSite;
            if (site == null) return;

            var toSite = site.transform.position - transform.position;
            toSite.y = 0f;
            if (toSite.magnitude > BuildSite.WorkRadius)
            {
                if (input.BuildPressedThisFrame) game.ShowHint("Parlayan elmasın olduğu inşaata yaklaş!");
                return;
            }

            cooldown = Balance.HitCooldown;
            // Turn toward the building so the swing reads clearly.
            transform.rotation = Quaternion.LookRotation(toSite.normalized);
            visual.Swing();
            var point = Vector3.Lerp(transform.position, site.transform.position, 0.55f) + Vector3.up * 1.2f;
            game.TryHit(site, game.HammerPower, point);
        }
    }
}
