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
            game.CoinsChanged += Refresh;
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
            game.CoinsChanged -= Refresh;
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

        // Every card shows "tier/last tier" (all start at 1) so it is clear how many purchases are left.
        void Refresh()
        {
            if (!game.InDistrict) return;   // upgrades belong to a district; nothing to show on the dashboard
            var coins = game.Economy;

            var hammer = game.Hammer;
            hammerLabel.text = hammer.IsMaxed
                ? $"ÇEKİÇ  {hammer.Tier}/{hammer.MaxTier}\nEn yüksek • vuruş {game.BaseHammerPower}"
                : $"ÇEKİÇ  {hammer.Tier}/{hammer.MaxTier}\nVuruş {game.BaseHammerPower} → {game.BaseHammerPower + game.CurrentState.Def.Scale}  •  {NumberFormat.Compact(hammer.NextCost)}";
            hammerButton.interactable = !hammer.IsMaxed && coins.CanAfford(hammer.NextCost);

            var workers = game.Workers;
            workerLabel.text = workers.IsMaxed
                ? $"İŞÇİ  {workers.Tier}/{workers.MaxTier}\nEkip tamam"
                : $"İŞÇİ  {workers.Tier}/{workers.MaxTier}\nYeni işçi al  •  {NumberFormat.Compact(workers.NextCost)}";
            workerButton.interactable = !workers.IsMaxed && coins.CanAfford(workers.NextCost);

            // Shows what the upgrade does in the unit the player feels: hits per full energy bar.
            var stamina = game.Stamina;
            var now = Balance.HitsPerBar(Balance.PlayerEnergy, stamina.Level);
            staminaLabel.text = stamina.IsMaxed
                ? $"DAYANIKLILIK  {stamina.Tier}/{stamina.MaxTier}\nEn yüksek • {now} vuruş"
                : $"DAYANIKLILIK  {stamina.Tier}/{stamina.MaxTier}\n{now} → {Balance.HitsPerBar(Balance.PlayerEnergy, stamina.Level + 1)} vuruş  •  {NumberFormat.Compact(stamina.NextCost)}";
            staminaButton.interactable = !stamina.IsMaxed && coins.CanAfford(stamina.NextCost);

            var speed = game.Speed;
            var speedNow = Mathf.RoundToInt((Balance.SpeedMultiplier(speed.Level) - 1f) * 100f);
            var speedNext = Mathf.RoundToInt((Balance.SpeedMultiplier(speed.Level + 1) - 1f) * 100f);
            speedLabel.text = speed.IsMaxed
                ? $"HIZ  {speed.Tier}/{speed.MaxTier}\nEn yüksek • +%{speedNow}"
                : $"HIZ  {speed.Tier}/{speed.MaxTier}\n+%{speedNow} → +%{speedNext}  •  {NumberFormat.Compact(speed.NextCost)}";
            speedButton.interactable = !speed.IsMaxed && coins.CanAfford(speed.NextCost);

            colaBuyLabel.text = $"SARI KOLA  cepte {game.ColaCount}/{Balance.MaxColaInventory}\nEnerji + Coşku  •  {NumberFormat.Compact(game.ColaPrice)}";
            colaBuyButton.interactable = coins.CanAfford(game.ColaPrice);
        }
    }
}
