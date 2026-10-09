using System.Globalization;

namespace Game.Core
{
    public static class NumberFormat
    {
        /// <summary>1234 -> "1.2K", 2_500_000 -> "2.5M". Culture-invariant so builds behave the same everywhere.</summary>
        public static string Compact(long value)
        {
            var c = CultureInfo.InvariantCulture;
            if (value < 10_000) return value.ToString("0", c);
            if (value < 1_000_000) return (value / 1000d).ToString("0.#", c) + "K";
            if (value < 1_000_000_000) return (value / 1_000_000d).ToString("0.#", c) + "M";
            return (value / 1_000_000_000d).ToString("0.#", c) + "B";
        }
    }
}
