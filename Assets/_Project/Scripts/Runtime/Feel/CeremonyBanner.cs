using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feel
{
    /// <summary>
    /// The ribbon in the middle of the screen for the big moments (a finished building, a level, a finished district, the finished city).
    /// Pops in, holds, fades out, on unscaled time so a banner never stalls. The kind decides the colour and the flourish: stars burst out
    /// for a level, the title slams down like a stamp for a district.
    /// </summary>
    public sealed class CeremonyBanner : MonoBehaviour
    {
        const float InSeconds = 0.38f, OutSeconds = 0.4f;

        [SerializeField] RectTransform root;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image ribbon;
        [SerializeField] RectTransform titleRect;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] Image[] burst;        // stars thrown outward for a level

        CeremonyRequest request;
        Action done;
        float time;
        bool playing;

        public bool IsPlaying => playing;

        void Awake() => Hide();

        public void Play(CeremonyRequest next, Action onDone)
        {
            request = next;
            done = onDone;
            time = 0f;
            playing = true;
            title.text = next.Title;
            subtitle.text = next.Subtitle;
            subtitle.gameObject.SetActive(!string.IsNullOrEmpty(next.Subtitle));
            ribbon.color = ColourFor(next.Kind);
            root.gameObject.SetActive(true);
            foreach (var star in burst) star.gameObject.SetActive(false);
            Apply();
        }

        /// <summary>Stops at once (leaving the district): no callback.</summary>
        public void Cancel()
        {
            playing = false;
            done = null;
            Hide();
        }

        static Color ColourFor(CeremonyKind kind)
        {
            switch (kind)
            {
                case CeremonyKind.Level: return new Color(0.62f, 0.5f, 1f);
                case CeremonyKind.District: return new Color(0.4f, 0.86f, 0.5f);
                case CeremonyKind.City:
                case CeremonyKind.Gold: return new Color(1f, 0.78f, 0.2f);
                default: return new Color(1f, 0.74f, 0.28f);
            }
        }

        void Hide() => root.gameObject.SetActive(false);

        void Update()
        {
            if (!playing) return;
            time += Time.unscaledDeltaTime;
            if (time >= InSeconds + request.Seconds + OutSeconds)
            {
                playing = false;
                Hide();
                var finished = done;
                done = null;
                finished?.Invoke();
                return;
            }
            Apply();
        }

        void Apply()
        {
            float scale, alpha;
            if (time < InSeconds)
            {
                var t = time / InSeconds;
                scale = Mathf.LerpUnclamped(0.5f, 1f, Easing.OutBack(t));
                alpha = Mathf.Clamp01(t * 3f);
            }
            else if (time < InSeconds + request.Seconds)
            {
                scale = 1f;
                alpha = 1f;
            }
            else
            {
                var t = (time - InSeconds - request.Seconds) / OutSeconds;
                scale = Mathf.Lerp(1f, 1.08f, t);
                alpha = 1f - t;
            }
            root.localScale = Vector3.one * scale;
            group.alpha = alpha;

            // The title of a district banner slams down like a stamp: starts huge and tilted, lands fast, shakes the ribbon once.
            if (request.Kind == CeremonyKind.District || request.Kind == CeremonyKind.City || request.Kind == CeremonyKind.Gold)
            {
                var slam = Mathf.Clamp01(time / 0.22f);
                var eased = slam * slam;
                titleRect.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, eased);
                titleRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-7f, 0f, eased));
                var impact = time - 0.22f;
                root.anchoredPosition = impact is > 0f and < 0.3f ? new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 14f * (1f - impact / 0.3f) + RestPosition : RestPosition;
            }
            else
            {
                titleRect.localScale = Vector3.one;
                titleRect.localRotation = Quaternion.identity;
            }

            if (request.Kind == CeremonyKind.Level) AnimateStars();
        }

        Vector2 RestPosition => restPosition ??= root.anchoredPosition;
        Vector2? restPosition;

        // Stars fly out of the middle of the ribbon, spinning, and fade.
        void AnimateStars()
        {
            var t = Mathf.Clamp01((time - 0.1f) / 1.1f);
            for (var i = 0; i < burst.Length; i++)
            {
                var star = burst[i];
                if (t <= 0f || t >= 1f) { star.gameObject.SetActive(false); continue; }
                star.gameObject.SetActive(true);
                var angle = i * Mathf.PI * 2f / burst.Length + 0.3f;
                var distance = Mathf.Lerp(40f, 520f + (i % 3) * 70f, Easing.OutCubic(t));
                star.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.55f) * distance;
                star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 360f * (i % 2 == 0 ? 1f : -1f));
                star.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);
                var c = star.color;
                c.a = 1f - t * t;
                star.color = c;
            }
        }
    }
}
