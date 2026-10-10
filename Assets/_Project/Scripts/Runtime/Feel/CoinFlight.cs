using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feel
{
    /// <summary>
    /// Coins earned by hammer hits fly from the building to the coin counter in the corner (in an arc, one after another), and the counter
    /// swells for every coin that arrives. A fixed pool of images: at most <see cref="PoolSize"/> coins in the air, extras are dropped.
    /// </summary>
    public sealed class CoinFlight : MonoBehaviour
    {
        const int PoolSize = 24;
        const float Duration = 0.6f;

        [SerializeField] Image[] coins = new Image[PoolSize];
        [SerializeField] RectTransform target;     // the coin counter; coins fly to its centre and it swells on arrival

        struct Flight
        {
            public bool Active;
            public Vector2 Start, Control, End;
            public float Time, Delay;
        }

        Flight[] flights;
        Camera cam;
        float swell;

        /// <summary>A coin reached the counter.</summary>
        public event Action Landed;

        void Awake()
        {
            flights = new Flight[coins.Length];
            foreach (var coin in coins) coin.gameObject.SetActive(false);
        }

        /// <summary>Launches <paramref name="count"/> coins from a world point.</summary>
        public void Fly(Vector3 worldPoint, int count)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var from = (Vector2)cam.WorldToScreenPoint(worldPoint);
            var to = (Vector2)target.position;
            for (var n = 0; n < count; n++)
            {
                var slot = FreeSlot();
                if (slot < 0) return;   // everything is in the air already
                var control = (from + to) * 0.5f + new Vector2(UnityEngine.Random.Range(-140f, 140f), 190f);
                flights[slot] = new Flight
                {
                    Active = true,
                    Start = from + UnityEngine.Random.insideUnitCircle * 18f,
                    Control = control,
                    End = to,
                    Delay = n * 0.06f,
                };
                coins[slot].gameObject.SetActive(false);   // shown when its delay is over
            }
        }

        int FreeSlot()
        {
            for (var i = 0; i < flights.Length; i++) if (!flights[i].Active) return i;
            return -1;
        }

        /// <summary>Hides everything in flight (leaving the district).</summary>
        public void Clear()
        {
            for (var i = 0; i < flights.Length; i++)
            {
                flights[i].Active = false;
                coins[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < flights.Length; i++)
            {
                if (!flights[i].Active) continue;
                var f = flights[i];
                if (f.Delay > 0f)
                {
                    f.Delay -= dt;
                    flights[i] = f;
                    continue;
                }
                f.Time += dt;
                var t = f.Time / Duration;
                var coin = coins[i];
                if (t >= 1f)
                {
                    f.Active = false;
                    flights[i] = f;
                    coin.gameObject.SetActive(false);
                    swell = 1f;
                    Landed?.Invoke();
                    continue;
                }
                flights[i] = f;
                if (!coin.gameObject.activeSelf) coin.gameObject.SetActive(true);
                var e = t * t * (3f - 2f * t);   // smoothstep: leaves slowly, snaps into the counter
                var a = Vector2.Lerp(f.Start, f.Control, e);
                var b = Vector2.Lerp(f.Control, f.End, e);
                coin.rectTransform.position = Vector2.Lerp(a, b, e);
                coin.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, Mathf.Sin(Mathf.Min(1f, t * 2.2f) * Mathf.PI * 0.5f)) * (t > 0.8f ? Mathf.Lerp(1f, 0.6f, (t - 0.8f) * 5f) : 1f);
            }

            if (swell > 0f && target != null)
            {
                swell = Mathf.MoveTowards(swell, 0f, dt * 6f);
                target.localScale = Vector3.one * (1f + 0.1f * swell);
            }
        }
    }
}
