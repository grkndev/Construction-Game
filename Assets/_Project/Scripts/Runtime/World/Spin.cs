using UnityEngine;

namespace Game.World
{
    /// <summary>Turns its object around its own z axis (windmill blades).</summary>
    public sealed class Spin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 40f;

        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
