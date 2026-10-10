using System.Collections;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Command-line hooks for automated checks of a built player (no effect unless the flags are passed):
    /// <c>-shot &lt;file.png&gt;</c> saves the game's own frame (only our window, never the desktop) after
    /// <c>-shotDelay &lt;seconds&gt;</c> and quits; <c>-demo</c> auto-plays (visits every district in turn, stands next to
    /// the active plot so the player hammers automatically, buys upgrades); <c>-enter N</c> opens district N, <c>-finish N</c> completes district N's buildings (<c>-finishAll</c>: the whole city), <c>-break N</c> gives two of them a problem,
    /// <c>-xp N</c> / <c>-coins N</c> grant XP / coins (to the entered district), <c>-shop</c> opens the shop panel, <c>-settings</c> the settings window, <c>-ceremony coins|building|level|district|gold|city</c> plays that effect once (use a short <c>-shotDelay</c>), <c>-event chest|birds|pet</c> starts a small event now, <c>-exit</c> returns to the map, <c>-order</c> orders the next decoration, <c>-shoptab decor</c> opens the shop on the decorations tab, <c>-decorate N</c> builds N decorations at once (use with <c>-finish</c>), <c>-workers N</c> / <c>-hammer N</c> buy that many helpers / hammer upgrades (after <c>-coins</c>), <c>-stats</c> logs how many renderers / triangles are live and how long the first district visit took, <c>-at x,z</c> drops the player at that offset from the district centre (the walkable area pulls them back inside).
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
            if (LaunchArgs.Has("-finishAll"))
                for (var i = 0; i < game.Districts.Count; i++)
                {
                    game.EnsureBuilt(i);
                    foreach (var site in game.Districts[i].Sites) site.AddWork(int.MaxValue / 2, site.transform.position);
                }
            if (int.TryParse(LaunchArgs.Value("-finish"), out var finish))
            {
                game.EnsureBuilt(finish);
                foreach (var site in game.Districts[finish].Sites) site.AddWork(int.MaxValue / 2, site.transform.position);
            }
            if (int.TryParse(LaunchArgs.Value("-break"), out var broken))
            {
                game.ForceIssue(broken, 0, 1);
                game.ForceIssue(broken, 1, 3);
            }
            var visit = System.Diagnostics.Stopwatch.StartNew();
            if (int.TryParse(LaunchArgs.Value("-enter"), out var district)) game.EnterDistrict(district);
            var visitMs = visit.Elapsed.TotalMilliseconds;
            if (long.TryParse(LaunchArgs.Value("-coins"), out var coins)) game.Economy.Add(coins);   // goes to the district just entered
            if (int.TryParse(LaunchArgs.Value("-workers"), out var hired)) for (var i = 0; i < hired; i++) game.BuyWorker();   // needs -coins
            if (int.TryParse(LaunchArgs.Value("-hammer"), out var upgrades)) for (var i = 0; i < upgrades; i++) game.BuyHammer();
            var at = LaunchArgs.Value("-at")?.Split(',');
            if (at != null && at.Length == 2 && game.InDistrict
                && float.TryParse(at[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atX)
                && float.TryParse(at[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atZ))
            {
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.position = game.CurrentDistrict.Center + new Vector3(atX, 0f, atZ);
                controller.enabled = true;
            }
            if (int.TryParse(LaunchArgs.Value("-decorate"), out var decorated)) game.DevDecorate(decorated);
            if (LaunchArgs.Has("-order")) game.OrderDecoration();
            if (LaunchArgs.Has("-exit")) game.ExitToDashboard();   // back to the map (to see what a visit changed there)   // pays for the next decoration (needs -finish and -coins)
            var spawnEvent = LaunchArgs.Value("-event");
            if (spawnEvent != null) FindFirstObjectByType<Game.World.EventSpawner>()?.Spawn(spawnEvent);
            var ceremony = LaunchArgs.Value("-ceremony");
            if (ceremony != null) FindFirstObjectByType<Game.Feel.FeelDirector>()?.Demo(ceremony);
            if (LaunchArgs.Has("-shop")) game.ToggleShop();
            if (LaunchArgs.Value("-shoptab") == "decor") FindFirstObjectByType<Game.UI.ShopPanel>()?.ShowTab(true);
            if (LaunchArgs.Has("-settings")) FindFirstObjectByType<Game.UI.SettingsPanel>()?.Open();
            if (demo) StartCoroutine(Demo());

            if (shot == null) yield break;
            float.TryParse(LaunchArgs.Value("-shotDelay"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var delay);
            yield return new WaitForSeconds(Mathf.Max(0.2f, delay));   // short delays catch effects mid-flight
            yield return new WaitForEndOfFrame();
            if (LaunchArgs.Has("-stats")) LogStats(visitMs);
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

        // Live cost of the frame on screen: active renderers, how many cast shadows, triangles, and the district visit time.
        void LogStats(double visitMs)
        {
            var renderers = 0;
            var casters = 0;
            long triangles = 0;
            foreach (var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var meshRenderer = filter.GetComponent<MeshRenderer>();
                if (meshRenderer == null || !meshRenderer.enabled || filter.sharedMesh == null) continue;
                renderers++;
                if (meshRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) casters++;
                triangles += filter.sharedMesh.GetIndexCount(0) / 3;
            }
            // Walkers (helpers, villagers, strays) must not overlap each other or stand inside a solid structure.
            var walkers = new System.Collections.Generic.List<Transform>();
            foreach (var w in FindObjectsByType<Game.World.AutoWorker>(FindObjectsSortMode.None)) walkers.Add(w.transform);
            foreach (var w in FindObjectsByType<Game.World.Resident>(FindObjectsSortMode.None)) walkers.Add(w.transform);
            foreach (var w in FindObjectsByType<Game.World.Pet>(FindObjectsSortMode.None)) walkers.Add(w.transform);
            var overlapping = 0;
            var inside = 0;
            for (var i = 0; i < walkers.Count; i++)
            {
                for (var j = i + 1; j < walkers.Count; j++)
                    if (Vector3.ProjectOnPlane(walkers[i].position - walkers[j].position, Vector3.up).magnitude < 0.5f) overlapping++;
                foreach (var hit in Physics.OverlapSphere(walkers[i].position + Vector3.up * 0.9f, 0.1f))
                    if (!(hit is CharacterController) && !hit.isTrigger) { inside++; break; }
            }
            var status = new System.Collections.Generic.Dictionary<string, int>();
            var unsticks = 0;
            foreach (var w in FindObjectsByType<Game.World.AutoWorker>(FindObjectsSortMode.None)) unsticks += w.Unsticks;
            foreach (var w in FindObjectsByType<Game.World.AutoWorker>(FindObjectsSortMode.None)) status[w.Status] = (status.TryGetValue(w.Status, out var n) ? n : 0) + 1;
            if (game.InDistrict) Debug.Log($"[Decor] built={game.Decorations.Built} ordered={game.Decorations.Ordered} progress={game.Decorations.Progress:0.00}");
            Debug.Log($"[Walkers] count={walkers.Count} overlappingPairs={overlapping} insideSolids={inside} unsticks={unsticks} helpers: " + string.Join(", ", System.Linq.Enumerable.Select(status, kv => $"{kv.Key}={kv.Value}")));
            Debug.Log($"[Perf] renderers={renderers} shadowCasters={casters} triangles={triangles} gameObjects={FindObjectsByType<Transform>(FindObjectsSortMode.None).Length} firstVisitMs={visitMs:0.0}");
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
