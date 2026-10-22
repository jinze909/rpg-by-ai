using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static Enemy EvasionArcher(GameCore game, Point foot, Point player)
        {
            game.enemies.Clear(); game.save.x = player.x; game.save.y = player.y;
            var enemy = new Enemy(98601, 1, foot.x, foot.y, 43) { timer = 99 };
            game.enemies.Add(enemy);
            Check(game.CanStand(foot) && game.CanStand(player), "Archer fixtures use legal full-foot positions on the shipped map");
            return enemy;
        }
        private static Point EvasionSweep(GameCore game, Point foot, Point step)
        { return (Point)typeof(GameCore).GetMethod("SweepActor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { foot, step }); }
        private static bool EvasionVisible(GameCore game, Point from, Point to)
        { return (bool)typeof(GameCore).GetMethod("ClearAttackPath", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { from, to }); }

        public static void ArcherEvasionEscapesMirroredWallsAndRejectsClosingCornerSlides(Campaign data)
        {
            foreach (float dt in new[] { .005f, .02f, .05f }) foreach (int fixture in new[] { 0, 1, 2, 3 })
            {
                var game = Quiet(data);
                bool horizontal = fixture >= 2; int mirror = fixture % 2 == 0 ? 1 : -1;
                Point foot = horizontal ? new Point(10.5f, mirror == 1 ? 8.815f : 10.185f) : new Point(mirror == 1 ? 11.225f : 8.775f, 9.5f);
                Point player = foot + (horizontal ? new Point(0, -mirror * 2) : new Point(mirror * 2, 0));
                Point away = (foot - player).Normalized, tangent = new Point(-away.y, away.x);
                Enemy enemy = EvasionArcher(game, foot, player); float budget = dt * 1.6f;
                Check(EvasionVisible(game, foot, player) && (EvasionSweep(game, foot, away * budget) - foot).Length == 0,
                    "Both sides of shipped vertical and horizontal forest walls block straight retreat while leaving the player visible");
                foreach (int side in new[] { -1, 1 })
                    Check(Math.Abs((EvasionSweep(game, foot, tangent * (side * budget)) - foot).Length - budget) < .00001f,
                        "Both perpendicular alternatives have the full existing movement budget");
                game.Tick(dt, new Point()); Point escaped = enemy.Position;
                Point travelled = escaped - foot;
                Check(Math.Abs(travelled.x * away.x + travelled.y * away.y) < .00001f && Math.Abs(travelled.Length - budget) < .00001f &&
                    (escaped - player).Length > (foot - player).Length && game.CanStand(escaped),
                    "A visible cornered archer escapes sideways without closing distance or exceeding its retreat speed");
                Check(enemy.moving && enemy.backpedaling && Math.Abs(enemy.moveTime - budget) < .00001f &&
                    (enemy.facing - (player - escaped).Normalized).Length < .00001f,
                    "Actual escape displacement drives locomotion, retreat animation and player-facing direction");
                var repeat = Quiet(data); Enemy repeated = EvasionArcher(repeat, foot, player); repeat.Tick(dt, new Point());
                Check((repeated.Position - escaped).Length == 0, "Equal alternatives keep a deterministic tie choice");
                for (int frame = 0; frame < (int)Math.Ceiling(2 / dt); frame++)
                {
                    Point before = enemy.Position; float distance = (before - player).Length; game.Tick(dt, new Point());
                    Check(game.CanStand(enemy.Position) && (enemy.Position - before).Length <= budget + .00001f &&
                        (enemy.Position - player).Length >= distance, "Repeated real ticks preserve full-foot clearance, the speed budget and separation");
                }
                Check((enemy.Position - player).Length >= 3, "Two seconds of actual movement escapes the wall and restores the established close-range separation");
                game = Quiet(data); foot = new Point(2.225f, 2.185f); player = new Point(4.225f, 4.185f);
                enemy = EvasionArcher(game, foot, player); away = (foot - player).Normalized;
                Check(EvasionVisible(game, foot, player) && (EvasionSweep(game, foot, away * budget) - foot).Length == 0,
                    "The shipped boundary corner blocks the entire diagonal retreat");
                foreach (int side in new[] { -1, 1 })
                {
                    Point swept = EvasionSweep(game, foot, new Point(-away.y, away.x) * (side * budget));
                    Check((swept - foot).Length > 0 && (swept - player).Length < (foot - player).Length,
                        "Axis sliding makes each nominally lateral corner alternative move toward the player");
                }
                game.Tick(dt, new Point());
                Check((enemy.Position - foot).Length == 0 && !enemy.moving && !enemy.backpedaling && enemy.moveTime == 0,
                    "An archer stays still when both legal corner slides reduce separation");
            }
        }

        public static void ArcherEvasionPreservesStraightRetreatAndExistingPerception(Campaign data)
        {
            foreach (float dt in new[] { .005f, .02f, .05f })
            {
                var game = Quiet(data); Point foot = new Point(15, 14), player = new Point(17, 14);
                Enemy enemy = EvasionArcher(game, foot, player); game.Tick(dt, new Point());
                Check((enemy.Position - (foot + new Point(-dt * 1.6f, 0))).Length < .00001f && enemy.backpedaling && enemy.moving && enemy.state == 0,
                    "An unobstructed ordinary archer retains its exact established straight retreat");
            }
            foreach (float distance in new[] { 3f, 4.5f, 8.01f })
            {
                var game = Quiet(data); Point foot = new Point(15, 14); Enemy enemy = EvasionArcher(game, foot, foot + new Point(distance, 0));
                game.Tick(.02f, new Point());
                Check((enemy.Position - foot).Length == 0 && !enemy.moving && !enemy.backpedaling &&
                    Math.Abs(enemy.timer - (distance > 8 ? 99 : 98.98f)) < .00001f,
                    "The original strict close-distance threshold, preferred range and awareness range remain intact");
            }
            var hidden = Quiet(data); Point start = new Point(11.225f, 9.5f), target = new Point(8.775f, 9.5f);
            Enemy archer = EvasionArcher(hidden, start, target); archer.timer = 0;
            Check(!EvasionVisible(hidden, start, target), "The opposing forest approach is occluded by its shipped wall");
            Point expected = EvasionSweep(hidden, start, hidden.DirectionTo(start, target) * (.02f * 1.5f)); hidden.Tick(.02f, new Point());
            Check((archer.Position - expected).Length < .00001f && !archer.backpedaling && archer.state == 0,
                "Occluded archers retain the existing pursuit route and cannot prepare an unseen shot");
        }

        public static void ArcherEvasionKeepsCommittedShotsAndTrainingExclusions(Campaign data)
        {
            var game = Quiet(data); Enemy enemy = EvasionArcher(game, new Point(15, 14), new Point(17, 14)); enemy.timer = 0;
            game.Tick(.02f, new Point()); Point committed = enemy.Position, aim = enemy.facing, target = enemy.target;
            Check(enemy.state == 1 && enemy.timer == .65f && enemy.windupDuration == .65f && (target - game.Position).Length == 0,
                "The existing real preparation still captures a .65-second aimed shot after retreat");
            game.save.x = 17; game.save.y = 16; game.Tick(.02f, new Point());
            Check((enemy.Position - committed).Length == 0 && (enemy.facing - aim).Length == 0 && (enemy.target - target).Length == 0 &&
                Math.Abs(enemy.timer - .63f) < .00001f && !enemy.moving && !enemy.backpedaling,
                "A committed wind-up keeps its body, aim, target and countdown when the player moves");
            for (int frame = 0; frame < 40 && enemy.state == 1; frame++) game.Tick(.02f, new Point());
            Check(game.projectiles.Count == 1 && enemy.state == 0 && enemy.timer == 1.4f && enemy.releaseTime == .24f,
                "The real release emits exactly one arrow and preserves the mature cooldown and release lock");
            Projectile shot = game.projectiles[0]; Point muzzle = GameCore.WeaponOrigin(committed, 1, aim);
            Check(!shot.friendly && shot.damage == 10 && shot.visual == "enemyArrow" && shot.source == "shot" &&
                (shot.velocity - (target - muzzle).Normalized * 5).Length < .00001f && (enemy.Position - committed).Length == 0,
                "The released arrow retains its captured target, original speed, damage and weapon muzzle");
            game.invulnerable = 0; game.save.health = 120;
            Point contact = shot.position + shot.velocity * .02f; game.save.x = contact.x; game.save.y = contact.y;
            game.Tick(.02f, new Point());
            Check(game.save.health == 110 && !game.projectiles.Contains(shot) && (enemy.Position - committed).Length == 0,
                "An actual arrow contact still deals exactly ten health damage while release prevents movement");
            foreach (string exclusion in new[] { "stun", "release", "hit", "boss", "hunter", "heavy" })
            {
                game = Quiet(data); Point foot = new Point(11.225f, 9.5f); enemy = EvasionArcher(game, foot, foot + new Point(2, 0));
                if (exclusion == "stun") enemy.stun = 1;
                else if (exclusion == "release") enemy.releaseTime = 1;
                else if (exclusion == "hit") { enemy.hitTime = 1; enemy.recoil = new Point(); }
                else if (exclusion == "boss") enemy.boss = true;
                else if (exclusion == "hunter") enemy.kind = 0;
                else enemy.kind = 2;
                Point expected = exclusion == "hunter" || exclusion == "heavy" ?
                    EvasionSweep(game, foot, game.DirectionTo(foot, game.Position) * (.02f * (exclusion == "hunter" ? 2.1f : 1.5f))) : foot;
                game.Tick(.02f, new Point());
                Check((enemy.Position - expected).Length < .00001f && !enemy.backpedaling,
                    "Existing movement and lock behavior stays unchanged for " + exclusion);
            }
            var basic = Fresh(data); basic.StartPractice();
            enemy = EvasionArcher(basic, new Point(15, 14), new Point(17, 14)); basic.Tick(.02f, new Point());
            Check(basic.practicing && !basic.SafeTrainingActive && enemy.x == 15 && enemy.y == 14 && !enemy.backpedaling,
                "An actual basic-practice session keeps its no-retreat policy");
            var range = RangeGame(data); Check(range.StartRange(0), "Enter the actual loadout range");
            enemy = range.enemies[0]; enemy.kind = 1; enemy.x = 15; enemy.y = 14; enemy.timer = 0; range.save.x = 17; range.save.y = 14;
            range.Tick(.02f, new Point());
            Check(range.RangeActive && enemy.x == 15 && enemy.y == 14 && enemy.state == 0 && !enemy.moving && !enemy.backpedaling,
                "An actual reserved range actor remains stationary and cannot prepare a hostile shot");
            var sparring = SparringGame(data); Check(sparring.StartSparring(), "Enter actual tactical sparring");
            SparringClearStage(sparring); Check(sparring.ContinueSparring(), "Reach the heavy lesson");
            SparringClearStage(sparring); Check(sparring.ContinueSparring(), "Reach the actual mixed lesson archer");
            enemy = sparring.enemies.Find(e => e.kind == 1); Check(enemy != null, "The authored mixed wave includes its archer");
            foreach (Enemy other in sparring.enemies) if (!ReferenceEquals(other, enemy)) other.stun = 99;
            enemy.x = 15; enemy.y = 14; enemy.timer = 99; enemy.hitTime = enemy.stun = enemy.releaseTime = 0; enemy.state = 0;
            sparring.save.x = 17; sparring.save.y = 14; sparring.hitStop = 0; sparring.playerAction = null; sparring.Tick(.02f, new Point());
            Check(sparring.SparringActive && enemy.backpedaling && Math.Abs(enemy.x - 14.968f) < .00001f && enemy.y == 14,
                "The actual mixed-wave sparring archer retains ordinary close-range evasion");
        }

        public static void ArcherEvasionPersistsOnlyExistingCoordinatesAndLoadsBothSaveVersions(Campaign data)
        {
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("archer", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("evasion", StringComparison.OrdinalIgnoreCase) < 0 &&
                    field.Name.IndexOf("backpedal", StringComparison.OrdinalIgnoreCase) < 0, "Wall evasion adds no persistent movement state");
            Check(typeof(EnemyRecord).GetFields().Length == 7, "Enemy snapshots retain their original seven-field schema");
            foreach (int version in new[] { 1, 2 })
            {
                var game = Quiet(data); Enemy enemy = EvasionArcher(game, new Point(15, 14), new Point(17, 14)); game.Tick(.02f, new Point());
                Point position = enemy.Position; enemy.state = 1; enemy.timer = .3f; enemy.windupDuration = .65f; enemy.stun = .2f;
                game.PrepareSave(); SaveData saved = CloneSave(game.save); saved.version = version;
                Check(saved.worldEnemies.Length == 1 && saved.worldEnemies[0].x == position.x && saved.worldEnemies[0].y == position.y,
                    "PrepareSave captures the actual moved coordinates through the original enemy record");
                var loaded = new GameCore(data, saved); loaded.Start(); enemy = loaded.enemies.Find(e => e.id == 98601);
                Check(enemy != null && (enemy.Position - position).Length == 0 && enemy.hp == 43 && enemy.maxHp == 43 &&
                    enemy.state == 0 && enemy.stun == 0 && enemy.releaseTime == 0 && !enemy.moving && !enemy.backpedaling && enemy.moveTime == 0,
                    "Version " + version + " reconstructs existing coordinates and health with fresh transient movement and attacks");
                var legacy = new SaveData { version = version, room = 1, stage = 1, coins = 91, x = 13.225f, y = 9.5f, snapshotRoom = 1,
                    worldEnemies = new[] { new EnemyRecord { id = 98601, kind = 1, x = 11.225f, y = 9.5f, hp = 31, maxHp = 43 } } };
                legacy.flags.Add("intro"); loaded = new GameCore(data, legacy); loaded.Start(); enemy = loaded.enemies.Find(e => e.id == 98601);
                Check(enemy != null && enemy.x == 11.225f && enemy.y == 9.5f && enemy.hp == 31 && loaded.save.coins == 91 &&
                    loaded.CanStand(enemy.Position) && loaded.CanStand(loaded.Position),
                    "Legacy version " + version + " accepts the original wall-side coordinates without relocation, rewards or invented state");
            }
        }
    }
}
