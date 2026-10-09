using System.Collections;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Command-line hooks for automated checks of a built player (no effect unless the flags are passed):
    /// <c>-shot &lt;file.png&gt;</c> saves the game's own frame (only our window, never the desktop) after
    /// <c>-shotDelay &lt;seconds&gt;</c> and quits; <c>-demo</c> auto-plays (visits every district in turn, stands next to
    /// the active plot so the player hammers automatically, buys upgrades); <c>-enter N</c> opens district N, <c>-finish N</c> completes district N's buildings, <c>-break N</c> gives two of them a problem,
    /// <c>-xp N</c> / <c>-coins N</c> grant XP / coins (to the entered district), <c>-shop</c> opens the shop panel.
    /// <c>-demo</c> and <c>-shot</c> never touch the real save file unless <c>-persist</c> is passed (see <see cref="GameController"/>).
    /// </summary>
    public sealed class DevCommands : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] Game.World.Character player;

        IEnumerator Start()
        {
            var shot = LaunchArgs.Value("-shot");
            var demo = LaunchArgs.Has("-demo");
            if (shot == null && !demo) yield break;
            Application.runInBackground = true;   // automated runs must not stall when the window isn't focused
            if (float.TryParse(LaunchArgs.Value("-speed"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var speed) && speed > 0f)
                Time.timeScale = speed;   // fast-forward long automated runs (the demo plays a whole city)

            yield return null;   // let GameController.Start build the dashboard first
            if (int.TryParse(LaunchArgs.Value("-xp"), out var xp)) game.Level.Add(xp);
            if (int.TryParse(LaunchArgs.Value("-finish"), out var finish))
                foreach (var site in game.Districts[finish].Sites) site.AddWork(int.MaxValue / 2, site.transform.position);
            if (int.TryParse(LaunchArgs.Value("-break"), out var broken))
            {
                game.ForceIssue(broken, 0, 1);
                game.ForceIssue(broken, 1, 3);
            }
            if (int.TryParse(LaunchArgs.Value("-enter"), out var district)) game.EnterDistrict(district);
            if (long.TryParse(LaunchArgs.Value("-coins"), out var coins)) game.Economy.Add(coins);   // goes to the district just entered
            if (LaunchArgs.Has("-shop")) game.ToggleShop();
            if (demo) StartCoroutine(Demo());

            if (shot == null) yield break;
            float.TryParse(LaunchArgs.Value("-shotDelay"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var delay);
            yield return new WaitForSeconds(Mathf.Max(1f, delay));
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(shot, tex.EncodeToPNG());
            var resting = 0;
            foreach (var c in FindObjectsByType<Game.World.Character>(FindObjectsSortMode.None)) if (c.Energy.IsExhausted) resting++;
            var progress = new System.Text.StringBuilder();
            for (var i = 0; i < game.Districts.Count; i++)
                progress.Append($"{Balance.Districts[i].Name}:{game.StateOf(i).CompletedCount}/{game.StateOf(i).PlotCount}" +
                                $"{(game.StateOf(i).IssueCount > 0 ? $"(!{game.StateOf(i).IssueCount})" : "")} ");
            Debug.Log($"[Dev] mode={game.Mode} level={game.Level.Level} xp={game.Level.Xp} characters resting={resting}; {progress}");
            Debug.Log($"[Dev] Saved screenshot {shot} ({tex.width}x{tex.height}), coins={game.Economy.Coins}, cityComplete={game.CityComplete}");
            Application.Quit();
        }

        IEnumerator Demo()
        {
            // Pretend to be a fast player: visits the districts in order, stands next to the smallest unfinished plot
            // (the player hammers by itself), buys upgrades whenever affordable, drinks colas when found.
            var wait = new WaitForSeconds(0.12f);
            var elapsed = 0f;
            var boosted = false;
            while (!game.CityComplete)
            {
                if (!game.InDistrict)
                {
                    for (var i = 0; i < game.Districts.Count; i++)
                    {
                        if (!game.IsUnlocked(i) || game.StateOf(i).IsComplete) continue;
                        game.EnterDistrict(i);
                        elapsed = 0f;
                        boosted = false;
                        break;
                    }
                    yield return wait;
                    continue;
                }

                elapsed += 0.12f;
                if (!boosted && elapsed > 20f)   // exercise the bought-cola path once per district so its effects show up in screenshots
                {
                    boosted = true;
                    game.Economy.Add(game.ColaPrice);
                    game.BuyCola();
                }
                // Like a sensible player: finish the smallest unfinished plot.
                Game.World.BuildSite site = null;
                foreach (var candidate in game.Sites)
                {
                    if (!candidate.NeedsWork) continue;
                    if (site == null || candidate.Plan.TotalWork < site.Plan.TotalWork) site = candidate;
                }
                if (site == null)
                {
                    yield return new WaitForSeconds(1.2f);   // let the last confetti play
                    game.ExitToDashboard();
                    yield return new WaitForSeconds(1.5f);   // and let the dashboard be seen
                    continue;
                }
                StandNextTo(site);
                game.BuyWorker();
                game.BuyHammer();
                game.BuyStamina();
                game.BuySpeed();
                game.UseCola();
                yield return wait;
            }
        }

        // Puts the player a little in front of the plot (towards the island centre) so it is inside the work radius.
        void StandNextTo(Game.World.BuildSite site)
        {
            var toCentre = game.CurrentDistrict.Center - site.transform.position;
            toCentre.y = 0f;
            var spot = site.transform.position + toCentre.normalized * 2.5f;
            if ((player.transform.position - spot).sqrMagnitude < 1f) return;

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(spot.x, player.transform.position.y, spot.z);
            controller.enabled = true;
        }
    }
}
