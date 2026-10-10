using System.Collections.Generic;

namespace Game.Core
{
    public enum CeremonyTier { Small, Medium, Large }

    public enum CeremonyKind { Building, Level, District, City, Gold }

    public readonly struct CeremonyRequest
    {
        public readonly CeremonyKind Kind;
        public readonly CeremonyTier Tier;
        public readonly string Title;
        public readonly string Subtitle;
        public readonly float Seconds;

        public CeremonyRequest(CeremonyKind kind, CeremonyTier tier, string title, string subtitle, float seconds)
        {
            Kind = kind;
            Tier = tier;
            Title = title;
            Subtitle = subtitle;
            Seconds = seconds;
        }
    }

    /// <summary>
    /// Decides which celebration plays next. One at a time; bigger ones go first; small ones are dropped when they would only pile up
    /// (a burst of finished buildings must not leave the player staring at a queue of banners). Plain logic.
    /// </summary>
    public sealed class CeremonyQueue
    {
        public const int MaxWaitingSmall = 2;

        readonly List<CeremonyRequest> waiting = new List<CeremonyRequest>();

        public bool Busy { get; private set; }
        public int Waiting => waiting.Count;

        public void Request(CeremonyRequest request)
        {
            if (request.Tier == CeremonyTier.Small)
            {
                var smalls = 0;
                foreach (var other in waiting)
                {
                    if (other.Tier > CeremonyTier.Small) return;   // something bigger is already waiting: a small banner would only clutter
                    smalls++;
                }
                if (smalls >= MaxWaitingSmall) return;
            }
            waiting.Add(request);
        }

        /// <summary>The next ceremony to show, if none is running: the biggest waiting one, oldest first among equals.</summary>
        public bool TryNext(out CeremonyRequest next)
        {
            next = default;
            if (Busy || waiting.Count == 0) return false;
            var best = 0;
            for (var i = 1; i < waiting.Count; i++)
                if (waiting[i].Tier > waiting[best].Tier) best = i;
            next = waiting[best];
            waiting.RemoveAt(best);
            Busy = true;
            return true;
        }

        /// <summary>The running ceremony is over.</summary>
        public void Finish() => Busy = false;

        public void Clear()
        {
            waiting.Clear();
            Busy = false;
        }
    }
}
