using Game.Core;
using UnityEngine;

namespace Game.Feel
{
    /// <summary>A flag on the map for every finished district. A flag that is earned during play pops up; flags of a loaded save are simply there.</summary>
    public sealed class MapFlags : MonoBehaviour
    {
        [SerializeField] Transform[] flags;   // one per district, switched on when the district is complete
        [SerializeField] Material gold;       // the cloth turns gold when the district is fully beautified (golden star)

        GameController game;
        bool[] shown;
        bool[] golden;
        Material[] cloth;
        float[] popTime;

        /// <summary>Raised when a flag appears for the first time (the director adds sparkles).</summary>
        public event System.Action<int, Vector3> Planted;

        public void Bind(GameController controller)
        {
            game = controller;
            shown = new bool[flags.Length];
            golden = new bool[flags.Length];
            cloth = new Material[flags.Length];
            for (var i = 0; i < flags.Length; i++) cloth[i] = flags[i].Find("Cloth").GetComponent<Renderer>().sharedMaterial;
            popTime = new float[flags.Length];
            game.StateChanged += Refresh;
            Refresh(animate: false);
        }

        void OnDestroy()
        {
            if (game != null) game.StateChanged -= Refresh;
        }

        void Refresh() => Refresh(animate: true);

        void Refresh(bool animate)
        {
            for (var i = 0; i < flags.Length; i++)
            {
                var state = game.StateOf(i);
                var gold = state.Decorations.IsComplete;
                if (gold != golden[i])
                {
                    golden[i] = gold;
                    flags[i].Find("Cloth").GetComponent<Renderer>().sharedMaterial = gold ? this.gold : cloth[i];
                }
                var complete = state.IsComplete;
                if (complete == shown[i]) continue;
                shown[i] = complete;
                flags[i].gameObject.SetActive(complete);
                if (!complete) continue;
                popTime[i] = animate ? 0f : float.MaxValue;
                flags[i].localScale = animate ? Vector3.zero : Vector3.one;
                if (animate) Planted?.Invoke(i, flags[i].position);
            }
        }

        void Update()
        {
            for (var i = 0; i < flags.Length; i++)
            {
                if (!shown[i] || popTime[i] >= 0.6f) continue;
                popTime[i] += Time.unscaledDeltaTime;
                flags[i].localScale = Vector3.one * Easing.OutBack(popTime[i] / 0.6f);
                if (popTime[i] >= 0.6f) flags[i].localScale = Vector3.one;
            }
        }
    }
}
