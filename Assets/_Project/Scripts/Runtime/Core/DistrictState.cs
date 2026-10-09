using System;

namespace Game.Core
{
    /// <summary>
    /// Everything the player owns inside one district: its upgrades, worker count, how far each plot is built and
    /// which finished buildings currently have a problem. Plain C# so it can be saved and unit-tested.
    /// </summary>
    public sealed class DistrictState
    {
        public DistrictDef Def { get; }
        public UpgradeTrack Hammer { get; }
        public UpgradeTrack Workers { get; }
        public UpgradeTrack Stamina { get; }
        public UpgradeTrack Speed { get; }

        public int[] PlotWork { get; }
        /// <summary>Per plot: 0 = fine, otherwise the kind of problem (1..<see cref="Balance.RepairKinds"/>).</summary>
        public int[] Issues { get; }
        public int[] RepairWork { get; }
        public float RepairClock { get; set; }
        public bool IntroSeen { get; set; }

        public int PlotCount => Def.Plans.Length;

        public DistrictState(DistrictDef def)
        {
            Def = def;
            Hammer = Balance.NewHammerTrack(def.Scale);
            Workers = Balance.NewWorkerTrack(def.Scale);
            Stamina = Balance.NewStaminaTrack(def.Scale);
            Speed = Balance.NewSpeedTrack(def.Scale);
            PlotWork = new int[def.Plans.Length];
            Issues = new int[def.Plans.Length];
            RepairWork = new int[def.Plans.Length];
        }

        public bool IsPlotComplete(int plot) => PlotWork[plot] >= Def.Plans[plot].TotalWork;

        public int CompletedCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < PlotCount; i++) if (IsPlotComplete(i)) n++;
                return n;
            }
        }

        public bool IsComplete => CompletedCount == PlotCount;

        public int IssueCount
        {
            get
            {
                var n = 0;
                foreach (var kind in Issues) if (kind != 0) n++;
                return n;
            }
        }

        /// <summary>Overall progress 0..1 (work done / work needed), for the dashboard card.</summary>
        public float Fraction
        {
            get
            {
                long done = 0, total = 0;
                for (var i = 0; i < PlotCount; i++)
                {
                    done += Math.Min(PlotWork[i], Def.Plans[i].TotalWork);
                    total += Def.Plans[i].TotalWork;
                }
                return total == 0 ? 1f : (float)done / total;
            }
        }

        public void Break(int plot, int kind)
        {
            if (!IsPlotComplete(plot) || Issues[plot] != 0) return;
            Issues[plot] = kind;
            RepairWork[plot] = 0;
        }

        /// <summary>Adds repair work; returns true when the problem is fixed by this call.</summary>
        public bool AddRepairWork(int plot, int work)
        {
            if (Issues[plot] == 0 || work <= 0) return false;
            RepairWork[plot] += work;
            if (RepairWork[plot] < Balance.RepairWork(Def.Scale)) return false;
            Issues[plot] = 0;
            RepairWork[plot] = 0;
            return true;
        }

        public DistrictSave ToSave() => new DistrictSave
        {
            hammer = Hammer.Level,
            workers = Workers.Level,
            stamina = Stamina.Level,
            speed = Speed.Level,
            plotWork = (int[])PlotWork.Clone(),
            issues = (int[])Issues.Clone(),
            repairWork = (int[])RepairWork.Clone(),
            repairClock = RepairClock,
            introSeen = IntroSeen,
        };

        /// <summary>Applies a save; missing or short arrays (older saves, new plots) are tolerated.</summary>
        public void Restore(DistrictSave save)
        {
            if (save == null) return;
            Hammer.SetLevel(save.hammer);
            Workers.SetLevel(save.workers);
            Stamina.SetLevel(save.stamina);
            Speed.SetLevel(save.speed);
            for (var i = 0; i < PlotCount; i++)
            {
                PlotWork[i] = Math.Max(0, Math.Min(Def.Plans[i].TotalWork, Get(save.plotWork, i)));
                Issues[i] = IsPlotComplete(i) ? Math.Max(0, Math.Min(Balance.RepairKinds, Get(save.issues, i))) : 0;
                RepairWork[i] = Issues[i] != 0 ? Math.Max(0, Get(save.repairWork, i)) : 0;
            }
            RepairClock = Math.Max(0f, save.repairClock);
            IntroSeen = save.introSeen;
        }

        static int Get(int[] values, int i) => values != null && i < values.Length ? values[i] : 0;
    }
}
