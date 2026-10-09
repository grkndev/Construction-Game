using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>A tappable label floating over one district on the dashboard.</summary>
    public sealed class DistrictCard : MonoBehaviour
    {
        [SerializeField] RectTransform rect;
        [SerializeField] Button button;
        [SerializeField] Image back;
        [SerializeField] TMP_Text label;

        public RectTransform Rect => rect;
        public Button Button => button;

        public void Show(string name, string statusText, Color tint, bool locked)
        {
            label.text = $"{name}\n<size=68%>{statusText}</size>";
            back.color = tint;
            label.color = locked ? new Color(0.35f, 0.33f, 0.4f) : new Color(0.17f, 0.13f, 0.2f);
        }
    }
}
