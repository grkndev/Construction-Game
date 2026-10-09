using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Screen-space HUD: coins, current building, energy bar, cola, upgrade buttons, hints. It only reads game state and calls Buy*/Use*.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        const float EnergyBarMinWidth = 140f;   // bar width at base capacity; it grows with stamina so the upgrade is visible
        const float EnergyBarMaxWidth = 344f;   // ...up to this width at the maximum stamina level

        [SerializeField] TMP_Text coinsText;
        [SerializeField] UiPlate buildingPlate;
        [SerializeField] UiPlate hintPlate;
        [SerializeField] UiPlate victoryPlate;
        [SerializeField] UiPlate boostPlate;
        [SerializeField] Button hammerButton;
        [SerializeField] TMP_Text hammerLabel;
        [SerializeField] Button workerButton;
        [SerializeField] TMP_Text workerLabel;
        [SerializeField] Button staminaButton;
        [SerializeField] TMP_Text staminaLabel;
        [SerializeField] Button colaBuyButton;
        [SerializeField] TMP_Text colaBuyLabel;
        [SerializeField] Button colaUseButton;
        [SerializeField] TMP_Text colaUseLabel;
        [SerializeField] TMP_Text playerEnergyLabel;
        [SerializeField] RectTransform playerEnergyBack;
        [SerializeField] RectTransform playerEnergyFill;
        [SerializeField] Image playerEnergyImage;

        static readonly Color Good = new Color(0.36f, 0.89f, 0.49f);
        static readonly Color Warn = new Color(1f, 0.82f, 0.25f);
        static readonly Color Low = new Color(1f, 0.35f, 0.3f);

        GameController game;
        float hintTimer;

        public void Bind(GameController controller)
        {
            game = controller;
            game.Economy.Changed += Refresh;
            game.Hammer.Changed += Refresh;
            game.Workers.Changed += Refresh;
            game.Stamina.Changed += Refresh;
            game.StateChanged += Refresh;
            game.Hint += ShowHint;

            hammerButton.onClick.AddListener(game.BuyHammer);
            workerButton.onClick.AddListener(game.BuyWorker);
            staminaButton.onClick.AddListener(game.BuyStamina);
            colaBuyButton.onClick.AddListener(game.BuyCola);
            colaUseButton.onClick.AddListener(game.UseCola);
            victoryPlate.Text = "";

            ShowHint("WASD / sol çubuk: yürü    SPACE / tık / A: çekiç vur    İstediğin inşaata git!", 7f);
            Refresh();
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.Economy.Changed -= Refresh;
            game.Hammer.Changed -= Refresh;
            game.Workers.Changed -= Refresh;
            game.Stamina.Changed -= Refresh;
            game.StateChanged -= Refresh;
            game.Hint -= ShowHint;
        }

        void ShowHint(string text) => ShowHint(text, 2.8f);

        void ShowHint(string text, float seconds)
        {
            hintPlate.Text = text;
            hintPlate.Alpha = 1f;
            hintTimer = seconds;
        }

        void Update()
        {
            if (game == null) return;

            if (hintTimer > 0f)
            {
                hintTimer -= Time.deltaTime;
                hintPlate.Alpha = Mathf.Clamp01(hintTimer * 2f);
                if (hintTimer <= 0f) hintPlate.Text = "";
            }

            // Values that change every frame (bars, timers, hit progress) are cheaper to poll than to wire as events.
            var energy = game.PlayerEnergy;
            SetBar(playerEnergyFill, playerEnergyImage, energy.Fraction, energy.IsExhausted);
            playerEnergyLabel.text = $"ENERJİ  {Mathf.CeilToInt(energy.Value)} / {Mathf.RoundToInt(energy.Max)}";
            var capacity = Mathf.InverseLerp(Balance.PlayerEnergy.Max,
                Balance.WithStamina(Balance.PlayerEnergy, Balance.MaxStaminaLevel).Max, energy.Max);
            playerEnergyBack.sizeDelta = new Vector2(Mathf.Lerp(EnergyBarMinWidth, EnergyBarMaxWidth, capacity), playerEnergyBack.sizeDelta.y);

            if (game.Boost.IsActive) boostPlate.Text = $"COŞKU!  Sv +{Balance.LevelBonus}  hızlı  {game.Boost.Remaining:0.0}s";
            else if (game.LevelBuff.IsActive) boostPlate.Text = $"SEVİYE +{Balance.LevelBonus}  {game.LevelBuff.Remaining:0.0}s";
            else boostPlate.Text = "";

            if (game.AllDone) return;
            var focus = game.FocusSite;
            buildingPlate.Text = focus != null
                ? $"{focus.Plan.Name}   Kat {focus.FloorsBuilt}/{focus.Plan.Floors}   %{Mathf.FloorToInt(focus.Fraction * 100f)}"
                : $"İnşaatlar  {game.CompletedCount}/{game.TotalCount}";
        }

        static void SetBar(RectTransform fill, Image image, float fraction, bool exhausted)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            image.enabled = fraction > 0.02f;   // a negative-width rect would draw mirrored at 0
            image.color = exhausted || fraction < 0.25f ? Low : fraction < 0.5f ? Warn : Good;
        }

        void Refresh()
        {
            coinsText.text = NumberFormat.Compact(game.Economy.Coins);

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

            colaBuyLabel.text = $"SARI KOLA\nEnerji + Coşku  •  {NumberFormat.Compact(game.ColaPrice)}";
            colaBuyButton.interactable = game.Economy.CanAfford(game.ColaPrice);

            colaUseLabel.text = $"Sarı Kola  x{game.ColaCount}   [E]";
            colaUseButton.interactable = game.ColaCount > 0;

            if (game.AllDone)
            {
                buildingPlate.Text = "";
                victoryPlate.Text = "Şehir tamamlandı!\nHarika iş çıkardın";
            }
        }
    }
}
