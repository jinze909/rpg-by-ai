using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly float[] ArcherOverlapSteps = { .005f, 1 / 60f, .02f, .05f };

        private static Enemy ArcherOverlapLand(GameCore game, Point foot, Point offset, Point facing, float dt)
        {
            // The actor is still in an existing release lock while one actual
            // player movement step reaches its legal feet. No body collision
            // prevents this ordinary movement from sharing the actor's ground.
            Enemy enemy = EvasionArcher(game, foot, new Point(foot.x - game.Speed * dt + offset.x, foot.y + offset.y));
            enemy.Face(facing); enemy.releaseTime = dt * 1.5f;
            Point playerBefore = game.Position; game.Tick(dt, new Point(1, 0));
            Check((game.Position - playerBefore).Length > .001f && (enemy.Position - foot).Length == 0 &&
                !enemy.moving && !enemy.backpedaling && enemy.releaseTime > 0,
                "Real player movement reaches the planted actor while its original release lock still owns the body");
            Point landed = game.Position - foot;
            Check(offset.Length == 0 ? landed.Length == 0 : landed.Length > 0 && landed.Length < .0001f,
                "The movement reproduction lands at exact or genuinely nonzero sub-deadband separation");
            Check(game.CanStand(game.Position) && game.CanStand(enemy.Position) && EvasionVisible(game, enemy.Position, game.Position),
                "Both complete footprints and the actual perception ray remain legal after overlap");
            return enemy;
        }

        private static Point ArcherOverlapUnit(Point delta)
        {
            float length = delta.Length;
            Check(length > 0, "An expected real separation has a nonzero magnitude");
            return new Point(delta.x / length, delta.y / length);
        }

        private static void ArcherOverlapStep(GameCore game, Enemy enemy, float dt, string reason)
        {
            Point before = enemy.Position; float separation = (before - game.Position).Length, gait = enemy.moveTime, timer = enemy.timer;
            game.Tick(dt, new Point()); Point step = enemy.Position - before;
            Check(step.Length > .001f && step.Length <= dt * 1.6f + .00001f && game.CanStand(enemy.Position) &&
                (enemy.Position - game.Position).Length > separation, reason);
            Check(enemy.moving && enemy.backpedaling && Math.Abs(enemy.moveTime - gait - step.Length) < .00001f &&
                (enemy.facing - (game.Position - enemy.Position).Normalized).Length < .00001f,
                "Actual swept overlap escape owns movement, reversed gait distance and player-facing direction");
            Check(enemy.state == 0 && Math.Abs(enemy.timer - (timer - dt)) < .00002f && game.projectiles.Count == 0,
                "Overlap escape keeps the original idle cooldown and cannot invent a shot or preparation");
        }

        public static void ArcherOverlapMovementEscapeHandlesExactAndTinySeparation(Campaign data)
        {
            foreach (float dt in ArcherOverlapSteps) foreach (Point facing in new[] {
                new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1), new Point(1, 1) })
            {
                var game = Quiet(data); Point foot = new Point(15, 14);
                Enemy enemy = ArcherOverlapLand(game, foot, new Point(), facing, dt); Point capturedFacing = enemy.facing;
                ArcherOverlapStep(game, enemy, dt, "Exact shared feet escape on the next eligible idle tick at dt=" + dt);
                Check((enemy.Position - (foot + capturedFacing * -dt * 1.6f)).Length < .00001f,
                    "Exact overlap chooses the deterministic direction behind its captured original facing");
                for (int frame = 0; frame < (int)Math.Ceiling(2 / dt); frame++)
                {
                    Point before = enemy.Position; float distance = (before - game.Position).Length, gait = enemy.moveTime;
                    game.Tick(dt, new Point()); Point travelled = enemy.Position - before;
                    Check(game.CanStand(enemy.Position) && travelled.Length <= dt * 1.6f + .00001f &&
                        (enemy.Position - game.Position).Length >= distance && Math.Abs(enemy.moveTime - gait - travelled.Length) < .00001f,
                        "Sustained real ticks retain legal feet, speed, exact gait and nonclosing separation after coincidence");
                }
                Check((enemy.Position - game.Position).Length >= 3, "The formerly coincident archer reaches its original useful retreat band");
            }
            foreach (float dt in ArcherOverlapSteps) foreach (Point offset in new[] {
                new Point(.00005f, 0), new Point(-.00005f, 0), new Point(0, .00005f), new Point(0, -.00005f), new Point(.00003f, -.00003f) })
            {
                var game = Quiet(data); Point foot = new Point(15, 14);
                Enemy enemy = ArcherOverlapLand(game, foot, offset, offset.x != 0 ? new Point(0, 1) : new Point(1, 0), dt);
                Point actual = game.Position - foot, away = ArcherOverlapUnit(actual) * -1;
                Check(actual.Normalized.Length == 0, "The tiny movement landing falls inside the shared Point.Normalized deadband");
                ArcherOverlapStep(game, enemy, dt, "Nonzero sub-deadband separation must evade instead of stalling at dt=" + dt);
                Check((enemy.Position - (foot + away * dt * 1.6f)).Length < .00001f,
                    "Every nonzero overlap uses its actual player-away direction, including an axis different from the actor's fallback facing");
            }
        }

        public static void ArcherOverlapTerrainSweepKeepsDeterministicFacingAndGait(Campaign data)
        {
            foreach (float dt in ArcherOverlapSteps) foreach (int fixture in new[] { 0, 1, 2, 3 })
            {
                bool horizontal = fixture >= 2; int mirror = fixture % 2 == 0 ? 1 : -1;
                Point foot = horizontal ? new Point(10.5f, mirror == 1 ? 8.815f : 10.185f) : new Point(mirror == 1 ? 11.225f : 8.775f, 9.5f);
                Point facing = horizontal ? new Point(0, -mirror) : new Point(mirror, 0);
                var game = Quiet(data); Enemy enemy = EvasionArcher(game, foot, foot); enemy.Face(facing);
                Check((EvasionSweep(game, foot, facing * -dt * 1.6f) - foot).Length == 0,
                    "A real shipped wall blocks the fallback retreat on each mirrored horizontal/vertical face");
                var repeated = Quiet(data); Enemy twin = EvasionArcher(repeated, foot, foot); twin.Face(facing);
                ArcherOverlapStep(game, enemy, dt, "A shared-foot archer must use a legal wall-side escape when its fallback points into terrain");
                ArcherOverlapStep(repeated, twin, dt, "An identical wall overlap must independently find a legal escape");
                Point travelled = enemy.Position - foot;
                Check((enemy.Position - twin.Position).Length == 0 && Math.Abs(travelled.Length - dt * 1.6f) < .00001f &&
                    Math.Abs(travelled.x * facing.x + travelled.y * facing.y) < .00001f,
                    "Equal wall alternatives are deterministic, perpendicular and share one existing movement budget");
                Point swept = EvasionSweep(game, foot, travelled);
                Check((swept - enemy.Position).Length < .00001f, "Fallback wall escape is reproducible through the original complete-foot sweep");
            }
            // The old straight, wall-side, strict-threshold and no-closing-slide
            // scenarios remain the production regression oracle for all normal
            // separations; no new fallback may perturb those existing paths.
            ArcherEvasionEscapesMirroredWallsAndRejectsClosingCornerSlides(data);
            ArcherEvasionPreservesStraightRetreatAndExistingPerception(data);
        }

        public static void ArcherOverlapPreservesCommittedAttacksAndExistingExclusions(Campaign data)
        {
            ArcherEvasionKeepsCommittedShotsAndTrainingExclusions(data);
            foreach (string exclusion in new[] { "stun", "release", "hit", "boss", "hunter", "heavy" })
            {
                var game = Quiet(data); Point foot = new Point(15, 14); Enemy enemy = EvasionArcher(game, foot, foot);
                if (exclusion == "stun") enemy.stun = 1;
                else if (exclusion == "release") enemy.releaseTime = 1;
                else if (exclusion == "hit") { enemy.hitTime = 1; enemy.recoil = new Point(); }
                else if (exclusion == "boss") enemy.boss = true;
                else if (exclusion == "hunter") enemy.kind = 0;
                else enemy.kind = 2;
                float timer = enemy.timer; game.Tick(.02f, new Point());
                Check((enemy.Position - foot).Length == 0 && !enemy.moving && !enemy.backpedaling && enemy.moveTime == 0 && enemy.state == 0 &&
                    Math.Abs(enemy.timer - (exclusion == "stun" || exclusion == "release" || exclusion == "hit" ? timer : timer - .02f)) < .00001f,
                    "Exact overlap cannot bypass existing body/attack ownership for " + exclusion);
            }
            var basic = Fresh(data); basic.StartPractice(); Enemy trainee = EvasionArcher(basic, new Point(15, 14), new Point(15, 14));
            basic.Tick(.02f, new Point());
            Check(basic.practicing && !basic.SafeTrainingActive && !trainee.moving && !trainee.backpedaling && trainee.x == 15 && trainee.y == 14,
                "Actual basic training keeps its original no-retreat policy even at shared feet");
            var range = RangeGame(data); Check(range.StartRange(0), "The genuine safe range starts");
            Enemy dummy = range.enemies[0]; dummy.kind = 1; dummy.x = range.save.x; dummy.y = range.save.y; dummy.timer = 0;
            Point dummyFeet = dummy.Position; range.Tick(.02f, new Point());
            Check(range.RangeActive && (dummy.Position - dummyFeet).Length == 0 && dummy.state == 0 && !dummy.moving && !dummy.backpedaling,
                "The actual reserved range actor remains inert under exact overlap");

            var committed = Quiet(data); Enemy archer = EvasionArcher(committed, new Point(15, 14), new Point(17, 14)); archer.timer = 0;
            committed.Tick(.02f, new Point()); Point planted = archer.Position, target = archer.target, aim = archer.facing;
            Check(archer.state == 1 && archer.timer == .65f && archer.windupDuration == .65f, "A genuine original .65-second preparation begins before approach");
            for (int frame = 0; frame < 40 && (committed.Position - planted).Length > .00001f; frame++)
            {
                Point delta = planted - committed.Position;
                Point movement = delta * (1 / (committed.Speed * .02f)); if (movement.Length > 1) movement = movement.Normalized;
                committed.Tick(.02f, movement);
                Check(archer.state == 1 && (archer.Position - planted).Length == 0 && (archer.target - target).Length == 0 &&
                    (archer.facing - aim).Length == 0 && !archer.moving && !archer.backpedaling,
                    "Real player approach cannot make the preparing archer escape, refacing or recapture its announced target");
            }
            Check((committed.Position - planted).Length < .00001f && archer.state == 1, "Real movement reaches shared feet before the full original warning expires");
            while (archer.state == 1) committed.Tick(.02f, new Point());
            Check(archer.releaseTime == .24f && archer.timer == 1.4f && committed.projectiles.Count == 1 && (archer.Position - planted).Length == 0,
                "Only the unchanged warning boundary releases one arrow and keeps its planted release lock");
            Projectile shot = committed.projectiles[0]; Point muzzle = GameCore.WeaponOrigin(planted, 1, aim);
            Check(shot.damage == 10 && Math.Abs(shot.velocity.Length - 5) < .00001f &&
                (shot.velocity - (target - muzzle).Normalized * 5).Length < .00001f && shot.visual == "enemyArrow" && shot.source == "shot",
                "Overlap during preparation cannot alter the captured arrow's original target, five-unit speed or ten damage");
        }

        public static void ArcherOverlapRestoreUsesExistingEnemySnapshotsAndSchema(Campaign data)
        {
            Check(typeof(EnemyRecord).GetFields().Length == 7, "Overlap recovery retains the original seven-field enemy snapshot");
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("overlap", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("evasion", StringComparison.OrdinalIgnoreCase) < 0,
                    "Overlap recovery adds no persistent movement state");
            foreach (int version in new[] { 1, 2 }) foreach (float offset in new[] { 0f, .00005f, -.00005f })
            {
                var game = Quiet(data); Point foot = new Point(15, 14);
                Enemy enemy = ArcherOverlapLand(game, foot, new Point(offset, 0), new Point(-1, 0), .02f);
                enemy.hp = 31; enemy.state = 1; enemy.stun = .2f; enemy.timer = .3f; enemy.windupDuration = .65f;
                game.save.coins = 191; game.save.health = 73; game.save.energy = 47; game.save.flags.Add("archer_overlap_mature_story");
                game.PrepareSave(); SaveData saved = CloneSave(game.save); saved.version = version;
                Check(saved.snapshotRoom == 1 && saved.worldEnemies.Length == 1 && saved.worldEnemies[0].hp == 31 &&
                    saved.worldEnemies[0].x == foot.x && saved.worldEnemies[0].y == foot.y,
                    "Original PrepareSave preserves the shared-foot actor's canonical HP and coordinates");
                var loaded = new GameCore(data, saved); loaded.Start(); enemy = loaded.enemies.Find(e => e.id == 98601);
                Check(enemy != null && enemy.hp == 31 && enemy.maxHp == 43 && (enemy.Position - foot).Length == 0 &&
                    enemy.state == 0 && enemy.stun == 0 && enemy.releaseTime == 0 && enemy.moveTime == 0 && !enemy.moving && !enemy.backpedaling &&
                    loaded.save.coins == 191 && loaded.save.health == 73 && loaded.save.energy == 47 && loaded.Has("archer_overlap_mature_story"),
                    "Version " + version + " keeps mature saved value and the existing transient-reset policy");
                Point delta = loaded.Position - foot;
                Point away = delta.Length > 0 ? ArcherOverlapUnit(delta) * -1 : enemy.facing * -1;
                Point expected = EvasionSweep(loaded, foot, away * .02f * 1.6f);
                ArcherOverlapStep(loaded, enemy, .02f, "A restored version " + version + " shared-foot archer escapes on its first normal idle tick");
                Check((enemy.Position - expected).Length < .00001f, "Fresh restored facing is only the exact-zero fallback; tiny saved separation retains its own away heading");
                Point escaped = enemy.Position; loaded.PrepareSave(); var again = new GameCore(data, CloneSave(loaded.save)); again.Start();
                Enemy restored = again.enemies.Find(e => e.id == 98601);
                Check(restored != null && (restored.Position - escaped).Length == 0 && restored.hp == 31 && restored.moveTime == 0,
                    "The ordinary next snapshot retains actual escaped feet without serializing gait or fallback intent");
            }
        }
    }
}
