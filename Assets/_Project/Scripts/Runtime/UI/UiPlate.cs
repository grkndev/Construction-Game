using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// A text with a translucent dark backing that grows with the text (layout components on the same object).
    /// An empty text hides the whole plate, so no empty dark boxes are left on screen.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiPlate : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        CanvasGroup group;

        public string Text
        {
            get => label.text;
            set
            {
                if (label.text != value) label.text = value;
                var show = !string.IsNullOrEmpty(value);
                if (gameObject.activeSelf != show) gameObject.SetActive(show);
            }
        }

        public float Alpha
        {
            set
            {
                if (group == null) group = GetComponent<CanvasGroup>();
                group.alpha = value;
            }
        }
    }
}
