using System;

namespace Game.Core
{
    /// <summary>Command-line flags of a built player (<c>-demo</c>, <c>-reset</c>, ...), read once.</summary>
    public static class LaunchArgs
    {
        static readonly string[] Args = Environment.GetCommandLineArgs();

        public static bool Has(string flag) => Array.IndexOf(Args, flag) >= 0;

        public static string Value(string flag)
        {
            var i = Array.IndexOf(Args, flag);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null;
        }
    }
}
