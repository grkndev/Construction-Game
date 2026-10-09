using Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Plays the tick sound for the given buttons (purchases and cola have their own sounds, so they are not listed).</summary>
    public sealed class UiClickSounds : MonoBehaviour
    {
        [SerializeField] AudioManager audioManager;
        [SerializeField] Button[] buttons;

        void Start()
        {
            foreach (var button in buttons) button.onClick.AddListener(audioManager.Click);
        }
    }
}
