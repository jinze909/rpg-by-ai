using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static Enemy PursuitActor(GameCore game, int kind, Point foot, Point player, int id = 98701)
        {
            game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear();
            game.save.x = player.x; game.save.y = player.y; game.hitStop = 0; game.invulnerable = 1000;
            var enemy = new Enemy(id, kind, foot.x, foot.y, 43) { timer = 99 };
            game.enemies.Add(enemy);
            Check(game.CanStand(foot) && game.CanStand(player), "Pursuit fixtures use legal full-foot positions on the shipped map");
            return enemy;
        }
        private static void PursuitPlace(GameCore game, Enemy enemy, Point foot, float distance)
        {
            enemy.x = foot.x; enemy.y = foot.y; game.save.x = foot.x + distance; game.save.y = foot.y;
            Check(game.CanStand(foot) && game.CanStand(game.Position), "Each awareness boundary uses legal map positions");
        }
        private static void PursuitFrozenTick(GameCore game, Enemy enemy, string reason)
        {
            Point foot = enemy.Position; float timer = enemy.timer, moved = enemy.moveTime; game.Tick(.02f, new Point());
            Check((enemy.Position - foot).Length == 0 && enemy.timer == timer && enemy.moveTime == moved && !enemy.moving && !enemy.backpedaling,
                reason);
        }
        private static void PursuitMovingTick(GameCore game, Enemy enemy, string reason)
        {
            Point foot = enemy.Position; float timer = enemy.timer; game.Tick(.02f, new Point());
            float speed = enemy.boss ? 1.15f + enemy.BossPhase * .15f : enemy.kind == 0 ? 2.1f : 1.5f;
            Check(Math.Abs((enemy.Position - foot).Length - .02f * speed) < .00001f && Math.Abs(enemy.timer - (timer - .02f)) < .00001f && enemy.moving,
                reason);
        }

        public static void EnemyPursuitCrossesShippedWaterDetourAtTwentyAndSixtyFrames(Campaign data)
        {
            foreach (float dt in new[] { .05f, 1 / 60f })
            {
                var game = Quiet(data); game.save.room = 2;
                Point start = new Point(3.5f, 10.5f), player = new Point(8.5f, 4.5f);
                Enemy enemy = PursuitActor(game, 0, start, player); enemy.timer = 9999;
                int coins = game.save.coins, xp = game.save.xp, shards = game.save.shards; int[] kills = (int[])game.save.kills.Clone();
                Check((start - player).Length < 8 && (game.DirectionTo(start, player) - (player - start).Normalized).Length > .1f,
                    "The recorded water fixture acquires within eight and requires the shipped detour");
                float maximum = 0, distanceWalked = 0; bool crossed = false, closed = false;
                for (int frame = 0; frame < (int)Math.Ceiling(12 / dt); frame++)
                {
                    Point before = enemy.Position; game.Tick(dt, new Point()); Point displacement = enemy.Position - before;
                    float distance = (enemy.Position - player).Length; maximum = Math.Max(maximum, distance); distanceWalked += displacement.Length;
                    crossed |= distance > 8;
                    Check(game.CanStand(enemy.Position) && displacement.Length <= dt * 2.1f + .00001f,
                        "Every actual detour tick preserves full-foot clearance and the established hunter speed");
                    Point swept = (Point)typeof(GameCore).GetMethod("SweepActor", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(game, new object[] { before, displacement });
                    Check((swept - enemy.Position).Length < .00001f, "Each actual movement is independently legal through the production sweep");
                    if (distance <= 1.2f) { closed = true; break; }
                }
                Check(crossed && maximum > 8 && maximum < 8.3f && distanceWalked > .315f && closed,
                    "An acquired hunter must keep the legal water detour past eight units and close to melee range at " + dt + " seconds per frame");
                Check(enemy.state == 0 && enemy.hp == 43 && game.save.coins == coins && game.save.xp == xp && game.save.shards == shards,
                    "Pursuit alone creates no attacks, damage, money, experience or shards");
                for (int i = 0; i < kills.Length; i++) Check(game.save.kills[i] == kills[i], "Pursuit grants no kills or progression");
            }
        }

        public static void EnemyPursuitUsesExactAcquisitionRetentionAndOriginalTrainingGates(Campaign data)
        {
            Point foot = new Point(12, 14);
            foreach (int kind in new[] { 0, 1, 2 })
            {
                var game = Quiet(data); Enemy enemy = PursuitActor(game, kind, foot, foot + new Point(8.001f, 0));
                PursuitFrozenTick(game, enemy, "A fresh ordinary actor beyond eight cannot acquire for kind " + kind);
                PursuitPlace(game, enemy, foot, 8); PursuitMovingTick(game, enemy, "Exactly eight acquires with the existing movement and cooldown for kind " + kind);
                PursuitPlace(game, enemy, foot, 10); PursuitMovingTick(game, enemy, "Exactly ten retains an acquired actor for kind " + kind);
                PursuitPlace(game, enemy, foot, 10.001f); PursuitFrozenTick(game, enemy, "Beyond ten releases ordinary pursuit for kind " + kind);
                PursuitPlace(game, enemy, foot, 9); PursuitFrozenTick(game, enemy, "Returning only to nine cannot reacquire a released actor for kind " + kind);
                PursuitPlace(game, enemy, foot, 8); PursuitMovingTick(game, enemy, "Returning to eight reacquires for kind " + kind);
                PursuitPlace(game, enemy, foot, 9); PursuitMovingTick(game, enemy, "A reacquired actor again retains pursuit at nine for kind " + kind);
            }
            var bossGame = Quiet(data); foot = new Point(3, 14);
            Enemy boss = PursuitActor(bossGame, 2, foot, foot + new Point(18.001f, 0)); boss.boss = true;
            PursuitFrozenTick(bossGame, boss, "A chapter Boss retains its original outer eighteen-unit gate");
            PursuitPlace(bossGame, boss, foot, 18); PursuitMovingTick(bossGame, boss, "Exactly eighteen activates a chapter Boss with its original speed");
            PursuitPlace(bossGame, boss, foot, 18.001f); PursuitFrozenTick(bossGame, boss, "A previously active Boss still stops immediately beyond eighteen");
            foreach (string training in new[] { "basic", "sparring", "range" })
            {
                GameCore game = training == "basic" ? Fresh(data) : RangeGame(data);
                if (training == "basic") game.StartPractice();
                else Check(training == "sparring" ? game.StartSparring() : game.StartRange(0), "Enter the actual " + training + " session");
                if (training == "range")
                {
                    Enemy reserved = game.enemies[0]; game.enemies.RemoveAll(e => !ReferenceEquals(e, reserved));
                    PursuitPlace(game, reserved, new Point(12, 14), 8); PursuitFrozenTick(game, reserved, "An actual range target keeps its stationary exclusion");
                }
                Enemy enemy = PursuitActor(game, 0, new Point(12, 14), new Point(20, 14));
                Check(game.practicing, "Use the real " + training + " lifecycle flag");
                PursuitMovingTick(game, enemy, "Exactly eight preserves the original idle training gate in " + training);
                PursuitPlace(game, enemy, new Point(12, 14), 8.001f);
                PursuitFrozenTick(game, enemy, "Training never retains an eight-unit acquisition outside the original radius in " + training);
            }
        }

        public static void EnemyPursuitPreservesCommittedWarningsReleasesStunAndRecoil(Campaign data)
        {
            foreach (int kind in new[] { 0, 1, 2 })
            {
                var game = Quiet(data); Enemy enemy = PursuitActor(game, kind, new Point(15, 14), new Point(17, 14)); enemy.timer = 0;
                game.Tick(.02f, new Point()); Point committed = enemy.Position, target = enemy.target, aim = enemy.facing;
                Check(enemy.state == 1 && enemy.timer == .65f && enemy.windupDuration == .65f && (target - game.Position).Length == 0,
                    "An ordinary real preparation keeps its original duration and target for kind " + kind);
                game.save.x = 28; game.save.y = 14; game.Tick(.02f, new Point());
                Check(enemy.state == 1 && (enemy.Position - committed).Length == 0 && (enemy.target - target).Length == 0 && (enemy.facing - aim).Length == 0 && Math.Abs(enemy.timer - .63f) < .00001f,
                    "Leaving the pursuit radius cannot cancel or retarget a committed warning for kind " + kind);
                for (int frame = 0; frame < 40 && enemy.state == 1; frame++) game.Tick(.02f, new Point());
                Check(enemy.state == 0 && enemy.timer == 1.4f && enemy.releaseTime == .24f && (enemy.Position - committed).Length == 0,
                    "Committed attacks retain the original release lock and cooldown outside acquisition range for kind " + kind);
                Check(game.projectiles.Count == (kind == 1 ? 1 : 0), "Only the ordinary archer creates its single original projectile");
                if (kind == 1)
                {
                    Projectile shot = game.projectiles[0]; Point muzzle = GameCore.WeaponOrigin(committed, 1, aim);
                    Check(shot.damage == 10 && shot.source == "shot" && shot.visual == "enemyArrow" && !shot.friendly &&
                        (shot.velocity - (target - muzzle).Normalized * 5).Length < .00001f,
                        "The released arrow preserves captured aim, muzzle, speed and damage");
                }
            }
            foreach (string reaction in new[] { "stun", "release", "recoil" })
            {
                var game = Quiet(data); Point foot = new Point(12, 14); Enemy enemy = PursuitActor(game, 0, foot, new Point(20, 14));
                PursuitMovingTick(game, enemy, "Acquire before testing existing reaction locks");
                PursuitPlace(game, enemy, foot, 9); float timer = enemy.timer;
                if (reaction == "stun") enemy.stun = 1;
                else if (reaction == "release") enemy.releaseTime = 1;
                else { enemy.hitTime = 1; enemy.recoil = new Point(-1, 0); }
                game.Tick(.02f, new Point());
                Point expected = reaction == "recoil" ? foot + new Point(-.016f, 0) : foot;
                Check((enemy.Position - expected).Length < .00001f && enemy.timer == timer && !enemy.moving,
                    "An alert actor keeps the original " + reaction + " displacement and paused attack timer");
                enemy.stun = enemy.releaseTime = enemy.hitTime = 0;
                PursuitMovingTick(game, enemy, "A completed " + reaction + " resumes the same acquired pursuit with the original speed");
            }
        }

        public static void EnemyPursuitKeepsAlertsTransientAcrossSavesActorsTravelAndDiagnostics(Campaign data)
        {
            Check(typeof(EnemyRecord).GetFields().Length == 7, "Enemy records retain the original seven-field schema");
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("pursuit", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("alert", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("awareness", StringComparison.OrdinalIgnoreCase) < 0,
                    "Enemy pursuit introduces no persistent save field");
            foreach (int version in new[] { 1, 2 })
            {
                var game = Quiet(data); Point foot = new Point(12, 14); Enemy enemy = PursuitActor(game, 0, foot, new Point(20, 14));
                PursuitMovingTick(game, enemy, "Acquire before the real save boundary");
                PursuitPlace(game, enemy, foot, 9); PursuitMovingTick(game, enemy, "The saved actor was actively retaining pursuit beyond acquisition");
                Point position = enemy.Position; enemy.hp = 31; game.PrepareSave(); SaveData saved = CloneSave(game.save); saved.version = version;
                Check(saved.worldEnemies.Length == 1 && saved.worldEnemies[0].x == position.x && saved.worldEnemies[0].hp == 31,
                    "PrepareSave retains the original position and health payload");
                var loaded = new GameCore(data, saved); loaded.Start(); enemy = loaded.enemies.Find(e => e.id == 98701);
                Check(enemy != null && (enemy.Position - position).Length == 0 && enemy.hp == 31 && enemy.maxHp == 43,
                    "Version " + version + " reconstructs the ordinary legacy record without relocation or invented health");
                PursuitFrozenTick(loaded, enemy, "Version " + version + " reconstructs a fresh unaware actor beyond eight");
                PursuitPlace(loaded, enemy, foot, 8); PursuitMovingTick(loaded, enemy, "A restored actor can acquire normally within eight");
            }
            var actorGame = Quiet(data); Enemy prior = PursuitActor(actorGame, 0, new Point(12, 14), new Point(20, 14));
            PursuitMovingTick(actorGame, prior, "Acquire the actor whose ID will be reused");
            Enemy replacement = PursuitActor(actorGame, 0, new Point(12, 14), new Point(21, 14), prior.id);
            PursuitFrozenTick(actorGame, replacement, "A replacement with the same ID cannot inherit another actor's pursuit");
            actorGame.Enter(0); actorGame.Enter(1);
            replacement = PursuitActor(actorGame, 0, new Point(12, 14), new Point(21, 14), prior.id);
            PursuitFrozenTick(actorGame, replacement, "Room travel cannot transfer an old pursuit to a newly created same-ID actor");
            actorGame.NewGame(0); actorGame.Close(); actorGame.Enter(1);
            replacement = PursuitActor(actorGame, 0, new Point(12, 14), new Point(21, 14), prior.id);
            int coins = actorGame.save.coins, xp = actorGame.save.xp; float time = actorGame.save.totalTime, timerBefore = replacement.timer;
            for (int i = 0; i < 100; i++)
            {
                actorGame.DirectionTo(replacement.Position, actorGame.Position); actorGame.CanStand(replacement.Position);
                bool canFocus = actorGame.CanAcquireCombatFocus; Enemy focus = actorGame.CombatFocusTarget;
                Check(canFocus && focus == null, "Read-only focus diagnostics can inspect a visible nine-unit actor without selecting it");
            }
            Check(actorGame.save.coins == coins && actorGame.save.xp == xp && actorGame.save.totalTime == time && replacement.timer == timerBefore && replacement.hp == 43,
                "Repeated diagnostics grant no payout, health change, time or attack progression");
            PursuitFrozenTick(actorGame, replacement, "A new game and read-only path or focus diagnostics cannot manufacture enemy acquisition");
        }
    }
}
