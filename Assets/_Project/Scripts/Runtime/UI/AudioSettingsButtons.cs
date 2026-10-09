using Game.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>The two small switches (music, effects) shown on every screen.</summary>
    public sealed class AudioSettingsButtons : MonoBehaviour
    {
        [SerializeField] AudioManager audioManager;
        [SerializeField] Button musicButton;
        [SerializeField] TMP_Text musicLabel;
        [SerializeField] Button sfxButton;
        [SerializeField] TMP_Text sfxLabel;

        void Start()
        {
            musicButton.onClick.AddListener(() => { audioManager.SetMusic(!audioManager.MusicOn); Refresh(); });
            sfxButton.onClick.AddListener(() => { audioManager.SetSfx(!audioManager.SfxOn); Refresh(); });
            Refresh();
        }

        void Refresh()
        {
            // Off = struck through and dimmed, so the state reads without words.
            Show(musicButton, musicLabel, "Müzik", audioManager.MusicOn);
            Show(sfxButton, sfxLabel, "Ses", audioManager.SfxOn);
        }

        static void Show(Button button, TMP_Text label, string text, bool on)
        {
            label.text = on ? text : $"<s>{text}</s>";
            button.image.color = on ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        }
    }
}
