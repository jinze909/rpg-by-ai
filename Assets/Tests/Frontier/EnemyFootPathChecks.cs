using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore EnemyFootRegion(Campaign data)
        {
            var save = new SaveData { stage = 1 };
            save.flags.Add("exp_intro"); save.flags.Add("intro");
            var game = new GameCore(data, save); game.Enter(5); game.Close();
            Check(game.save.room == 5 && game.mode == "world" && game.enemies.Count == 7 && game.Position.x == 5 && game.Position.y == 14,
                "The reproduction enters the shipped seven-enemy region at its real spawn with only the earned introduction flags");
            Check(game.invulnerable == 1 && game.save.companion == -1 && !game.practicing && !game.InAbyss,
                "The natural route retains only the original one-second arrival grace, without companion, training or Abyss protection");
            return game;
        }
        private static bool EnemyFootVisible(GameCore game, Point from, Point to)
        {
            return (bool)typeof(GameCore).GetMethod("ClearAttackPath", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { from, to });
        }
        private static Point EnemyFootSweep(GameCore game, Point from, Point step)
        {
            return (Point)typeof(GameCore).GetMethod("SweepActor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { from, step });
        }
        private static void EnemyFootStep(GameCore game, float dt, Point input)
        {
            Enemy[] actors = game.enemies.ToArray(); var feet = new Point[actors.Length]; var gait = new float[actors.Length];
            var state = new int[actors.Length]; var reaction = new bool[actors.Length];
            for (int i = 0; i < actors.Length; i++)
            { feet[i] = actors[i].Position; gait[i] = actors[i].moveTime; state[i] = actors[i].state; reaction[i] = actors[i].hitTime > dt; }
            bool frozen = game.hitStop > 0, lowTide = game.LowTide; game.Tick(dt, input);
            Check(game.CanStand(game.Position), "Public walking keeps the complete player foot legal on every route frame");
            for (int i = 0; i < actors.Length; i++)
            {
                Enemy enemy = actors[i]; if (!game.enemies.Contains(enemy)) continue;
                Point step = enemy.Position - feet[i]; float speed = enemy.backpedaling ? 1.6f : enemy.kind == 0 ? 2.1f : 1.5f;
                Check(game.CanStand(enemy.Position), "Every real ordinary enemy frame retains full-foot clearance: id=" + enemy.id + " foot=" + enemy.x + "," + enemy.y);
                if (lowTide != game.LowTide && !game.CanStand(feet[i]))
                {
                    Check(!enemy.moving && enemy.moveTime == gait[i] && enemy.state == 0 && enemy.releaseTime <= 0,
                        "The existing rising-tide recovery repairs only newly flooded feet without inventing pursuit gait or a committed attack");
                    continue;
                }
                Check(step.Length <= speed * dt + .00004f,
                    "Every real ordinary enemy frame retains its original speed budget: id=" + enemy.id + " step=" + step.Length + " dt=" + dt + " speed=" + speed);
                Check((EnemyFootSweep(game, feet[i], step) - enemy.Position).Length < .00004f,
                    "The complete production footprint can sweep every recorded displacement without tunneling through house geometry");
                if (frozen) { Check(step.Length == 0 && enemy.moveTime == gait[i], "The original hit-stop freezes actual feet and gait"); continue; }
                if (reaction[i]) continue;
                if (step.Length > .001f)
                {
                    Check(enemy.moving && Math.Abs(enemy.moveTime - gait[i] - step.Length) < .00006f,
                        "Only actual swept travel advances the ordinary gait accumulator");
                    if (enemy.state == 0 && !enemy.backpedaling)
                        Check((enemy.facing - step.Normalized).Length < .0001f, "A walking actor faces its actual path step");
                }
                else Check(!enemy.moving && enemy.moveTime == gait[i], "A planted or blocked actor never invents walking animation distance");
                if (state[i] == 1) Check(step.Length == 0 && enemy.moveTime == gait[i], "Ordinary committed windups and their contact frame keep their feet planted");
            }
        }
        private static void EnemyFootWalk(GameCore game, Point waypoint, float dt)
        {
            for (int frame = 0; frame < 1500 && (waypoint - game.Position).Length > .005f; frame++)
            {
                Point delta = waypoint - game.Position;
                EnemyFootStep(game, Math.Min(dt, delta.Length / game.Speed), delta.Normalized);
            }
            Check((waypoint - game.Position).Length < .005f, "Public movement reaches every recorded house-route waypoint without relocating the player");
        }
        private static void EnemyFootNaturalRoute(GameCore game, float dt)
        {
            foreach (Point waypoint in new[] { new Point(14, 14), new Point(14, 8), new Point(17, 8), new Point(20, 8), new Point(20, 5.1f), new Point(17, 5.1f) })
                EnemyFootWalk(game, waypoint, dt);
            Check(game.CanStand(game.Position) && !game.CanStand(new Point(17.5f, 5.5f)),
                "The real idle player foot is legal although the navigation center in the same cell is inside the shipped house");
            Check(game.invulnerable <= 0, "The original arrival grace has expired before the unprotected idle wait");
        }

        public static void EnemyFootPathNaturalSevenEnemyHouseRouteClosesCombatAtTwentyAndSixtyFrames(Campaign data)
        {
            bool everyRateClosed = true;
            foreach (float dt in new[] { .05f, 1 / 60f })
            {
                GameCore game = EnemyFootRegion(data); Enemy hunter = game.enemies.Find(e => e.id == 1 && e.kind == 0);
                Check(hunter != null, "The unchanged authored population contains the original hunter");
                int coins = game.save.coins, xp = game.save.xp, shards = game.save.shards; int[] kills = (int[])game.save.kills.Clone();
                EnemyFootNaturalRoute(game, dt); float health = game.save.health; bool prepared = false, released = false; float closest = 100;
                for (int frame = 0; frame < (int)Math.Ceiling(20 / dt); frame++)
                {
                    int state = hunter.state; EnemyFootStep(game, dt, new Point()); closest = Math.Min(closest, (hunter.Position - game.Position).Length);
                    if (state != 1 && hunter.state == 1)
                    {
                        prepared = true;
                        Check(hunter.windupDuration == .65f && (hunter.target - game.Position).Length < .00001f,
                            "The detouring authored hunter reaches its original visible .65-second melee preparation against the real idle foot");
                    }
                    released |= state == 1 && hunter.state == 0 && hunter.releaseTime > 0;
                    if (prepared && released && closest < 1.2f && game.save.health < health) break;
                }
                Console.WriteLine("ENEMY_FOOT_NATURAL dt=" + dt + " enemies=" + game.enemies.Count + " closest=" + closest + " prepared=" + prepared + " released=" + released + " health=" + health + "->" + game.save.health);
                everyRateClosed &= prepared && released && closest < 1.2f && game.save.health < health && !game.Dying && game.mode == "world";
                Check(game.enemies.Count == 7 && game.save.coins == coins && game.save.xp == xp && game.save.shards == shards,
                    "The natural chase retains all seven authored actors and creates no rewards");
                for (int i = 0; i < kills.Length; i++) Check(game.save.kills[i] == kills[i], "Walking and enemy contacts never grant player kills");
            }
            Check(everyRateClosed,
                "At both twenty and sixty frames per second the naturally acquired hunter must leave the blocked house edge, close, prepare and release while real enemy contacts reduce the unprotected idle player's health");
        }

        public static void EnemyFootPathAllOrdinaryKindsSweepTheExactFootAndDriveOnlyRealGait(Campaign data)
        {
            foreach (float dt in new[] { .05f, 1 / 60f }) foreach (int kind in new[] { 0, 1, 2 })
            {
                GameCore game = EnemyFootRegion(data); Enemy enemy = game.enemies.Find(e => e.kind == kind);
                game.enemies.RemoveAll(e => !ReferenceEquals(e, enemy));
                enemy.x = 17; enemy.y = 7.5f; enemy.timer = 9999; game.save.x = 17; game.save.y = 5.1f;
                Check(ReturnEnabled(enemy) && game.CanStand(enemy.Position) && game.CanStand(game.Position) && !EnemyFootVisible(game, enemy.Position, game.Position),
                    "An actual authored ordinary kind begins below the real house, within acquisition but with attack line of sight blocked");
                float farthestX = enemy.x, walked = enemy.moveTime; bool arrived = false;
                for (int frame = 0; frame < (int)Math.Ceiling(16 / dt); frame++)
                {
                    EnemyFootStep(game, dt, new Point()); farthestX = Math.Max(farthestX, enemy.x);
                    if (EnemyFootVisible(game, enemy.Position, game.Position) && (game.Position - enemy.Position).Length <= (kind == 1 ? 4.5f : 1.2f)) { arrived = true; break; }
                }
                Check(arrived && farthestX > 18.9f && enemy.moveTime > walked + 3 && enemy.state == 0 && enemy.hp == enemy.maxHp,
                    "Each ordinary kind physically walks the swept side approach and reaches its unchanged visible stopping range at " + dt + " seconds per frame");
                Check(game.save.health == 100 && game.projectiles.Count == 0 && enemy.timer < 9999 && enemy.timer > 9900,
                    "A postponed attack advances only the established timer; path following causes no contact, health change or projectile");
            }
        }

        public static void EnemyFootPathKeepsNormalCentersScopeAwarenessAndCommittedAttackOwnership(Campaign data)
        {
            foreach (int kind in new[] { 0, 1, 2 })
            {
                Enemy enemy; GameCore game = ReturnRegion(data, 1, kind, out enemy);
                enemy.x = 12; enemy.y = 14; game.save.x = 20; game.save.y = 14; Point before = enemy.Position;
                Point expected = EnemyFootSweep(game, before, game.DirectionTo(before, game.Position) * .02f * (kind == 0 ? 2.1f : 1.5f));
                EnemyFootStep(game, .02f, new Point());
                Check((enemy.Position - expected).Length < .00001f, "A legal-center target retains the exact existing DirectionTo movement for every ordinary kind");
            }
            // These mature checks use real training lifecycles, precise 8/10/18
            // thresholds, captured warnings, recoil, authored return posts,
            // actual reserved quest/contract/Abyss actors and blocked homes.
            EnemyPursuitUsesExactAcquisitionRetentionAndOriginalTrainingGates(data);
            EnemyPursuitPreservesCommittedWarningsReleasesStunAndRecoil(data);
            EnemyReturnRetainsAcquisitionCommittedAttacksAndReactionPriority(data);
            EnemyReturnPreservesSpecialEncountersTrainingAndBlockedHomes(data);
            foreach (string excluded in new[] { "unowned", "boss", "practice" })
            {
                GameCore game = EnemyFootRegion(data); Enemy enemy = game.enemies[0];
                if (excluded == "unowned") { enemy = new Enemy(1, 0, 17, 7.5f, 43); game.enemies.Clear(); game.enemies.Add(enemy); }
                else game.enemies.RemoveAll(e => !ReferenceEquals(e, enemy));
                enemy.x = 17; enemy.y = 7.5f; enemy.timer = 9999; game.save.x = 17; game.save.y = 5.1f;
                if (excluded == "boss") enemy.boss = true;
                if (excluded == "practice") game.practicing = true;
                Check(game.enemies.Contains(enemy), "The scope fixture retains the actor whose old heading is measured");
                Point before = enemy.Position; float speed = enemy.boss ? 1.15f + enemy.BossPhase * .15f : 2.1f;
                Point expected = EnemyFootSweep(game, before, game.DirectionTo(before, game.Position) * .02f * speed);
                game.Tick(.02f, new Point());
                Check((enemy.Position - expected).Length < .00001f, "The exact-foot change leaves the original heading intact for excluded " + excluded + " actors");
            }
        }

        public static void EnemyFootPathLegacySnapshotsAndReplacementTravelKeepFreshActorOwnership(Campaign data)
        {
            Check(typeof(EnemyRecord).GetFields().Length == 7, "Exact-foot routing retains the original seven-field EnemyRecord schema");
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("footPath", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("waypoint", StringComparison.OrdinalIgnoreCase) < 0,
                    "Exact-foot routing introduces no saved path or waypoint state");
            foreach (int version in new[] { 1, 2 })
            {
                GameCore original = EnemyFootRegion(data); EnemyFootNaturalRoute(original, .05f);
                Enemy hunter = original.enemies.Find(e => e.id == 1); EnemyFootStep(original, .05f, new Point()); hunter.hp = 31;
                original.PrepareSave(); SaveData saved = CloneSave(original.save); saved.version = version; Point position = hunter.Position; int coins = saved.coins;
                var loaded = new GameCore(data, saved); loaded.Start(); loaded.Close(); Enemy restored = loaded.enemies.Find(e => e.id == hunter.id);
                Check(restored != null && !ReferenceEquals(restored, hunter) && (restored.Position - position).Length == 0 && restored.hp == 31 && restored.maxHp == hunter.maxHp && ReturnEnabled(restored),
                    "Version " + version + " preserves the exact saved feet, damage and authored home without relocating the restored actor");
                Check(loaded.save.coins == coins && loaded.enemies.Count == 7 && !Returning(loaded, restored) && restored.state == 0 && restored.moveTime == 0,
                    "A legacy snapshot reconstructs fresh transient attack, return and gait state while preserving the entire population");
                loaded.invulnerable = 1000; restored.timer = 9999; bool closed = false;
                for (int frame = 0; frame < 400; frame++)
                { EnemyFootStep(loaded, .05f, new Point()); if ((restored.Position - loaded.Position).Length < 1.2f) { closed = true; break; } }
                Check(closed, "A restored authored hunter computes a fresh legal exact-foot approach after version " + version + " load");
            }
            EnemyPursuitKeepsAlertsTransientAcrossSavesActorsTravelAndDiagnostics(data);
            EnemyReturnKeepsOldSnapshotsDamageAndInstanceOwnership(data);
        }
    }
}
