using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Screen-space HUD: coins, current building, upgrade buttons, hints. It only reads game state and calls Buy*.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] TMP_Text coinsText;
        [SerializeField] TMP_Text buildingText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] TMP_Text victoryText;
        [SerializeField] Button hammerButton;
        [SerializeField] TMP_Text hammerLabel;
        [SerializeField] Button workerButton;
        [SerializeField] TMP_Text workerLabel;

        GameController game;
        float hintTimer;

        public void Bind(GameController controller)
        {
            game = controller;
            game.Economy.Changed += Refresh;
            game.Hammer.Changed += Refresh;
            game.Workers.Changed += Refresh;
            game.SiteChanged += Refresh;
            game.Hint += ShowHint;

            hammerButton.onClick.AddListener(game.BuyHammer);
            workerButton.onClick.AddListener(game.BuyWorker);
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
            game.SiteChanged -= Refresh;
            game.Hint -= ShowHint;
        }

        void ShowHint(string text) => ShowHint(text, 2.5f);

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

            // The text changes on every hit; cheap enough to refresh each frame and avoids an extra event.
            if (game.AllDone) return;
            var focus = game.FocusSite;
            buildingText.text = focus != null
                ? $"{focus.Plan.Name}   Kat {focus.FloorsBuilt}/{focus.Plan.Floors}   %{Mathf.FloorToInt(focus.Fraction * 100f)}"
                : $"İnşaatlar  {game.CompletedCount}/{game.TotalCount}";
        }

        void Refresh()
        {
            coinsText.text = NumberFormat.Compact(game.Economy.Coins);

            hammerLabel.text = game.Hammer.IsMaxed
                ? "ÇEKİÇ\nEn yüksek seviye"
                : $"ÇEKİÇ  Sv.{game.Hammer.Level + 1}\nVuruş {game.HammerPower} (+1)  •  {NumberFormat.Compact(game.Hammer.NextCost)}";
            hammerButton.interactable = !game.Hammer.IsMaxed && game.Economy.CanAfford(game.Hammer.NextCost);

            workerLabel.text = game.Workers.IsMaxed
                ? $"İŞÇİ  {game.Workers.Level}\nEkip tamam"
                : $"İŞÇİ AL  ({game.Workers.Level}/{Balance.MaxWorkers})\n{NumberFormat.Compact(game.Workers.NextCost)}";
            workerButton.interactable = !game.Workers.IsMaxed && game.Economy.CanAfford(game.Workers.NextCost);

            if (game.AllDone)
            {
                buildingText.text = "";
                victoryText.gameObject.SetActive(true);
                victoryText.text = "Şehir tamamlandı!\nHarika iş çıkardın";
            }
        }
    }
}
