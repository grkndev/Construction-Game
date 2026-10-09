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
        [SerializeField] Game.World.Character player;

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            var shot = Arg(args, "-shot");
            var demo = Array.IndexOf(args, "-demo") >= 0;
            if (shot == null && !demo) yield break;
            Application.runInBackground = true;   // automated runs must not stall when the window isn't focused

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
            var resting = 0;
            foreach (var c in FindObjectsByType<Game.World.Character>(FindObjectsSortMode.None)) if (c.Energy.IsExhausted) resting++;
            Debug.Log($"[Dev] workers per plot: {spread}; characters resting: {resting}");
            Debug.Log($"[Dev] Saved screenshot {shot} ({tex.width}x{tex.height}), coins={game.Economy.Coins}, done={game.CompletedCount}/{game.TotalCount}");
            Application.Quit();
        }

        IEnumerator Demo()
        {
            // Pretend to be a fast player: ~8 hits per second, upgrades bought whenever affordable, colas drunk when found.
            var wait = new WaitForSeconds(0.12f);
            var elapsed = 0f;
            var boosted = false;
            while (!game.AllDone)
            {
                elapsed += 0.12f;
                if (!boosted && elapsed > 20f)   // exercise the bought-cola path once so its effects show up in screenshots
                {
                    boosted = true;
                    game.Economy.Add(game.ColaPrice);
                    game.BuyCola();
                }
                // Like a sensible player: finish the smallest unfinished plot.
                Game.World.BuildSite site = null;
                foreach (var candidate in game.Sites)
                {
                    if (candidate.State != Game.World.SiteState.Active) continue;
                    if (site == null || candidate.Plan.TotalWork < site.Plan.TotalWork) site = candidate;
                }
                if (site != null) game.TryHit(site, game.HammerPower, site.transform.position + Vector3.up, player);
                game.BuyWorker();
                game.BuyHammer();
                game.BuyStamina();
                game.UseCola();
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
