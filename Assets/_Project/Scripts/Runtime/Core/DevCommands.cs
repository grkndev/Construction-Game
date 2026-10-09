using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Command-line hooks for automated checks of a built player (no effect unless the flags are passed):
    /// <c>-shot &lt;file.png&gt;</c> saves the game's own frame (only our window, never the desktop) after
    /// <c>-shotDelay &lt;seconds&gt;</c> and quits; <c>-demo</c> auto-plays by hammering the active plot
    /// and buying upgrades, which exercises the whole loop without a human.
    /// </summary>
    public sealed class DevCommands : MonoBehaviour
    {
        [SerializeField] GameController game;

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            var shot = Arg(args, "-shot");
            var demo = Array.IndexOf(args, "-demo") >= 0;
            if (shot == null && !demo) yield break;

            if (demo) StartCoroutine(Demo());

            if (shot == null) yield break;
            float.TryParse(Arg(args, "-shotDelay"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var delay);
            yield return new WaitForSeconds(Mathf.Max(1f, delay));
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(shot, tex.EncodeToPNG());
            var spread = string.Join(",", System.Array.ConvertAll(
                FindObjectsByType<Game.World.BuildSite>(FindObjectsSortMode.InstanceID), x => $"{x.Plan.Name}:{x.AssignedWorkers}{(x.IsComplete ? "(done)" : "")}"));
            Debug.Log($"[Dev] workers per plot: {spread}");
            Debug.Log($"[Dev] Saved screenshot {shot} ({tex.width}x{tex.height}), coins={game.Economy.Coins}, done={game.CompletedCount}/{game.TotalCount}");
            Application.Quit();
        }

        IEnumerator Demo()
        {
            // Pretend to be a fast player: ~8 hits per second, upgrades bought whenever affordable.
            var wait = new WaitForSeconds(0.12f);
            while (!game.AllDone)
            {
                var site = game.NearestWorkableSite(Vector3.zero, 1000f);   // any unfinished plot
                if (site != null) game.TryHit(site, game.HammerPower, site.transform.position + Vector3.up);
                game.BuyWorker();
                game.BuyHammer();
                yield return wait;
            }
        }

        static string Arg(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
