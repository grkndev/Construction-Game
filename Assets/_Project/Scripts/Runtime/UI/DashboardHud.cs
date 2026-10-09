using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The map screen: the whole island seen from far away with one card per district (progress, lock, level needed),
    /// plus the world name with arrows to the neighbouring worlds. Visible only in <see cref="GameMode.Dashboard"/>.
    /// </summary>
    public sealed class DashboardHud : MonoBehaviour
    {
        const float CardHeightAboveGround = 11f;   // cards float above the district so they do not hide its buildings

        [SerializeField] GameObject root;
        [SerializeField] DistrictCard[] cards;
        [SerializeField] TMP_Text worldName;
        [SerializeField] Button previousWorld;
        [SerializeField] Button nextWorld;
        [SerializeField] UiPlate tipPlate;

        static readonly Color Open = new Color(1f, 0.86f, 0.35f);
        static readonly Color Done = new Color(0.5f, 0.92f, 0.55f);
        static readonly Color Locked = new Color(0.72f, 0.7f, 0.78f);

        GameController game;
        Camera cam;

        public void Bind(GameController controller)
        {
            game = controller;
            game.ModeChanged += Refresh;
            game.StateChanged += Refresh;
            game.Economy.Changed += Refresh;

            for (var i = 0; i < cards.Length; i++)
            {
                var index = i;
                cards[i].Button.onClick.AddListener(() => game.EnterDistrict(index));
            }
            worldName.text = "ÇEKİÇKÖY";
            // More worlds arrive with later updates; the arrows are there so the screen already reads as "one world of several".
            previousWorld.interactable = false;
            nextWorld.onClick.AddListener(() => game.ShowHint("Yeni dünyalar güncellemeyle gelecek"));
            Refresh();
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.ModeChanged -= Refresh;
            game.StateChanged -= Refresh;
            game.Economy.Changed -= Refresh;
        }

        void Refresh()
        {
            var visible = game.Mode == GameMode.Dashboard;
            root.SetActive(visible);
            if (!visible) return;

            for (var i = 0; i < cards.Length; i++)
            {
                var def = Balance.Districts[i];
                var state = game.StateOf(i);
                if (!game.IsUnlocked(i))
                    cards[i].Show(def.Name, $"Seviye {def.RequiredLevel}", Locked, locked: true);
                else if (state.IsComplete)
                    cards[i].Show(def.Name, state.IssueCount > 0 ? $"Tamamlandı  •  {state.IssueCount} sorun" : "Tamamlandı",
                        state.IssueCount > 0 ? Open : Done, locked: false);
                else
                    cards[i].Show(def.Name, $"{state.CompletedCount}/{state.PlotCount} bina  %{Mathf.FloorToInt(state.Fraction * 100f)}", Open, locked: false);
            }
            tipPlate.Text = game.CityComplete ? "" : "Bir mahalle seç";
        }

        void LateUpdate()
        {
            if (game == null || game.Mode != GameMode.Dashboard) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            for (var i = 0; i < cards.Length; i++)
            {
                var world = game.Districts[i].Center + Vector3.up * CardHeightAboveGround;
                cards[i].Rect.position = cam.WorldToScreenPoint(world);
            }
        }
    }
}
