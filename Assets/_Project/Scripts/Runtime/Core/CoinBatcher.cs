using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Collects the coins earned by hired workers and hands them out as one batch every <c>interval</c> seconds, so eight helpers hitting
    /// ten times a second do not send eighty coins flying across the screen. Plain logic, driven by the caller's clock.
    /// </summary>
    public sealed class CoinBatcher
    {
        readonly float interval;
        float timer;
        long pending;
        Vector3 point;

        public CoinBatcher(float interval = 0.4f) => this.interval = interval;

        public long Pending => pending;

        public void Add(long coins, Vector3 where)
        {
            if (coins <= 0) return;
            pending += coins;
            point = where;   // the batch flies from wherever the latest hit landed
        }

        /// <summary>Advances the clock; true (with the batch) when one is due and there is something to send.</summary>
        public bool Tick(float deltaTime, out long coins, out Vector3 where)
        {
            timer += deltaTime;
            coins = 0;
            where = point;
            if (timer < interval) return false;
            timer = 0f;
            if (pending <= 0) return false;
            coins = pending;
            pending = 0;
            return true;
        }

        public void Clear()
        {
            pending = 0;
            timer = 0f;
        }
    }
}
