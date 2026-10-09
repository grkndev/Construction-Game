using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The shop: a side panel with the four purchases. It never pauses the game (workers keep working and energy keeps
    /// refilling while the player shops). Only reads game state and calls Buy*.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        const float SlideSeconds = 0.22f;

        [SerializeField] RectTransform panel;
        [SerializeField] Button closeButton;
        [SerializeField] Button hammerButton;
        [SerializeField] TMP_Text hammerLabel;
        [SerializeField] Button workerButton;
        [SerializeField] TMP_Text workerLabel;
        [SerializeField] Button staminaButton;
        [SerializeField] TMP_Text staminaLabel;
        [SerializeField] Button speedButton;
        [SerializeField] TMP_Text speedLabel;
        [SerializeField] Button colaBuyButton;
        [SerializeField] TMP_Text colaBuyLabel;

        GameController game;
        bool open;
        float slide;            // 0 = hidden, 1 = shown
        float shownX, hiddenX;

        public bool IsOpen => open;

        public void Bind(GameController controller)
        {
            game = controller;
            game.Economy.Changed += Refresh;
            game.StateChanged += Refresh;
            game.ShopToggleRequested += Toggle;

            hammerButton.onClick.AddListener(game.BuyHammer);
            workerButton.onClick.AddListener(game.BuyWorker);
            staminaButton.onClick.AddListener(game.BuyStamina);
            speedButton.onClick.AddListener(game.BuySpeed);
            colaBuyButton.onClick.AddListener(game.BuyCola);
            closeButton.onClick.AddListener(Close);

            shownX = panel.anchoredPosition.x;
            hiddenX = shownX + panel.sizeDelta.x + 80f;
            Apply();
            Refresh();
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.Economy.Changed -= Refresh;
            game.StateChanged -= Refresh;
            game.ShopToggleRequested -= Toggle;
        }

        public void Toggle()
        {
            open = !open;
            if (open) Refresh();
        }

        public void Close() => open = false;

        public void HideImmediate()
        {
            open = false;
            slide = 0f;
            Apply();
        }

        void Update()
        {
            var target = open ? 1f : 0f;
            if (Mathf.Approximately(slide, target)) return;
            slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime / SlideSeconds);
            Apply();
        }

        void Apply()
        {
            panel.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, shownX, Easing.OutCubic(slide)), panel.anchoredPosition.y);
            panel.gameObject.SetActive(slide > 0.001f);
        }

        void Refresh()
        {
            if (!game.InDistrict) return;   // upgrades belong to a district; nothing to show on the dashboard
            hammerLabel.text = game.Hammer.IsMaxed
                ? "ÇEKİÇ\nEn yüksek seviye"
                : $"ÇEKİÇ  Sv.{game.Hammer.Level + 1}\nVuruş {game.BaseHammerPower} (+1)  •  {NumberFormat.Compact(game.Hammer.NextCost)}";
            hammerButton.interactable = !game.Hammer.IsMaxed && game.Economy.CanAfford(game.Hammer.NextCost);

            workerLabel.text = game.Workers.IsMaxed
                ? $"İŞÇİ  {game.Workers.Level}\nEkip tamam"
                : $"İŞÇİ AL  ({game.Workers.Level}/{Balance.MaxWorkers})\n{NumberFormat.Compact(game.Workers.NextCost)}";
            workerButton.interactable = !game.Workers.IsMaxed && game.Economy.CanAfford(game.Workers.NextCost);

            // Shows what the upgrade does in the unit the player feels: hits per full energy bar.
            var now = Balance.HitsPerBar(Balance.PlayerEnergy, game.Stamina.Level);
            staminaLabel.text = game.Stamina.IsMaxed
                ? $"DAYANIKLILIK\nEn yüksek • {now} vuruş"
                : $"DAYANIKLILIK  Sv.{game.Stamina.Level + 1}\n{now} → {Balance.HitsPerBar(Balance.PlayerEnergy, game.Stamina.Level + 1)} vuruş  •  {NumberFormat.Compact(game.Stamina.NextCost)}";
            staminaButton.interactable = !game.Stamina.IsMaxed && game.Economy.CanAfford(game.Stamina.NextCost);

            var speedPercent = Mathf.RoundToInt((Balance.SpeedMultiplier(game.Speed.Level + 1) - 1f) * 100f);
            speedLabel.text = game.Speed.IsMaxed
                ? $"HIZ\nEn yüksek • +%{Mathf.RoundToInt((Balance.SpeedMultiplier(game.Speed.Level) - 1f) * 100f)}"
                : $"HIZ  Sv.{game.Speed.Level + 1}/{Balance.MaxSpeedLevel}\nYürü, vur, toparlan +%{speedPercent}  •  {NumberFormat.Compact(game.Speed.NextCost)}";
            speedButton.interactable = !game.Speed.IsMaxed && game.Economy.CanAfford(game.Speed.NextCost);

            colaBuyLabel.text = $"SARI KOLA\nEnerji + Coşku  •  {NumberFormat.Compact(game.ColaPrice)}";
            colaBuyButton.interactable = game.Economy.CanAfford(game.ColaPrice);
        }
    }
}
