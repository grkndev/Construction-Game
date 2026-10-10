using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The shop: a side panel with two tabs: the upgrades, and (once a district is finished) its decorations. It never pauses the game (workers keep working and energy keeps
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
        [SerializeField] GameObject upgradesGroup;
        [SerializeField] GameObject decorGroup;
        [SerializeField] Button upgradesTab;
        [SerializeField] Button decorTab;
        [SerializeField] TMP_Text decorTabLabel;
        [SerializeField] TMP_Text decorHeader;
        [SerializeField] Button[] decorButtons;   // one row per decoration, in building order
        [SerializeField] TMP_Text[] decorLabels;

        GameController game;
        bool open;
        bool showDecorations;
        float decorRefresh;
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
            upgradesTab.onClick.AddListener(() => ShowTab(false));
            decorTab.onClick.AddListener(() => ShowTab(true));
            foreach (var row in decorButtons) row.onClick.AddListener(() => game.OrderDecoration());

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

        public void ShowTab(bool decorations)
        {
            showDecorations = decorations;
            Refresh();
        }

        void Update()
        {
            // While a decoration is being built its row shows the progress: refresh a few times a second.
            if (open && showDecorations && game != null && game.InDistrict && game.Decorations.Ordered)
            {
                decorRefresh -= Time.unscaledDeltaTime;
                if (decorRefresh <= 0f)
                {
                    decorRefresh = 0.25f;
                    Refresh();
                }
            }

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

        static readonly Color Gold = new Color(1f, 0.79f, 0.24f);
        static readonly Color Done = new Color(0.55f, 0.9f, 0.55f);
        static readonly Color Busy = new Color(1f, 0.6f, 0.3f);
        static readonly Color Locked = new Color(0.62f, 0.6f, 0.7f);

        void RefreshDecorations(Economy coins)
        {
            var progress = game.Decorations;
            var scale = game.CurrentState.Def.Scale;
            var income = Balance.TouristIncome(progress.Built, scale);
            decorHeader.text = progress.IsComplete
                ? "ALTIN YILDIZ  •  Bütün süsler tamam"
                : $"GÜZELLİK {progress.Built}/{Balance.DecorationCount}  •  Turist  +{NumberFormat.Compact((long)System.Math.Round(income))}/sn";

            for (var i = 0; i < decorButtons.Length; i++)
            {
                var name = StoryBeats.DecorationNames[i];
                var row = decorButtons[i];
                if (i < progress.Built)
                {
                    decorLabels[i].text = $"{name}\nKuruldu";
                    SetTint(row, Done);
                    row.interactable = false;
                }
                else if (i == progress.Built && progress.Ordered)
                {
                    decorLabels[i].text = $"{name}\nİşçiler kuruyor  %{Mathf.FloorToInt(progress.Progress * 100f)}";
                    SetTint(row, Busy);
                    row.interactable = false;
                }
                else if (i == progress.Built)
                {
                    var cost = progress.NextCost(scale);
                    decorLabels[i].text = $"{name}\nSıradaki  •  {NumberFormat.Compact(cost)}";
                    SetTint(row, Gold);
                    row.interactable = coins.CanAfford(cost);
                }
                else
                {
                    decorLabels[i].text = $"{name}\nKilitli";
                    SetTint(row, Locked);
                    row.interactable = false;
                }
            }
        }

        // Buttons are tinted through their colour block (the sprite itself is white).
        static void SetTint(Button button, Color tint)
        {
            var block = button.colors;
            block.normalColor = tint;
            block.highlightedColor = Color.Lerp(tint, Color.white, 0.35f);
            block.pressedColor = Color.Lerp(tint, Color.black, 0.2f);
            block.selectedColor = tint;
            if (tint != Locked) block.disabledColor = Color.Lerp(tint, new Color(0.62f, 0.6f, 0.7f), 0.15f);
            button.colors = block;
        }

        // Every card shows "tier/last tier" (all start at 1) so it is clear how many purchases are left.
        void Refresh()
        {
            if (!game.InDistrict) return;   // upgrades belong to a district; nothing to show on the dashboard
            var coins = game.Economy;

            // Decorations are only for a finished district; until then their tab is locked and the upgrades show.
            var canDecorate = game.CurrentState.IsComplete;
            if (!canDecorate) showDecorations = false;
            upgradesGroup.SetActive(!showDecorations);
            decorGroup.SetActive(showDecorations);
            decorTab.interactable = canDecorate;
            decorTabLabel.text = canDecorate ? "GÜZELLEŞTİR" : "GÜZELLEŞTİR\n<size=62%>mahalle bitince açılır</size>";
            SetTint(upgradesTab, showDecorations ? new Color(0.78f, 0.74f, 0.9f) : new Color(1f, 0.82f, 0.3f));
            SetTint(decorTab, canDecorate ? (showDecorations ? new Color(1f, 0.82f, 0.3f) : new Color(0.78f, 0.74f, 0.9f)) : new Color(0.62f, 0.6f, 0.7f));
            if (showDecorations)
            {
                RefreshDecorations(coins);
                return;
            }

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
