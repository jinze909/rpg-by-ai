using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore SignalReadyGame(Campaign data, int profession = 0)
        {
            var g = Fresh(data, profession); g.SkipTutorial();
            foreach (string flag in new[] { "scout_saved", "core_1", "cache_1", "watch_accept", "watch_signal_0", "watch_signal_1", "watch_signal_2", "watch_complete" })
                g.save.flags.Add(flag);
            g.Enter(1); g.enemies.Clear();
            g.save.x = g.SignalPoint.x; g.save.y = g.SignalPoint.y - 1;
            return g;
        }
        private static Enemy SignalStart(GameCore g)
        {
            Check(g.StartSignalEncounter(), "A completed patrol opens the optional Warden trial at the final lantern");
            Enemy e = g.enemies.Find(GameCore.IsSignalWarden);
            Check(e != null && g.SignalActive && e.hp == GameCore.SignalWardenHealth,
                "The optional encounter starts its own full-health Boss rather than a chapter guardian");
            return e;
        }
        private static void SignalAttackUntilDefeated(GameCore g, Enemy e)
        {
            Combat(g, true);
            Check(!g.Dying && !g.enemies.Contains(e),
                "Actual attacks, techniques and dodges must defeat the optional Boss while its authored AI runs");
        }
        private static Enemy SignalWindup(GameCore g, int pattern)
        {
            Enemy e = g.enemies.Find(GameCore.IsSignalWarden);
            Check(e != null, "A Warden must be present before exercising its authored pattern");
            e.state = 0; e.cycle = pattern; e.timer = .02f; e.stun = 0; e.releaseTime = 0;
            g.save.x = e.x + 2.5f; g.save.y = e.y;
            g.invulnerable = 100;
            for (int frame = 0; frame < 100 && e.state != 1; frame++) g.Tick(.02f, new Point());
            Check(e.state == 1 && GameCore.SignalPattern(e) == pattern, "The requested authored pattern enters a real telegraph");
            return e;
        }
        private static void SignalRelease(GameCore g, Enemy e)
        {
            for (int frame = 0; frame < 200 && e.state == 1; frame++) g.Tick(.02f, new Point());
            Check(e.state == 0 && e.releaseTime > 0, "A prepared Warden move releases through the real simulation");
        }
        public static void SignalWardenReadinessAndOriginalStory(Campaign data)
        {
            var locked = Fresh(data); locked.SkipTutorial(); locked.Enter(1);
            locked.save.x = 23; locked.save.y = 8;
            Check(!locked.SignalReady && !locked.StartSignalEncounter() && !locked.SignalActive,
                "The new optional trial cannot bypass the original patrol story");
            var g = SignalReadyGame(data);
            Check(g.SignalReady && !g.SignalComplete && !g.SignalActive && !g.Has("signal_started"),
                "A completed patrol offers the trial without accepting or spawning it automatically");
            g.save.x = g.SignalPoint.x + 3; g.save.y = g.SignalPoint.y;
            Check(!g.StartSignalEncounter() && !g.Has("signal_started"), "The lantern cannot start the Boss remotely");
            g.save.x = g.SignalPoint.x; g.save.y = g.SignalPoint.y - 1;
            foreach (string mode in new[] { "dialog", "inventory", "pause", "journal" })
            { g.mode = mode; Check(!g.StartSignalEncounter(), "A paused world cannot begin a Boss attempt"); }
            g.Close(); Enemy e = SignalStart(g);
            Check(!g.StartSignalEncounter() && g.enemies.FindAll(GameCore.IsSignalWarden).Count == 1,
                "Repeated interaction cannot stack copies of the encounter");
            g.save.x = 28; g.save.y = 10; AwaitArenaOpening(g); g.Talk(Object(g, "shrine"));
            Check(g.mode == "world" && g.enemies.FindAll(x => x.boss).Count == 1 && g.SignalActive,
                "An active optional trial cannot be combined with a chapter guardian");
            Check(g.Has("watch_complete") && g.Has("scout_saved") && g.Has("core_1") && !g.Has("boss_1") && g.save.stage == 0,
                "Starting the trial preserves the patrol, original rescue, core and campaign stage");
            Check(!g.Blocked(e.x - .22f, e.y - .18f) && !g.Blocked(e.x + .22f, e.y + .18f),
                "The optional Boss starts with valid footing in the existing forest");
            Check(!GameCore.IsSignalWarden(new Enemy(GameCore.SignalWardenId, 3, 20, 9, 30)) &&
                !GameCore.IsSignalWarden(new Enemy(GameCore.SignalWardenId, 0, 20, 9, 30, true)) &&
                !GameCore.IsSignalWarden(new Enemy(GameCore.SignalWardenId - 1, 3, 20, 9, 30, true)),
                "The optional identity requires the reserved ID, Boss role and authored kind together");
        }
        public static void SignalWardenVictoryRewardsAndStoryIsolation(Campaign data)
        {
            var g = SignalReadyGame(data); g.save.bounty = 3; g.save.bountyProgress = 0;
            Enemy e = SignalStart(g);
            int coins = g.save.coins, xp = g.save.xp, shards = g.save.shards, points = g.save.skillPoints,
                stage = g.save.stage, kills = g.save.kills[1], materials = g.save.inventory[5], mastery = g.save.mastery[g.save.weapon];
            var ordinary = new Projectile { position = new Point(30, 16), life = 100, ownerId = 0 };
            var owned = new Projectile { position = new Point(30, 17), life = 100, ownerId = GameCore.SignalWardenId };
            g.projectiles.Add(ordinary); g.projectiles.Add(owned);
            SignalAttackUntilDefeated(g, e);
            Check(g.SignalComplete && g.Has("signal_complete") && !g.SignalActive && g.save.shards == shards + 6 && g.save.skillPoints == points + 1,
                "Actual victory awards six shards and one talent point exactly once");
            Check(g.save.coins == coins && g.save.xp == xp && g.save.kills[1] == kills && g.save.inventory[5] == materials &&
                g.save.bountyProgress == 0 && g.save.mastery[g.save.weapon] == mastery,
                "The trial cannot award normal enemy loot, chapter Boss loot, guardian contract credit or mastery farming");
            Check(g.projectiles.Contains(ordinary) && !g.projectiles.Contains(owned),
                "Optional victory retires its own hostile shots while preserving unrelated world projectiles");
            Check(g.save.stage == stage && g.Has("watch_complete") && g.Has("core_1") && !g.Has("boss_1") && !g.Has("core_2") && !g.Has("boss_4"),
                "Optional victory does not defeat or deliver any campaign guardian");
            Check(!g.StartSignalEncounter(), "A completed trial cannot be restarted as a reward farm");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.SignalComplete && !loaded.SignalActive && !loaded.StartSignalEncounter() &&
                loaded.save.shards == shards + 6 && loaded.save.skillPoints == points + 1,
                "A saved victory preserves its one-time reward and cannot respawn the optional Boss");
            loaded.save.x = 28; loaded.save.y = 10; AwaitArenaOpening(loaded); Action(loaded, "shrine", "boss");
            Enemy guardian = loaded.enemies.Find(x => x.boss);
            Check(guardian != null && !GameCore.IsSignalWarden(guardian) && guardian.id < GameCore.SignalWardenId,
                "The original forest guardian remains available with its original allocator after optional victory");
        }
        public static void SignalWardenSnapshotsTravelAndLegacyCompatibility(Campaign data)
        {
            var g = SignalReadyGame(data); Enemy e = SignalStart(g); e.hp = 143; e.state = 1; e.timer = .05f;
            // Mature progress must not filter the optional Boss as an already defeated room guardian.
            g.save.flags.Add("boss_1"); g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Enemy resumed = loaded.enemies.Find(GameCore.IsSignalWarden);
            Check(resumed != null && resumed.hp == 143 && resumed.maxHp == GameCore.SignalWardenHealth && resumed.state == 0 && resumed.timer <= 1.2f && resumed.timer > 0,
                "The same-room snapshot retains optional Boss health and restarts a safe bounded preparation clock");
            Check(loaded.Has("boss_1") && loaded.Has("core_1") && loaded.Has("watch_complete") && loaded.Has("signal_started"),
                "Optional resume retains all mature and accepted story flags");
            var allocatorSave = CloneSave(g.save); allocatorSave.flags.Remove("boss_1");
            var allocator = new GameCore(data, allocatorSave); allocator.Start();
            SignalAttackUntilDefeated(allocator, allocator.enemies.Find(GameCore.IsSignalWarden));
            allocator.save.x = 28; allocator.save.y = 10; AwaitArenaOpening(allocator); Action(allocator, "shrine", "boss");
            Enemy original = allocator.enemies.Find(x => x.boss);
            Check(original != null && !GameCore.IsSignalWarden(original) && original.id < GameCore.SignalWardenId && !allocator.IsWatchEnemyId(original.id),
                "A restored reserved Warden ID cannot drive the normal guardian allocator into optional ID ranges");
            loaded.Enter(0); loaded.Enter(1);
            Check(!loaded.SignalActive && loaded.Has("signal_started") && !loaded.SignalComplete,
                "Travel abandons the attempt without discarding its accepted story or granting victory");
            loaded.save.x = loaded.SignalPoint.x; loaded.save.y = loaded.SignalPoint.y - 1; Enemy retry = SignalStart(loaded);
            Check(retry.hp == GameCore.SignalWardenHealth, "A deliberate retry begins with full Boss health");
            foreach (int version in new[] { 1, 2 })
            {
                var saved = new SaveData { version = version, room = 1, stage = 1, x = 20, y = 9, coins = 91,
                    flags = new List<string> { "intro", "scout_saved", "core_1", "cache_1" }, snapshotRoom = 1,
                    worldEnemies = new[] { new EnemyRecord { id = 12, kind = 3, boss = true, x = 29, y = 10, hp = 91, maxHp = 300 } } };
                var old = new GameCore(data, saved); old.Start();
                Check(old.save.coins == 91 && old.Has("core_1") && old.Has("cache_1") && !old.SignalReady && !old.SignalActive && !old.Has("signal_started"),
                    "Legacy saves retain mature progress and never autoaccept the new trial");
                Check(old.enemies.Count == 1 && old.enemies[0].boss && !GameCore.IsSignalWarden(old.enemies[0]) && old.enemies[0].hp == 91,
                    "Legacy campaign Boss snapshots retain their identity and live health");
            }
        }
        public static void SignalWardenDeathAbandonsWithoutReward(Campaign data)
        {
            var g = SignalReadyGame(data); SignalStart(g);
            int shards = g.save.shards, points = g.save.skillPoints; g.invulnerable = 0; g.save.health = 1;
            g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.05f, new Point());
            Check(g.Dying && !g.SignalComplete && !g.StartSignalEncounter(), "Lethal contact cannot complete or start the optional Boss trial");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.save.room == 0 && !loaded.SignalActive && loaded.Has("signal_started") && !loaded.SignalComplete &&
                loaded.save.shards == shards && loaded.save.skillPoints == points,
                "Saving during the fallen pose recovers once without fabricating optional completion or payment");
            Advance(g, 1); Check(g.save.room == 0 && !g.SignalActive && !g.SignalComplete,
                "Normal death recovery abandons the attempt without changing its story");
            g.Enter(1); Check(!g.SignalActive, "Returning after death requires a deliberate new attempt rather than an ambush");
            g.save.x = g.SignalPoint.x; g.save.y = g.SignalPoint.y - 1; SignalStart(g);
        }
        public static void SignalWardenThreePatternsKeepLockedTelegraphs(Campaign data)
        {
            var g = SignalReadyGame(data); SignalStart(g);
            for (int pattern = 0; pattern < 3; pattern++)
            {
                Enemy e = SignalWindup(g, pattern); Point origin = e.attackOrigin, target = e.target, facing = e.facing;
                float duration = e.windupDuration;
                Check(Math.Abs(duration - (pattern == 0 ? .95f : pattern == 1 ? 1.15f : 1.1f)) < .001f,
                    "Every Warden pattern has its authored readable preparation duration");
                e.hp = 80; g.save.x -= 1; g.save.y += 1; g.Tick(.02f, new Point());
                Check(GameCore.SignalPattern(e) == pattern && (e.attackOrigin - origin).Length < .001f &&
                    (e.target - target).Length < .001f && (e.facing - facing).Length < .001f && e.windupDuration == duration,
                    "Health phase changes and player movement cannot retarget or reshape an already visible warning");
                if (pattern == 1)
                {
                    Point[] marks = GameCore.SignalMarks(e);
                    Check(marks.Length == 2 && Math.Abs((marks[0] - target).Length - 2) < .001f &&
                        Math.Abs((marks[1] - target).Length - 2) < .001f && ((marks[0] + marks[1]) * .5f - target).Length < .001f &&
                        GameCore.SignalMarkRadius < 2,
                        "The paired marks leave a real safe gap at the locked target instead of hiding a centre hit");
                }
                if (pattern == 2)
                {
                    Point[] fan = GameCore.SignalFanDirections(e);
                    Check(fan.Length == 4 && GameCore.SignalFanStart == 1.7f && GameCore.SignalFanLength == 4.5f,
                        "The split fan has four finite authored lanes with no hidden central projectile");
                    foreach (Point direction in fan)
                    {
                        float angle = (float)Math.Acos(Math.Min(1, Math.Max(-1, direction.x * facing.x + direction.y * facing.y)));
                        Check(Math.Abs(direction.Length - 1) < .001f && (Math.Abs(angle - .42f) < .001f || Math.Abs(angle - .78f) < .001f),
                            "Every split-fan lane matches one of the four displayed offsets");
                    }
                }
                for (int frame = 0; frame < 150 && e.state == 1; frame++) g.Tick(.02f, new Point());
                Check(e.state == 0 && e.cycle == pattern + 1 && e.releaseTime > 0,
                    "One prepared pattern releases once and advances to the next counter window");
                Check(g.enemies.Count == 1, "The optional Boss does not inherit chapter reinforcements");
                if (pattern != 2) Check(g.projectiles.Count == 0, "Rush and paired marks do not inherit generic Boss spokes");
                else Check(g.projectiles.Count <= 4, "The split fan produces only its four authored lanes");
                g.projectiles.Clear(); g.hazards.Clear();
            }
        }
        public static void SignalWardenWarningsMatchActualContacts(Campaign data)
        {
            // Paired marks punish their displayed circles while the centre remains safe.
            foreach (bool inMark in new[] { false, true })
            {
                var g = SignalReadyGame(data); SignalStart(g); Enemy e = SignalWindup(g, 1);
                Point stand = inMark ? GameCore.SignalMarks(e)[0] : e.target;
                g.save.x = stand.x; g.save.y = stand.y; g.invulnerable = 0; float health = g.save.health;
                SignalRelease(g, e);
                Check(inMark ? g.save.health < health : g.save.health == health,
                    "Paired mark damage must match the warning circles and preserve their central counter lane");
            }
            // The locked rush is a narrow swept capsule, rather than a hidden radial slam.
            foreach (bool inLane in new[] { false, true })
            {
                var g = SignalReadyGame(data); SignalStart(g); Enemy e = SignalWindup(g, 0);
                Point side = new Point(-e.facing.y, e.facing.x);
                Point stand = e.attackOrigin + e.facing * 2 + side * (inLane ? .2f : GameCore.SignalRushRadius + .15f);
                g.save.x = stand.x; g.save.y = stand.y; g.invulnerable = 0; float health = g.save.health;
                SignalRelease(g, e);
                Check(inLane ? g.save.health < health : g.save.health == health,
                    "The actual rush hits only the displayed narrow lane and leaves a lateral dodge safe");
                Check((e.Position - e.attackDestination).Length < .05f && !g.Blocked(e.x - .22f, e.y - .18f) && !g.Blocked(e.x + .22f, e.y + .18f),
                    "The rush reaches its authored endpoint with the real actor footprint");
            }
            // Actual flying contacts leave the split fan's centre gap intact.
            foreach (bool inLane in new[] { false, true })
            {
                var g = SignalReadyGame(data); SignalStart(g); Enemy e = SignalWindup(g, 2);
                Point direction = inLane ? GameCore.SignalFanDirections(e)[1] : e.facing;
                Point stand = e.attackOrigin + direction * 3;
                g.save.x = stand.x; g.save.y = stand.y; g.invulnerable = 0; float health = g.save.health;
                SignalRelease(g, e);
                Check(g.projectiles.Count == 4, "The released fan contains exactly its four authored projectiles");
                e.timer = 9999; Advance(g, 1.6f);
                Check(inLane ? g.save.health < health : g.save.health == health,
                    "Flying fan contacts match the warning lanes while preserving the advertised centre gap");
                Check(g.projectiles.Count == 0, "The authored fan finishes its finite travel rather than lingering indefinitely");
            }
        }
        public static void SignalWardenHitReactionsPreserveAnnouncedRush(Campaign data)
        {
            var g = SignalReadyGame(data); g.save.reducedMotion = true; SignalStart(g);
            Enemy e = SignalWindup(g, 0); Point origin = e.attackOrigin, destination = e.attackDestination;
            float bossHealth = e.hp;
            g.save.x = e.x; g.save.y = e.y - 1;
            g.Attack(new Point(0, 1));
            for (int frame = 0; frame < 100 && e.hp == bossHealth; frame++) g.Tick(.02f, new Point());
            Check(e.hp < bossHealth && e.state == 1 && e.hitTime > 0 && e.flash > 0,
                "A real nonbreaking weapon contact keeps damage, flash and the prepared Warden move");
            float pausedClock = e.timer;
            for (int frame = 0; frame < 5; frame++) g.Tick(.02f, new Point());
            Check((e.Position - origin).Length < .00001f && e.timer == pausedClock &&
                (e.attackOrigin - origin).Length < .00001f && (e.attackDestination - destination).Length < .00001f,
                "The hit reaction pauses its timer without displacing the body or the captured rush lane");
            // This position was hit by the first diagonal sweep after a .027-grid recoil,
            // even though it lies just beyond the announced semicircular start cap.
            Point stand = origin + new Point(0, GameCore.SignalRushRadius + .005f);
            g.save.x = stand.x; g.save.y = stand.y; g.invulnerable = 0;
            float playerHealth = g.save.health;
            SignalRelease(g, e);
            Check(g.save.health == playerHealth && (e.Position - destination).Length < .05f,
                "After an actual hit, the released rush still spares a player outside its displayed capsule");

            // Breaking an announced move cancels it; the normal physical reaction then applies.
            e = SignalWindup(g, 0);
            g.save.x = e.x; g.save.y = e.y - 1; e.stagger = 94; e.staggerRecovery = 0;
            bossHealth = e.hp; g.Attack(new Point(0, 1));
            for (int frame = 0; frame < 100 && e.hp == bossHealth; frame++) g.Tick(.02f, new Point());
            Check(e.hp < bossHealth && e.state == 0 && e.stun > 0 && e.hitTime > 0,
                "A real stagger break still cancels the prepared Warden move and applies stun");
            Point before = e.Position; g.Tick(.02f, new Point());
            Check(e.y > before.y && (e.Position - before).Length > .001f,
                "A cancelled Warden move retains the existing physical recoil in its resting state");
        }
        public static void SignalWardenFanLaunchRespectsInterveningRoots(Campaign data)
        {
            var g = SignalReadyGame(data); g.save.reducedMotion = true; Enemy e = SignalStart(g);
            // A valid position below the two-cell root wall lets the central aim see
            // the player, while an upper fan muzzle would otherwise jump across it.
            e.x = 10.25f; e.y = 10.3f; e.cycle = 2; e.timer = .02f;
            g.save.x = 8; g.save.y = 10.3f; g.invulnerable = 100;
            g.Tick(.02f, new Point());
            Check(g.CanStand(e.Position) && e.state == 1 && GameCore.SignalPattern(e) == 2,
                "The wall fixture enters a real fan warning from valid forest footing");
            Point direction = GameCore.SignalFanDirections(e)[2];
            Point muzzle = e.attackOrigin + direction * GameCore.SignalFanStart;
            Point stand = muzzle + direction * .5f;
            Point middle = (e.attackOrigin + muzzle) * .5f;
            Check(g.CanStand(stand) && !g.Blocked(muzzle.x, muzzle.y) && g.Blocked(middle.x, middle.y),
                "An open muzzle and player can be separated from the Warden by an existing root wall");
            g.save.x = stand.x; g.save.y = stand.y; g.invulnerable = 0;
            float health = g.save.health;
            SignalRelease(g, e); e.timer = 9999;
            Check(g.projectiles.Count < 4,
                "Fan lanes blocked during their offset launch stop at the wall instead of appearing beyond it");
            Advance(g, 1.6f);
            Check(g.save.health == health && g.projectiles.Count == 0,
                "The actual fan cannot damage a player across the muzzle wall and all remaining lanes finish normally");
        }
        public static void SignalWardenAllClassesCanWinWithPublicActions(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = SignalReadyGame(data, profession); SignalStart(g);
                Combat(g, true);
                Check(!g.Dying && g.SignalComplete && !g.SignalActive && g.Has("watch_complete") && !g.Has("boss_1"),
                    "Every starter class wins the isolated optional trial through real attacks, techniques and dodges while preserving chapter progress");
            }
        }
    }
}
