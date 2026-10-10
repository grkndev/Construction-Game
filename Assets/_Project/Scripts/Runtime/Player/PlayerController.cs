using Game.World;
using UnityEngine;

namespace Game.Player
{
    /// <summary>Camera-relative walking on a CharacterController, kept inside the island.</summary>
    [RequireComponent(typeof(CharacterController), typeof(GameInput), typeof(Character))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] float speed = 5.5f;
        [SerializeField] float turnSpeed = 14f;
        District district;
        [SerializeField] WorkerVisual visual;

        CharacterController controller;
        GameInput input;
        Character character;
        Transform cam;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<GameInput>();
            character = GetComponent<Character>();
        }

        /// <summary>The district the player is in; they are kept inside its walkable region.</summary>
        public void SetBounds(District current) => district = current;

        void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;

            // Flatten the camera axes so "up" on the stick always means "away from the camera".
            var forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            var right = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;

            var stick = Vector2.ClampMagnitude(input.Move, 1f);
            var direction = forward * stick.y + right * stick.x;

            // Small constant downward push keeps the controller grounded on the flat island.
            controller.Move((direction * (speed * character.SpeedMultiplier) + Vector3.down * 2f) * Time.deltaTime);

            if (district != null)
            {
                var clamped = district.Clamp(transform.position);
                if ((clamped - transform.position).sqrMagnitude > 1e-6f)
                {
                    controller.enabled = false;
                    transform.position = clamped;
                    controller.enabled = true;
                }
            }

            if (direction.sqrMagnitude > 0.001f)
            {
                var target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
            visual.SetMove(stick.magnitude);
        }
    }
}
