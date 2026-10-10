using UnityEngine;

namespace Game.Core
{
    /// <summary>How the hammer looks at a given upgrade tier: a material band (stone, steel, green, gold, white) and a head that grows. Plain maths, shared by everyone who holds a hammer.</summary>
    public static class HammerLook
    {
        public const int Bands = 5;
        const int TiersPerBand = 5;

        /// <summary>Material band for a hammer tier (tier 1 = the starting hammer): 1-5 stone, 6-10 steel, ... 21-25 white.</summary>
        public static int Band(int tier) => Mathf.Clamp((tier - 1) / TiersPerBand, 0, Bands - 1);

        /// <summary>Size of the hammer head relative to the starting one (1.0 .. 1.5 over the whole upgrade track).</summary>
        public static float HeadScale(int tier) => Mathf.Lerp(1f, 1.5f, Mathf.Clamp01((tier - 1) / (float)(Balance.MaxHammerLevel - 1)));
    }
}
