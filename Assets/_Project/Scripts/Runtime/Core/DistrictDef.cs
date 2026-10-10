using System;

namespace Game.Core
{
    /// <summary>
    /// Static description of one district: its name, the player level that unlocks it and its buildings.
    /// <see cref="Scale"/> multiplies everything money-shaped (work, rewards, upgrade prices, hit power), so each
    /// district plays like the first one at a bigger number and a district's upgrades can start again from zero.
    /// </summary>
    public sealed class DistrictDef
    {
        public string Name { get; }
        public int RequiredLevel { get; }
        public int Scale { get; }
        public BuildPlan[] Plans { get; }

        /// <summary>Plans are given in first-district numbers and scaled here.</summary>
        public DistrictDef(string name, int requiredLevel, int scale, params BuildPlan[] basePlans)
        {
            Name = name;
            RequiredLevel = requiredLevel;
            Scale = scale;
            Plans = Array.ConvertAll(basePlans, p =>
                new BuildPlan(p.Name, p.Floors, p.WorkPerFloor * scale, p.CompletionBonus * scale, p.Xp, p.Kind, p.Size, p.Variant));
        }
    }
}
