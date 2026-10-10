using System;

namespace Game.Core
{
    /// <summary>On-disk save format (JsonUtility: public fields, arrays only). Bump <see cref="Version"/> on breaking changes.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public const string DefaultMapId = "cekickoy";

        public int version = CurrentVersion;
        public string mapId = DefaultMapId;
        public long coins;   // legacy (before coins became per-district): read by nobody, kept so old saves still parse
        public int xp;
        public int cola;
        public long savedAtUtcTicks;
        public bool openingSeen;
        public DistrictSave[] districts = new DistrictSave[0];
    }

    [Serializable]
    public sealed class DistrictSave
    {
        public int hammer;
        public int workers;
        public int stamina;
        public int speed;
        public long coins;
        public int[] plotWork = new int[0];
        public int[] issues = new int[0];        // per plot: 0 = fine, 1..N = kind of problem
        public int[] repairWork = new int[0];    // per plot: repair progress
        public float repairClock;                // seconds towards the next problem
        public bool introSeen;
        public int decorationsBuilt;          // finished decorations (older saves: 0)
        public bool decorationOrdered;        // one is being built
        public float decorationProgress;
    }
}
