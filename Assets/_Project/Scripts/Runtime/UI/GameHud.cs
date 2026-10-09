using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Screen-space HUD: coins, current building, energy bars, cola, upgrade buttons, hints. It only reads game state and calls Buy*/Use*.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] TMP_Text coinsText;
        [SerializeField] TMP_Text buildingText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] TMP_Text victoryText;
        [SerializeField] TMP_Text boostText;
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
            victoryText.gameObject.SetActive(false);

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
            hintText.text = text;
            hintTimer = seconds;
        }

        void Update()
        {
            if (game == null) return;

            if (hintTimer > 0f)
            {
                hintTimer -= Time.deltaTime;
                hintText.alpha = Mathf.Clamp01(hintTimer * 2f);
            }

            // Values that change every frame (bars, timers, hit progress) are cheaper to poll than to wire as events.
            SetBar(playerEnergyFill, playerEnergyImage, game.PlayerEnergy.Fraction, game.PlayerEnergy.IsExhausted);

            var focus = game.FocusSite;

            var buffed = game.Boost.IsActive || game.LevelBuff.IsActive;
            boostText.gameObject.SetActive(buffed);
            if (game.Boost.IsActive) boostText.text = $"COŞKU!  Sv +{Balance.LevelBonus}  hızlı  {game.Boost.Remaining:0.0}s";
            else if (game.LevelBuff.IsActive) boostText.text = $"SEVİYE +{Balance.LevelBonus}  {game.LevelBuff.Remaining:0.0}s";

            if (game.AllDone) return;
            buildingText.text = focus != null
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

            staminaLabel.text = game.Stamina.IsMaxed
                ? $"DAYANIKLILIK\nEn yüksek seviye"
                : $"DAYANIKLILIK  Sv.{game.Stamina.Level + 1}\nEnerji +%15  •  {NumberFormat.Compact(game.Stamina.NextCost)}";
            staminaButton.interactable = !game.Stamina.IsMaxed && game.Economy.CanAfford(game.Stamina.NextCost);

            colaBuyLabel.text = $"SARI KOLA\nEnerji + Coşku  •  {NumberFormat.Compact(game.ColaPrice)}";
            colaBuyButton.interactable = game.Economy.CanAfford(game.ColaPrice);

            colaUseLabel.text = $"Sarı Kola  x{game.ColaCount}   [E]";
            colaUseButton.interactable = game.ColaCount > 0;

            if (game.AllDone)
            {
                buildingText.text = "";
                victoryText.gameObject.SetActive(true);
                victoryText.text = "Şehir tamamlandı!\nHarika iş çıkardın";
            }
        }
    }
}
