using Game.Audio;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The settings window: a centred panel over a dimmed screen with a slider for the music level and one for the effects
    /// level. Opened by the gear button (top-right on every screen) or Esc, closed by X, a tap outside or Esc. It never pauses the game.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] AudioManager audioManager;
        [SerializeField] GameObject window;       // dim overlay + panel, switched on and off
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button overlayButton;    // the dim background: tapping outside the panel closes
        [SerializeField] Slider musicSlider;
        [SerializeField] TMP_Text musicValue;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text sfxValue;
        [SerializeField] Button shakeButton;
        [SerializeField] TMP_Text shakeLabel;

        void Start()
        {
            musicSlider.SetValueWithoutNotify(audioManager.MusicLevel);
            sfxSlider.SetValueWithoutNotify(audioManager.SfxLevel);
            ShowValues();

            musicSlider.onValueChanged.AddListener(v => { audioManager.SetMusicLevel(v); ShowValues(); });
            sfxSlider.onValueChanged.AddListener(v => { audioManager.SetSfxLevel(v); ShowValues(); });
            openButton.onClick.AddListener(() => SetOpen(!window.activeSelf));
            ShowShake();
            shakeButton.onClick.AddListener(() => { FeelSettings.ShakeEnabled = !FeelSettings.ShakeEnabled; ShowShake(); });
            closeButton.onClick.AddListener(() => SetOpen(false));
            overlayButton.onClick.AddListener(() => SetOpen(false));
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetOpen(!window.activeSelf);
        }

        public void Open() => SetOpen(true);

        void SetOpen(bool open)
        {
            if (window.activeSelf == open) return;
            window.SetActive(open);
            if (open) transform.SetAsLastSibling();   // above whatever else is on screen
        }

        void ShowShake()
        {
            var on = FeelSettings.ShakeEnabled;
            shakeLabel.text = on ? "AÇIK" : "KAPALI";
            shakeButton.image.color = on ? new Color(0.5f, 0.9f, 0.55f) : new Color(0.7f, 0.68f, 0.78f);
        }

        void ShowValues()
        {
            musicValue.text = $"{Mathf.RoundToInt(musicSlider.value * 100f)}%";
            sfxValue.text = $"{Mathf.RoundToInt(sfxSlider.value * 100f)}%";
        }
    }
}
