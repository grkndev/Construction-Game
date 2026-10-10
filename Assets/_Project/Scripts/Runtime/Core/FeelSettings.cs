using UnityEngine;

namespace Game.Core
{
    /// <summary>Player choices about game feel (set in the settings window, remembered in PlayerPrefs).</summary>
    public static class FeelSettings
    {
        const string ShakeKey = "feel.shake";
        static int shake = -1;   // -1 = not read yet

        /// <summary>Whether a hammer hit shakes the screen. On by default.</summary>
        public static bool ShakeEnabled
        {
            get
            {
                if (shake < 0) shake = PlayerPrefs.GetInt(ShakeKey, 1) != 0 ? 1 : 0;
                return shake != 0;
            }
            set
            {
                shake = value ? 1 : 0;
                PlayerPrefs.SetInt(ShakeKey, shake);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Forgets the cached value so the next read comes from PlayerPrefs again (tests).</summary>
        public static void ResetCache() => shake = -1;
    }
}
