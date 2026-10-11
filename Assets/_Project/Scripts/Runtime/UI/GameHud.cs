using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Screen-space HUD: coins, current building, energy bar, cola, shop button, hints. It only reads game state and calls Use*; purchases live in <see cref="ShopPanel"/>.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        const float EnergyBarMinWidth = 140f;   // bar width at base capacity; it grows with stamina so the upgrade is visible
        const float EnergyBarMaxWidth = 344f;   // ...up to this width at the maximum stamina level

        [SerializeField] TMP_Text coinsText;
        [SerializeField] RectTransform coinPanel;
        [SerializeField] RectTransform levelPanel;
        [SerializeField] RectTransform audioGroup;   // music / effects switches: top-right, shifted left of the map button inside a district
        [SerializeField] UiPlate buildingPlate;
        [SerializeField] UiPlate hintPlate;
        [SerializeField] UiPlate boostPlate;
        [SerializeField] GameObject districtGroup;     // everything that only makes sense inside a district
        [SerializeField] Button exitButton;
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] RectTransform levelFill;
        [SerializeField] Image levelFillImage;
        [SerializeField] ShopPanel shop;
        [SerializeField] Button shopButton;
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
            game.CoinsChanged += Refresh;
            game.StateChanged += Refresh;
            game.ModeChanged += OnModeChanged;
            game.Level.Changed += Refresh;
            game.Hint += ShowHint;
            game.Story += ShowHint;

            shop.Bind(game);
            shopButton.onClick.AddListener(shop.Toggle);
            exitButton.onClick.AddListener(game.ExitToDashboard);
            colaUseButton.onClick.AddListener(game.UseCola);

            OnModeChanged();
            Refresh();
        }

        void OnModeChanged()
        {
            lastEnergy = lastEnergyMax = lastBoostKind = lastBoostTenths = -1;   // the plates below are cleared or refilled: forget what they showed
            lastPlateKey = (null, -1, -1, -1);
            districtGroup.SetActive(game.InDistrict);
            coinPanel.gameObject.SetActive(game.InDistrict);   // coins belong to a district: the map shows none
            levelPanel.anchoredPosition = new Vector2(levelPanel.anchoredPosition.x, game.InDistrict ? -150f : -40f);
            audioGroup.anchoredPosition = new Vector2(game.InDistrict ? -330f : 0f, 0f);
            if (!game.InDistrict)
            {
                shop.HideImmediate();
                buildingPlate.Text = "";
                boostPlate.Text = "";
                return;
            }
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.CoinsChanged -= Refresh;
            game.StateChanged -= Refresh;
            game.ModeChanged -= OnModeChanged;
            game.Level.Changed -= Refresh;
            game.Hint -= ShowHint;
            game.Story -= ShowHint;
        }

        void ShowHint(string text) => ShowHint(text, 2.8f);

        void ShowHint(string text, float seconds)
        {
            hintPlate.Text = text;
            hintPlate.Alpha = 1f;
            hintTimer = seconds;
        }

        // The coin counter counts up towards coinTarget instead of jumping.
        double coinShown;
        long coinTarget;
        int coinsDistrict = -2;
        long lastCoinText = -1;

        void ShowCoins()
        {
            var value = (long)coinShown;
            if (value == lastCoinText) return;
            lastCoinText = value;
            coinsText.text = NumberFormat.Compact(value);
        }

        // What the polled labels last showed (see Update).
        int lastEnergy = -1, lastEnergyMax = -1, lastBoostKind = -1, lastBoostTenths = -1;
        float lastBarFraction = -1f;
        bool lastBarExhausted;
        (Game.World.BuildSite, int, int, int) lastPlateKey = (null, -1, -1, -1);

        void Update()
        {
            if (game == null) return;

            if (hintTimer > 0f)
            {
                hintTimer -= Time.deltaTime;
                hintPlate.Alpha = Mathf.Clamp01(hintTimer * 2f);
                if (hintTimer <= 0f) hintPlate.Text = "";
            }

            if (!game.InDistrict) return;

            if (coinShown < coinTarget)
            {
                coinShown = System.Math.Min(coinTarget, coinShown + System.Math.Max(1.0, (coinTarget - coinShown) * System.Math.Min(1.0, Time.unscaledDeltaTime * 7.0)));
                ShowCoins();
            }

            // Values that change every frame (bars, timers, hit progress) are cheaper to poll than to wire as events.
            var energy = game.PlayerEnergy;
            if (energy.Fraction != lastBarFraction || energy.IsExhausted != lastBarExhausted)
            {
                lastBarFraction = energy.Fraction;
                lastBarExhausted = energy.IsExhausted;
                SetBar(playerEnergyFill, playerEnergyImage, energy.Fraction, energy.IsExhausted);
            }

            // Only touch text and layout when something visible changed: rebuilding a TMP mesh (and allocating its string) every frame adds up.
            var shown = Mathf.CeilToInt(energy.Value);
            var max = Mathf.RoundToInt(energy.Max);
            if (shown != lastEnergy || max != lastEnergyMax)
            {
                lastEnergy = shown;
                lastEnergyMax = max;
                playerEnergyLabel.text = $"ENERJİ  {shown} / {max}";
            }
            var capacity = Mathf.InverseLerp(Balance.PlayerEnergy.Max,
                Balance.WithStamina(Balance.PlayerEnergy, Balance.MaxStaminaLevel - 1).Max, energy.Max);
            var barWidth = Mathf.Lerp(EnergyBarMinWidth, EnergyBarMaxWidth, capacity);
            if (!Mathf.Approximately(playerEnergyBack.sizeDelta.x, barWidth)) playerEnergyBack.sizeDelta = new Vector2(barWidth, playerEnergyBack.sizeDelta.y);

            var boostKind = game.Boost.IsActive ? 1 : game.LevelBuff.IsActive ? 2 : 0;
            var boostTenths = boostKind == 1 ? Mathf.RoundToInt(game.Boost.Remaining * 10f) : boostKind == 2 ? Mathf.RoundToInt(game.LevelBuff.Remaining * 10f) : 0;
            if (boostKind != lastBoostKind || boostTenths != lastBoostTenths)
            {
                lastBoostKind = boostKind;
                lastBoostTenths = boostTenths;
                if (boostKind == 1) boostPlate.Text = $"COŞKU!  Sv +{Balance.LevelBonus}  hızlı  {game.Boost.Remaining:0.0}s";
                else if (boostKind == 2) boostPlate.Text = $"SEVİYE +{Balance.LevelBonus}  {game.LevelBuff.Remaining:0.0}s";
                else boostPlate.Text = "";
            }

            var focus = game.FocusSite;
            var plateKey = (focus, focus != null ? focus.FloorsBuilt : game.CompletedCount,
                focus != null ? Mathf.FloorToInt(focus.Fraction * 100f) : game.TotalCount, game.CurrentIndex);
            if (plateKey != lastPlateKey)
            {
                lastPlateKey = plateKey;
                buildingPlate.Text = focus != null
                    ? $"{focus.Plan.Name}   Kat {focus.FloorsBuilt}/{focus.Plan.Floors}   %{Mathf.FloorToInt(focus.Fraction * 100f)}"
                    : $"{game.CurrentState.Def.Name}  {game.CompletedCount}/{game.TotalCount}";
            }
        }

        static void SetBar(RectTransform fill, Image image, float fraction, bool exhausted)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            image.enabled = fraction > 0.02f;   // a negative-width rect would draw mirrored at 0
            image.color = exhausted || fraction < 0.25f ? Low : fraction < 0.5f ? Warn : Good;
        }

        void Refresh()
        {
            // The counter climbs to the new amount (the coins are still flying); spending or switching to another district's purse snaps at once.
            var coins = game.Economy.Coins;
            if (game.CurrentIndex != coinsDistrict || coins < coinTarget) { coinsDistrict = game.CurrentIndex; coinShown = coins; }
            coinTarget = coins;
            ShowCoins();

            var level = game.Level;
            levelLabel.text = level.IsMax ? $"SEVİYE {level.Level}  (EN YÜKSEK)" : $"SEVİYE {level.Level}   {level.XpIntoLevel}/{level.XpForLevel}";
            levelFill.anchorMax = new Vector2(Mathf.Clamp01(level.Fraction), 1f);
            levelFillImage.enabled = level.Fraction > 0.02f;

            colaUseLabel.text = $"Sarı Kola  x{game.ColaCount}";
            colaUseButton.interactable = game.ColaCount > 0;
        }
    }
}
