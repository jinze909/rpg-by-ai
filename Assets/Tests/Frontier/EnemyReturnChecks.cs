using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static bool Returning(GameCore game, Enemy enemy)
        {
            MethodInfo query = typeof(GameCore).GetMethod("EnemyReturningToPost", BindingFlags.Instance | BindingFlags.Public);
            return query != null && (bool)query.Invoke(game, new object[] { enemy });
        }
        private static bool ReturnEnabled(Enemy enemy)
        {
            FieldInfo field = typeof(Enemy).GetField("returnEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && (bool)field.GetValue(enemy);
        }
        private static GameCore ReturnRegion(Campaign data, int room, int kind, out Enemy enemy)
        {
            var game = Unlocked(data); game.SkipTutorial(); game.Enter(room);
            Check(game.save.room == room, "The return scenario enters the actual authored region " + room);
            enemy = game.enemies.Find(e => e.kind == kind && !e.boss);
            Check(enemy != null, "The original regional population includes the requested ordinary kind");
            Enemy selected = enemy; game.enemies.RemoveAll(e => !ReferenceEquals(e, selected));
            game.projectiles.Clear(); game.effects.Clear(); game.hazards.Clear();
            game.invulnerable = 1000; game.save.reducedMotion = true; game.save.companion = -1;
            enemy.timer = 9999;
            return game;
        }
        private static void ReturnPlayer(GameCore game, Point foot)
        { Check(game.CanStand(foot), "Player arrangements retain the shipped complete footprint"); game.save.x = foot.x; game.save.y = foot.y; }
        private static void ReturnArrange(GameCore game, Enemy enemy, Point foot, Point player)
        {
            Check(game.CanStand(foot), "The original spawned actor is arranged on legal production geometry");
            enemy.x = foot.x; enemy.y = foot.y; enemy.state = 0; enemy.hitTime = enemy.stun = enemy.releaseTime = 0;
            enemy.timer = 9999; ReturnPlayer(game, player); game.Tick(.02f, new Point());
            Check(!Returning(game, enemy), "An actual acquired ordinary actor is pursuing rather than returning");
            ReturnPlayer(game, new Point(30, 14.5f)); game.Tick(.02f, new Point());
            Check(Returning(game, enemy), "The naturally spawned actor starts returning only after actual awareness is lost beyond ten");
        }
        private static void ReturnWalk(GameCore game, Enemy enemy, Point post, float dt, float seconds)
        {
            float speed = enemy.kind == 0 ? 2.1f : 1.5f;
            for (int frame = 0; frame < (int)Math.Ceiling(seconds / dt) && (enemy.Position - post).Length > .08f; frame++)
            {
                Point before = enemy.Position; float timer = enemy.timer, health = enemy.hp;
                Point intent = game.DirectionTo(before, post) * Math.Min((post - before).Length, speed * dt);
                Point swept = (Point)typeof(GameCore).GetMethod("SweepActor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(game, new object[] { before, intent });
                game.Tick(dt, new Point()); Point step = enemy.Position - before;
                Check(game.CanStand(enemy.Position) && step.Length <= speed * dt + .00002f,
                    "Every actual return frame uses legal full-foot movement at the existing ordinary speed");
                Check((swept - enemy.Position).Length < .00002f, "The real bounded intention follows the complete-foot sweep, including its original corner-sliding substeps");
                Check(enemy.timer == timer && enemy.hp == health && enemy.state == 0 && !enemy.backpedaling,
                    "Returning never advances an attack timer, heals, commits a strike or advertises archer retreat");
                if (step.Length > .001f)
                    Check(enemy.moving && enemy.facing.x * step.x + enemy.facing.y * step.y > 0,
                        "Return movement drives the existing gait and faces its actual travel direction");
            }
            Check((enemy.Position - post).Length <= .08f, "The escaped regional enemy walks back to its actual authored post");
            game.Tick(dt, new Point()); Check(!Returning(game, enemy) && !enemy.moving, "Arrival retires return movement without an idle shuffle");
        }

        public static void EnemyReturnWalksAuthoredWaterDetoursAtTwentyAndSixtyFrames(Campaign data)
        {
            foreach (float dt in new[] { .05f, 1 / 60f })
            {
                Enemy enemy; GameCore game = ReturnRegion(data, 2, 0, out enemy); Point post = enemy.Position;
                Check(post.x == 11 && post.y == 12, "The actual Drowned Ruins population begins at its already legal authored post");
                ReturnPlayer(game, new Point(8.5f, 4.5f));
                Check((game.Position - post).Length <= 8, "The real outgoing chase starts inside the unchanged discovery radius");
                for (int frame = 0; frame < (int)Math.Ceiling(15 / dt) && (game.Position - enemy.Position).Length > 1.2f; frame++)
                    game.Tick(dt, new Point());
                Check((game.Position - enemy.Position).Length <= 1.2f && (enemy.Position - post).Length > 5,
                    "The naturally placed hunter actually follows the player around authored water before escape");
                Point direct = (post - enemy.Position).Normalized;
                Check((game.DirectionTo(enemy.Position, post) - direct).Length > .1f,
                    "Returning to this original post requires the real water detour rather than an unobstructed straight line");
                enemy.hp = 17; int coins = game.save.coins, xp = game.save.xp, shards = game.save.shards;
                int[] kills = (int[])game.save.kills.Clone(), inventory = (int[])game.save.inventory.Clone();
                ReturnPlayer(game, new Point(30, 14.5f)); game.Tick(dt, new Point());
                Check((game.Position - enemy.Position).Length > 10, "Actual player escape leaves the same damaged hunter beyond the unchanged release radius");
                ReturnWalk(game, enemy, post, dt, 20);
                Check(ReturnEnabled(enemy), "The actual original regional spawn owns the post used by its complete physical return");
                Check(enemy.hp == 17 && game.save.coins == coins && game.save.xp == xp && game.save.shards == shards && game.projectiles.Count == 0,
                    "Natural return neither heals a hurt enemy nor creates projectiles or money, experience or shard rewards");
                for (int i = 0; i < kills.Length; i++) Check(kills[i] == game.save.kills[i], "Returning cannot count a kill");
                for (int i = 0; i < inventory.Length; i++) Check(inventory[i] == game.save.inventory[i], "Returning cannot grant loot");
            }
        }

        public static void EnemyReturnRetainsAcquisitionCommittedAttacksAndReactionPriority(Campaign data)
        {
            foreach (int kind in new[] { 0, 1, 2 })
            {
                Enemy enemy; GameCore game = ReturnRegion(data, 1, kind, out enemy); Point post = enemy.Position;
                ReturnArrange(game, enemy, new Point(15, 14), new Point(17, 14));
                Point returning = enemy.Position; float timer = enemy.timer;
                ReturnPlayer(game, returning + new Point(9, 0)); game.Tick(.02f, new Point());
                Check(Returning(game, enemy) && enemy.timer == timer && (enemy.Position - returning).Length > .001f && (enemy.Position - post).Length < (returning - post).Length,
                    "A return already in progress continues at nine instead of silently reacquiring");
                enemy.x = 15; enemy.y = 14; ReturnPlayer(game, new Point(23, 14)); game.Tick(.02f, new Point());
                Check(!Returning(game, enemy) && enemy.timer < timer,
                    "Exactly eight reacquires through the original pursuit rule and retires return state");
                enemy.x = 15; enemy.y = 14; enemy.timer = 0; ReturnPlayer(game, new Point(16.5f, 14)); game.Tick(.02f, new Point());
                Check(enemy.state == 1, "The same natural actor begins its original committed warning");
                Point captured = enemy.target, facing = enemy.facing, prepared = enemy.Position;
                ReturnPlayer(game, new Point(30, 14.5f)); game.Tick(.02f, new Point());
                Check(enemy.state == 1 && !Returning(game, enemy) && (enemy.Position - prepared).Length == 0 &&
                    (enemy.target - captured).Length == 0 && (enemy.facing - facing).Length == 0,
                    "Escaping cannot cancel, move or retarget the captured warning");
                while (enemy.state == 1) game.Tick(.02f, new Point());
                Check(enemy.releaseTime > 0 && !Returning(game, enemy) && (enemy.Position - prepared).Length == 0 &&
                    game.projectiles.Count == (kind == 1 ? 1 : 0), "The original release completes before return, including the archer's one captured shot");
                while (enemy.releaseTime > .02f) game.Tick(.02f, new Point());
                game.Tick(.02f, new Point()); Check(Returning(game, enemy), "Return begins after the ordinary release lock clears");
            }
            foreach (string reaction in new[] { "stun", "release", "recoil", "pause" })
            {
                Enemy enemy; GameCore game = ReturnRegion(data, 1, 0, out enemy); Point post = enemy.Position;
                ReturnArrange(game, enemy, new Point(15, 14), new Point(17, 14));
                Point before = enemy.Position; float timer = enemy.timer;
                if (reaction == "stun") enemy.stun = 1;
                else if (reaction == "release") enemy.releaseTime = 1;
                else if (reaction == "recoil") { enemy.hitTime = 1; enemy.recoil = new Point(-1, 0); }
                else game.mode = "inventory";
                game.Tick(.02f, new Point()); Point expected = reaction == "recoil" ? before + new Point(-.016f, 0) : before;
                Check((enemy.Position - expected).Length < .00002f && enemy.timer == timer,
                    "An ongoing return preserves the original higher-priority " + reaction + " reaction and attack clock");
                if (reaction == "pause") Check(!Returning(game, enemy), "Paused menus never expose a live return cue");
                enemy.stun = enemy.releaseTime = enemy.hitTime = 0; game.Close();
                ReturnWalk(game, enemy, post, .02f, 8);
            }
        }

        public static void EnemyReturnKeepsOldSnapshotsDamageAndInstanceOwnership(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                Enemy enemy; GameCore original = ReturnRegion(data, 1, 0, out enemy); Point post = enemy.Position;
                ReturnArrange(original, enemy, new Point(15, 14), new Point(17, 14)); enemy.hp = 17;
                original.PrepareSave(); SaveData saved = CloneSave(original.save); saved.version = version;
                Point savedFoot = enemy.Position; int coins = saved.coins;
                var game = new GameCore(data, saved); game.Start(); game.Close();
                Enemy restored = game.enemies.Find(e => e.id == enemy.id);
                Check(restored != null && ReturnEnabled(restored) && !Returning(game, restored) && (restored.Position - savedFoot).Length == 0 && restored.hp == 17,
                    "Version " + version + " preserves snapshot position and damage while restoring only the natural post, never live awareness");
                game.invulnerable = 1000; restored.timer = 9999; Point before = restored.Position; float timer = restored.timer; game.Tick(.02f, new Point());
                Check((restored.Position - before).Length == 0 && restored.timer == timer, "A loaded unaware actor remains idle outside discovery");
                ReturnPlayer(game, new Point(17, 14)); game.Tick(.02f, new Point()); ReturnPlayer(game, new Point(30, 14.5f)); game.Tick(.02f, new Point());
                Check(Returning(game, restored), "A legitimately reacquired saved actor can later lose the player again");
                ReturnWalk(game, restored, post, .02f, 8);
                Check(restored.hp == 17 && game.save.coins == coins, "Reloaded return reaches the original authored post rather than the saved chase location without healing or payment");
                game.Enter(0); Check(!Returning(game, restored), "Travel retires the old room's actor identity");
                game.Enter(1); Enemy fresh = game.enemies.Find(e => e.id == restored.id);
                Check(fresh != null && !ReferenceEquals(fresh, restored) && !Returning(game, fresh), "A newly spawned same-ID actor cannot inherit its predecessor's return");
            }
            foreach (Type type in new[] { typeof(SaveData), typeof(EnemyRecord) })
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                    Check(field.Name.IndexOf("returnPost", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("returnEnabled", StringComparison.OrdinalIgnoreCase) < 0 &&
                        field.Name.IndexOf("returning", StringComparison.OrdinalIgnoreCase) < 0, "The mature save schema does not acquire transient return fields");
            Enemy active; GameCore dying = ReturnRegion(data, 1, 0, out active);
            ReturnArrange(dying, active, new Point(15, 14), new Point(17, 14)); Point deathFoot = active.Position;
            dying.deathTime = 1; dying.Tick(.02f, new Point());
            Check(!Returning(dying, active) && (active.Position - deathFoot).Length == 0, "Player death freezes returning enemies and hides the live cue");
            dying.deathTime = 0; var replacement = new Enemy(active.id, active.kind, active.x, active.y, active.maxHp);
            dying.enemies.Clear(); dying.enemies.Add(replacement); dying.Tick(.02f, new Point());
            Check(!Returning(dying, active) && !Returning(dying, replacement) && !ReturnEnabled(replacement), "Replacing an actor with an identical ID cannot forge a saved natural post");
        }

        public static void EnemyReturnPreservesSpecialEncountersTrainingAndBlockedHomes(Campaign data)
        {
            for (int room = 1; room <= 7; room++)
            {
                Enemy chosen; GameCore game = ReturnRegion(data, room, 0, out chosen);
                Check(game.save.room == room && ReturnEnabled(chosen), "The actual original regional spawn loop opts in for region " + room);
            }
            var abyss = Unlocked(data); abyss.StartAbyss(0);
            Check(abyss.InAbyss && abyss.enemies.Count > 0, "Use actual procedural Abyss entry for exclusion");
            foreach (Enemy enemy in abyss.enemies) Check(!ReturnEnabled(enemy) && !Returning(abyss, enemy), "Procedural Abyss actors retain their mature encounter behavior");
            var reach = ReachGame(data); Check(reach.enemies.Count > 0, "Use actual Reach guardian population");
            foreach (Enemy enemy in reach.enemies) Check(!ReturnEnabled(enemy), "Reserved Reach and homeward guards never inherit generic posts");
            var watch = WatchAcceptedGame(data); WatchStart(watch, 0); Check(watch.enemies.Count > 0, "Use a real active watch wave");
            foreach (Enemy enemy in watch.enemies) Check(!ReturnEnabled(enemy), "Defence wave actors keep their local duty rather than retreating to a generic post");
            var archive = ArchiveGame(data); ArchiveVent(archive); ArchiveReadAll(archive);
            Check(archive.enemies.Count == 3, "Use the actual final Archive record defence");
            foreach (Enemy enemy in archive.enemies) Check(!ReturnEnabled(enemy), "Archive record guardians retain their authored defence instead of adopting a regional post");
            var signal = SignalReadyGame(data); Enemy warden = SignalStart(signal);
            Check(!ReturnEnabled(warden) && !Returning(signal, warden), "The dedicated Signal Warden stays under its authored AI");
            var commission = CommissionGame(data); Enemy machine = CommissionStart(commission);
            Check(!ReturnEnabled(machine) && !Returning(commission, machine), "The real commissioning guardian keeps its dedicated station");
            var chapter = Unlocked(data); chapter.save.flags.Add("scout_saved"); chapter.save.flags.Add("gate_1"); chapter.Enter(1);
            chapter.enemies.Clear(); chapter.save.x = 28; chapter.save.y = 10; chapter.invulnerable = 1000;
            AwaitArenaOpening(chapter); Action(chapter, "shrine", "boss"); Enemy boss = chapter.enemies.Find(e => e.boss);
            Check(boss != null && !ReturnEnabled(boss), "The original chapter shrine spawns its Boss without an ordinary post");
            boss.hp = boss.maxHp * .3f; boss.cycle = 2; boss.timer = 0;
            for (int frame = 0; frame < 100 && chapter.enemies.Count == 1; frame++) chapter.Tick(.02f, new Point());
            Check(chapter.enemies.Count > 1, "The real phase-three committed release adds its original reinforcements");
            foreach (Enemy enemy in chapter.enemies) Check(!ReturnEnabled(enemy), "Chapter guardians and their actual reinforcements retain the original encounter AI");
            foreach (string session in new[] { "basic", "sparring", "range" })
            {
                GameCore game = session == "basic" ? Fresh(data) : RangeGame(data);
                if (session == "basic") game.StartPractice();
                else Check(session == "sparring" ? game.StartSparring() : game.StartRange(0), "Enter the actual " + session + " training");
                Check(game.practicing && game.enemies.Count > 0, "The original training has its real actor population");
                foreach (Enemy enemy in game.enemies) Check(!ReturnEnabled(enemy) && !Returning(game, enemy), "Original training actor activation and stationary targets stay unchanged");
            }
            Enemy stranded; GameCore blocked = ReturnRegion(data, 1, 0, out stranded); Point home = stranded.Position;
            ReturnArrange(blocked, stranded, new Point(15, 14), new Point(17, 14)); Point foot = stranded.Position;
            int row = (int)Math.Floor(home.y), col = (int)Math.Floor(home.x); string prior = blocked.Map.rows[row];
            try
            {
                char[] cells = prior.ToCharArray(); cells[col] = '#'; blocked.Map.rows[row] = new string(cells);
                Check(!blocked.CanStand(home), "The isolated obstruction makes only the original home illegal");
                for (int frame = 0; frame < 75; frame++) blocked.Tick(.02f, new Point());
                Check((stranded.Position - foot).Length == 0 && blocked.CanStand(stranded.Position), "An illegal return post never relocates or teleports the actor through an obstruction");
            }
            finally { blocked.Map.rows[row] = prior; }

            Enemy trapped; GameCore closed = ReturnRegion(data, 1, 0, out trapped);
            ReturnArrange(closed, trapped, new Point(15.5f, 14.5f), new Point(17.5f, 14.5f));
            string[] terrain = (string[])closed.Map.rows.Clone(); int cx = (int)trapped.x, cy = (int)trapped.y;
            float cooldown = trapped.timer, hp = trapped.hp;
            try
            {
                // Close the route around legal feet while leaving the original
                // destination legal: this is a blocked path, not an invalid home.
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    char[] cells = closed.Map.rows[y].ToCharArray();
                    for (int x = cx - 1; x <= cx + 1; x++) if (x != cx || y != cy) cells[x] = '#';
                    closed.Map.rows[y] = new string(cells);
                }
                Check(closed.CanStand(trapped.Position) && closed.CanStand((Point)typeof(Enemy).GetField("returnPost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trapped)),
                    "Both endpoints remain legal while the connecting route is closed");
                for (int frame = 0; frame < 120; frame++)
                {
                    Point before = trapped.Position; closed.Tick(.02f, new Point());
                    Check(closed.CanStand(trapped.Position) && (trapped.Position - before).Length <= .04202f && trapped.hp == hp && trapped.timer == cooldown,
                        "A trapped return respects every sweep and keeps its damage and attack clock");
                }
                Check(!Returning(closed, trapped) && !trapped.moving, "An unreachable return eventually waits safely instead of grinding or teleporting through the wall");
            }
            finally { closed.Map.rows = terrain; }
            ReturnArrange(closed, trapped, trapped.Position, trapped.Position + new Point(2, 0));
            Check(Returning(closed, trapped), "A fresh real acquisition can retry the restored route after a blocked return");
        }
    }
}
