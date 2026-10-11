using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Command-line flags of a built player (<c>-demo</c>, <c>-reset</c>, ...), read once. A development WebGL build has no command line, so it reads them
    /// from the page address instead: <c>?setup&amp;enter=4&amp;finish=4&amp;workers=7</c> means <c>-setup -enter 4 -finish 4 -workers 7</c>. Release builds ignore the address.
    /// </summary>
    public static class LaunchArgs
    {
        static readonly string[] Args = Read();

        static string[] Read()
        {
            var args = Environment.GetCommandLineArgs();
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = UnityEngine.Application.absoluteURL;
            var query = url.IndexOf('?');
            if (!UnityEngine.Debug.isDebugBuild || query < 0) return args;
            var list = new List<string>(args);
            var end = url.IndexOf('#');
            foreach (var part in url.Substring(query + 1, (end < 0 ? url.Length : end) - query - 1).Split('&'))
            {
                if (part.Length == 0) continue;
                var pair = part.Split('=');
                list.Add("-" + pair[0]);
                if (pair.Length > 1) list.Add(Uri.UnescapeDataString(pair[1]));
            }
            return list.ToArray();
#else
            return args;
#endif
        }

        public static bool Has(string flag) => Array.IndexOf(Args, flag) >= 0;

        public static string Value(string flag)
        {
            var i = Array.IndexOf(Args, flag);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null;
        }
    }
}
