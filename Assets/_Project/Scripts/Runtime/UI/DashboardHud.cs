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
        const float CardHeightAboveGround = 4f;    // cards float a little above their province

        [SerializeField] GameObject root;
        [SerializeField] DistrictCard[] cards;
        [SerializeField] TMP_Text worldName;
        [SerializeField] Button previousWorld;
        [SerializeField] Button nextWorld;
        [SerializeField] UiPlate tipPlate;

        static readonly Color Open = new Color(1f, 0.76f, 0.25f);
        static readonly Color Done = new Color(0.5f, 0.92f, 0.55f);
        static readonly Color Gold = new Color(1f, 0.97f, 0.55f);
        static readonly Color Locked = new Color(0.72f, 0.7f, 0.78f);

        GameController game;
        Camera cam;

        public void Bind(GameController controller)
        {
            game = controller;
            game.ModeChanged += Refresh;
            game.StateChanged += Refresh;

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
                {
                    // A finished district shows its beautifying: stars for the decorations, and a golden card when all six stand.
                    var deco = state.Decorations;
                    var status = deco.IsComplete ? "ALTIN YILDIZ" : deco.Built > 0 ? $"Tamamlandı  •  Süs {deco.Built}/{Balance.DecorationCount}" : "Tamamlandı";
                    if (state.IssueCount > 0) status += $"  •  {state.IssueCount} sorun";
                    cards[i].Show(def.Name, status, deco.IsComplete ? Gold : state.IssueCount > 0 ? Open : Done, locked: false);
                }
                else
                    cards[i].Show(def.Name, $"{state.CompletedCount}/{state.PlotCount} bina  %{Mathf.FloorToInt(state.Fraction * 100f)}", Open, locked: false);
            }
            tipPlate.Text = game.CityComplete ? "Şehir tamamlandı! Bozulan binaları onarmaya devam et" : "Bir mahalle seç";
        }

        void LateUpdate()
        {
            if (game == null || game.Mode != GameMode.Dashboard) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            for (var i = 0; i < cards.Length; i++)
            {
                var world = game.Districts[i].MapPoint + Vector3.up * CardHeightAboveGround;
                var screen = cam.WorldToScreenPoint(world);
                if (cards[i].Rect.position != screen) cards[i].Rect.position = screen;   // the map camera is still: a write would re-layout the canvas for nothing
            }
        }
    }
}
