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
        [SerializeField] UiPlate buildingPlate;
        [SerializeField] UiPlate hintPlate;
        [SerializeField] UiPlate victoryPlate;
        [SerializeField] UiPlate boostPlate;
        [SerializeField] GameObject districtGroup;     // everything that only makes sense inside a district
        [SerializeField] UiPlate levelPlate;
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
        float levelTimer;

        public void Bind(GameController controller)
        {
            game = controller;
            game.CoinsChanged += Refresh;
            game.StateChanged += Refresh;
            game.ModeChanged += OnModeChanged;
            game.LeveledUp += OnLeveledUp;
            game.Level.Changed += Refresh;
            game.Hint += ShowHint;
            game.Story += ShowHint;

            shop.Bind(game);
            shopButton.onClick.AddListener(shop.Toggle);
            exitButton.onClick.AddListener(game.ExitToDashboard);
            levelPlate.Text = "";
            colaUseButton.onClick.AddListener(game.UseCola);
            victoryPlate.Text = "";

            OnModeChanged();
            Refresh();
        }

        void OnModeChanged()
        {
            districtGroup.SetActive(game.InDistrict);
            coinPanel.gameObject.SetActive(game.InDistrict);   // coins belong to a district: the map shows none
            levelPanel.anchoredPosition = new Vector2(levelPanel.anchoredPosition.x, game.InDistrict ? -150f : -40f);
            if (!game.InDistrict)
            {
                shop.HideImmediate();
                buildingPlate.Text = "";
                boostPlate.Text = "";
                return;
            }
        }

        void OnLeveledUp(int level)
        {
            levelPlate.Text = $"SEVİYE {level}!";
            levelPlate.Alpha = 1f;
            levelTimer = 2.5f;
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.CoinsChanged -= Refresh;
            game.StateChanged -= Refresh;
            game.ModeChanged -= OnModeChanged;
            game.LeveledUp -= OnLeveledUp;
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

        void Update()
        {
            if (game == null) return;

            if (hintTimer > 0f)
            {
                hintTimer -= Time.deltaTime;
                hintPlate.Alpha = Mathf.Clamp01(hintTimer * 2f);
                if (hintTimer <= 0f) hintPlate.Text = "";
            }

            if (levelTimer > 0f)
            {
                levelTimer -= Time.deltaTime;
                levelPlate.Alpha = Mathf.Clamp01(levelTimer * 2f);
                if (levelTimer <= 0f) levelPlate.Text = "";
            }

            if (!game.InDistrict) return;

            // Values that change every frame (bars, timers, hit progress) are cheaper to poll than to wire as events.
            var energy = game.PlayerEnergy;
            SetBar(playerEnergyFill, playerEnergyImage, energy.Fraction, energy.IsExhausted);
            playerEnergyLabel.text = $"ENERJİ  {Mathf.CeilToInt(energy.Value)} / {Mathf.RoundToInt(energy.Max)}";
            var capacity = Mathf.InverseLerp(Balance.PlayerEnergy.Max,
                Balance.WithStamina(Balance.PlayerEnergy, Balance.MaxStaminaLevel - 1).Max, energy.Max);
            playerEnergyBack.sizeDelta = new Vector2(Mathf.Lerp(EnergyBarMinWidth, EnergyBarMaxWidth, capacity), playerEnergyBack.sizeDelta.y);

            if (game.Boost.IsActive) boostPlate.Text = $"COŞKU!  Sv +{Balance.LevelBonus}  hızlı  {game.Boost.Remaining:0.0}s";
            else if (game.LevelBuff.IsActive) boostPlate.Text = $"SEVİYE +{Balance.LevelBonus}  {game.LevelBuff.Remaining:0.0}s";
            else boostPlate.Text = "";

            if (game.CityComplete) return;
            var focus = game.FocusSite;
            buildingPlate.Text = focus != null
                ? $"{focus.Plan.Name}   Kat {focus.FloorsBuilt}/{focus.Plan.Floors}   %{Mathf.FloorToInt(focus.Fraction * 100f)}"
                : $"{game.CurrentState.Def.Name}  {game.CompletedCount}/{game.TotalCount}";
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

            var level = game.Level;
            levelLabel.text = level.IsMax ? $"SEVİYE {level.Level}  (EN YÜKSEK)" : $"SEVİYE {level.Level}   {level.XpIntoLevel}/{level.XpForLevel}";
            levelFill.anchorMax = new Vector2(Mathf.Clamp01(level.Fraction), 1f);
            levelFillImage.enabled = level.Fraction > 0.02f;

            colaUseLabel.text = $"Sarı Kola  x{game.ColaCount}";
            colaUseButton.interactable = game.ColaCount > 0;

            if (game.CityComplete)
            {
                buildingPlate.Text = "";
                victoryPlate.Text = "Şehir tamamlandı!\nHarika iş çıkardın";
            }
        }
    }
}
