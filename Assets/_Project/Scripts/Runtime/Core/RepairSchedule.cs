using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Decides when finished buildings get a problem. One problem every <see cref="Balance.RepairInterval"/>
    /// seconds per district, at most <see cref="Balance.MaxIssuesPerDistrict"/> open at once, at most one per
    /// building. Time away counts, but the caps mean a long absence never piles up more than that.
    /// The clock is passed in (seconds) and the randomness is injected, so it is deterministic in tests.
    /// </summary>
    public static class RepairSchedule
    {
        /// <summary>Advances the district's repair clock; returns the plots that just got a problem.</summary>
        public static List<int> Advance(DistrictState state, double elapsedSeconds, Random rng)
        {
            var created = new List<int>();
            if (elapsedSeconds <= 0.0) return created;

            state.RepairClock += (float)elapsedSeconds;
            while (state.RepairClock >= Balance.RepairInterval)
            {
                state.RepairClock -= Balance.RepairInterval;

                var candidates = new List<int>();
                for (var i = 0; i < state.PlotCount; i++)
                    if (state.IsPlotComplete(i) && state.Issues[i] == 0) candidates.Add(i);

                if (candidates.Count == 0 || state.IssueCount >= Balance.MaxIssuesPerDistrict)
                {
                    state.RepairClock = 0f;   // nothing can break right now: don't bank the time
                    break;
                }

                var plot = candidates[rng.Next(candidates.Count)];
                state.Break(plot, rng.Next(1, Balance.RepairKinds + 1));
                created.Add(plot);
            }
            return created;
        }
    }
}
