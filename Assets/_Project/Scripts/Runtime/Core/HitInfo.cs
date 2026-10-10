using UnityEngine;

namespace Game.Core
{
    /// <summary>One landed hammer hit as the presentation layer sees it (where, how many coins, who swung).</summary>
    public readonly struct HitInfo
    {
        public readonly Vector3 Point;
        public readonly long Coins;
        public readonly bool ByPlayer;

        public HitInfo(Vector3 point, long coins, bool byPlayer)
        {
            Point = point;
            Coins = coins;
            ByPlayer = byPlayer;
        }
    }
}
