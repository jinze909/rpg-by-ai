using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static ActorAction AttackBufferAwait(GameCore game, float step, ActorAction previous = null, bool held = false)
        {
            for (int frame = 0; frame < 80; frame++)
            {
                game.Tick(step, new Point(), new Point(-1, 0));
                if (held) game.Attack(new Point(-1, 0));
                ActorAction action = game.playerAction;
                if (action != null && action != previous && action.kind == "attack") return action;
            }
            return null;
        }
        private static int AttackBufferReleases(GameCore game)
        { int count = 0; foreach (string sound in game.sounds) if (sound == "attack" || sound == "arrow" || sound == "spell") count++; return count; }

        public static void AttackBufferThresholdCadencesOverlapAndSingleDispatch(Campaign data)
        {
            Check(GameCore.AttackBufferWindow == .12f, "The discrete basic window is exactly .12 simulation seconds");
            foreach (float step in new[] { .01f, .02f, 1f / 30, .05f })
            for (int profession = 0; profession < 3; profession++)
            for (int blocker = 0; blocker < 5; blocker++)
            foreach (float delay in new[] { .12f, .121f })
            {
                GameCore game = Quiet(data, profession); ActorAction old = null;
                if (blocker == 1 || blocker == 4)
                {
                    game.Attack(new Point(1, 0)); old = game.playerAction;
                    old.elapsed = old.contact + .1f - (delay - (blocker == 4 ? .015f : 0));
                    if (blocker == 4) game.playerHitTime = .015f;
                }
                if (blocker == 2) game.dashTime = delay;
                if (blocker == 3) game.playerHitTime = delay;
                game.attackCooldown = delay;
                float energy = game.save.energy; int effects = game.effects.Count;
                game.PressAttack(new Point(0, -4));
                Check(game.playerAction == old && game.save.energy == energy && game.effects.Count == effects && game.projectiles.Count == 0,
                    "A blocked press neither replaces contact nor pays or releases early");
                ActorAction next = AttackBufferAwait(game, step, old);
                string context = " profession " + profession + " blocker " + blocker + " dt " + step + " delay " + delay;
                Check((next != null) == (delay == .12f), "Accept .120 and reject .121 at every actual readiness boundary:" + context);
                if (next == null) continue;
                Check(next.elapsed == 0 && !next.released && next.direction.y < -.999f
                    && Math.Abs(game.attackCooldown - game.Profile(game.save.weapon, game.Style).interval) < .0001f,
                    "A saved press starts a fresh full action at readiness, without backdated preparation:" + context);
                if (old != null) Check(old.released && old.elapsed + .00001f >= old.contact + .1f,
                    "A buffered basic preserves the preceding action's real contact interval");
                int previousReleases = old == null ? 0 : 1;
                Advance(game, 1.4f);
                Check(game.playerAction == null && AttackBufferReleases(game) == previousReleases + 1,
                    "Releasing the input produces exactly one saved basic and never another automatic attack");
            }
            foreach (float step in new[] { .02f, 1f / 30, .05f })
            {
                GameCore overlap = Quiet(data, 1); overlap.Attack(new Point(1, 0)); ActorAction old = overlap.playerAction;
                old.elapsed = old.contact - .005f;
                overlap.attackCooldown = .11f; overlap.dashTime = .10f; overlap.playerHitTime = .01f;
                overlap.PressAttack(new Point(0, -1));
                ActorAction action = AttackBufferAwait(overlap, step, old);
                Check(action != null && action.direction.y < -.999f && overlap.save.totalTime < .22f,
                    "Overlapping clocks use their maximum, including unreleased contact plus recoil, instead of summing unrelated delays");
            }
            GameCore immediate = Quiet(data); immediate.PressAttack(new Point(3, 0));
            Check(immediate.playerAction != null && immediate.playerAction.elapsed == 0 && immediate.playerAction.direction.x == 1,
                "A fully ready press retains mature immediate attack behavior");
            GameCore held = Quiet(data); held.attackCooldown = .08f; held.Attack(new Point(1, 0)); Advance(held, .2f);
            Check(held.playerAction == null && AttackBufferReleases(held) == 0, "Held Attack remains a polling API and never creates discrete intent");
        }

        public static void AttackBufferCapturesAimAndUsesExecutionValuesOnce(Campaign data)
        {
            GameCore archer = Quiet(data, 1); archer.save.weaponStyles[1] = 1; archer.save.energy = 70; archer.attackCooldown = .08f;
            Point target = new Point(21, 12), direction = (target - archer.Position).Normalized;
            archer.SetAimTarget(target); archer.PressAttack(direction * 7);
            archer.SetAimTarget(new Point(5, 18)); archer.SetAim(new Point(-1, 0));
            ActorAction arrow = AttackBufferAwait(archer, .02f);
            Check(arrow != null && (arrow.direction - direction).Length < .0001f && arrow.target.HasValue
                && (arrow.target.Value - target).Length < .0001f && archer.projectiles.Count == 0,
                "A discrete press normalizes and retains its pointer direction and target through later live aim changes");
            FocusRelease(archer, arrow);
            Point origin = GameCore.WeaponOrigin(archer.Position, 1, direction);
            Check(archer.projectiles.Count == 1 && archer.projectiles[0].piercing
                && (archer.projectiles[0].velocity.Normalized - (target - origin).Normalized).Length < .001f,
                "The real paid-style arrow releases once from the authored muzzle toward the captured pointer");
            Effect release = archer.effects.Find(effect => effect.kind == "release");
            Check(release != null && (release.position - origin).Length < .001f && (release.direction - direction).Length < .001f,
                "The actual release effect uses the retained direction and matching muzzle");

            GameCore focused = Quiet(data, 1); Enemy enemy = FocusEnemy(focused, 9721, 18, 14); FocusAt(focused, enemy);
            focused.attackCooldown = .08f; Point captured = enemy.Position; focused.PressAttack(new Point(-1, 0));
            enemy.x = 12; enemy.y = 18; focused.Tick(.02f, new Point());
            Check(focused.aim.y > .999f, "Live focus follows the moving body while the basic press remains queued");
            ActorAction focusedArrow = AttackBufferAwait(focused, .02f);
            Check(focusedArrow != null && focusedArrow.direction.x > .999f && focusedArrow.target.HasValue
                && (focusedArrow.target.Value - captured).Length < .001f && ReferenceEquals(focused.CombatFocusTarget, enemy),
                "Focus resolves once at the press; dispatch cannot re-resolve the moving focused body");

            for (int profession = 0; profession < 3; profession++)
            {
                GameCore game = Quiet(data, profession); game.save.weaponStyles[profession] = 1;
                game.save.energy = 70; game.attackCooldown = .08f;
                float pressedDamage = game.Profile(profession, 1).damage; game.PressAttack(new Point(1, 0));
                Check(game.playerAction == null && game.save.energy == 70 && game.combo == 0, "An accepted intent reserves no energy, combo or prepared action");
                game.save.talents[0] = 2; game.combo = 1; game.comboTimer = 1; game.riposteTime = .3f;
                ActorAction action = AttackBufferAwait(game, .02f);
                float expectedDamage = game.Profile(profession, 1).damage * (profession == 0 ? 1.25f : 1) * 1.5f;
                Check(action != null && action.combo == 2 && action.impact.counter && action.damage > pressedDamage
                    && Math.Abs(action.damage - expectedDamage) < .001f && Math.Abs(action.impact.criticalChance - .11f) < .001f,
                    "Execution evaluates current damage, next combo, riposte and immutable impact values for profession " + profession);
                Check(Math.Abs(game.save.energy - (70 + .08f * (profession == 2 ? 18 : 14) - 4)) < .001f && game.riposteTime <= 0,
                    "Execution pays the current weapon profile's four energy and consumes riposte exactly once");
                float preparedDamage = action.damage; ImpactProfile impact = action.impact; game.save.talents[0] = 3;
                FocusRelease(game, action); Advance(game, 1.4f);
                Check(action.damage == preparedDamage && ReferenceEquals(action.impact, impact) && AttackBufferReleases(game) == 1,
                    "After preparation starts, neither build changes nor later ticks repeat or rewrite the single paid attack");
            }
        }

        public static void AttackBufferHonorsDiscretePriorityLatestPressAndHitStop(Campaign data)
        {
            GameCore skill = Quiet(data); skill.attackCooldown = .08f; skill.PressAttack(new Point(1, 0));
            skill.skillCooldown = .12f; skill.Skill(new Point(0, -1)); skill.PressAttack(new Point(1, 0)); Advance(skill, .14f);
            Check(skill.playerAction != null && skill.playerAction.kind == "skill" && skill.playerAction.direction.y < -.999f && AttackBufferReleases(skill) == 0,
                "Accepted Q clears a saved basic, and subsequent basics cannot displace the queued Q");
            GameCore dodge = Quiet(data); dodge.attackCooldown = .08f; dodge.PressAttack(new Point(1, 0));
            dodge.dashCooldown = .08f; dodge.Dash(new Point(0, -1)); dodge.PressAttack(new Point(1, 0)); Advance(dodge, .08f);
            Check(dodge.dashTime > 0 && dodge.playerAction == null && dodge.VisualFacing.y < -.999f,
                "Accepted dodge clears a saved basic and keeps priority over later basic presses");
            Advance(dodge, .8f); Check(AttackBufferReleases(dodge) == 0, "A superseded basic cannot revive after the dodge");
            for (int rejection = 0; rejection < 4; rejection++)
            {
                GameCore game = Quiet(data); game.attackCooldown = .08f; game.PressAttack(new Point(0, -1));
                if (rejection == 0) { game.skillCooldown = .201f; game.Skill(new Point(1, 0)); }
                if (rejection == 1) { game.save.energy = 39; game.Skill(new Point(1, 0)); }
                if (rejection == 2) { game.dashCooldown = .121f; game.Dash(new Point(1, 0)); }
                if (rejection == 3) { game.save.energy = 19; game.Dash(new Point(1, 0)); }
                ActorAction action = AttackBufferAwait(game, .02f);
                Check(action != null && action.direction.y < -.999f && game.dashTime == 0,
                    "Rejected cooldown or affordability Q/dodge cannot steal an accepted basic: " + rejection);
            }
            GameCore latest = Quiet(data, 1); latest.attackCooldown = .1f; latest.PressAttack(new Point(1, 0)); latest.Tick(.02f, new Point());
            Point target = new Point(12, 20); latest.SetAimTarget(target); latest.PressAttack(new Point(0, 8));
            ActorAction last = AttackBufferAwait(latest, .02f, null, true);
            Check(last != null && last.direction.y > .999f && last.target.HasValue && (last.target.Value - target).Length < .001f,
                "The latest accepted basic replaces one intent; held Attack cannot overwrite its saved aim or target");
            Advance(latest, 1.4f); Check(AttackBufferReleases(latest) == 1, "Repeated accepted presses still dispatch exactly one intent");
            foreach (float cooldown in new[] { 0f, .08f })
            {
                GameCore frozen = Quiet(data); frozen.save.weaponStyles[0] = 1; frozen.save.energy = 70;
                frozen.attackCooldown = cooldown; frozen.hitStop = .1f; frozen.PressAttack(new Point(0, -1));
                frozen.Tick(.05f, new Point()); frozen.Tick(.05f, new Point());
                Check(frozen.playerAction == null && frozen.attackCooldown == cooldown && frozen.save.energy == 70,
                    "Hit stop freezes both a ready blocked press and a cooldown press without aging input or spending energy");
                ActorAction action = AttackBufferAwait(frozen, .02f);
                Check(action != null && action.direction.y < -.999f && action.elapsed == 0,
                    "The saved press resumes once on the unchanged simulation clock after hit stop");
            }
        }

        public static void AttackBufferRejectsInvalidRequestsAndRechecksResources(Campaign data)
        {
            foreach (int style in new[] { 0, 1, 2 })
            {
                GameCore exact = Quiet(data); exact.save.weaponStyles[0] = style;
                exact.save.energy = exact.Profile(0, style).energy; exact.attackCooldown = .08f; exact.PressAttack(new Point(1, 0));
                Check(AttackBufferAwait(exact, .02f) != null && Math.Abs(exact.save.energy - .08f * 14) < .001f,
                    "The exact current profile affordability boundary accepts and pays once for style " + style);
            }
            for (int rejection = 0; rejection < 15; rejection++)
            {
                GameCore game = Quiet(data); game.save.weaponStyles[0] = 1; game.attackCooldown = .08f; game.PressAttack(new Point(0, -1));
                Point request = new Point(1, 0);
                if (rejection == 0) game.attackCooldown = .121f;
                if (rejection == 1) game.save.energy = 3.99f;
                if (rejection == 2) game.attackCooldown = float.NaN;
                if (rejection == 3) game.attackCooldown = float.PositiveInfinity;
                if (rejection == 4) game.attackCooldown = float.NegativeInfinity;
                if (rejection == 5) game.save.energy = float.NaN;
                if (rejection == 6) game.save.energy = float.PositiveInfinity;
                if (rejection == 7) game.save.energy = float.NegativeInfinity;
                if (rejection == 8) request = new Point(float.NaN, 0);
                if (rejection == 9) request = new Point(0, float.PositiveInfinity);
                if (rejection == 10) game.save.health = 0;
                if (rejection == 11) game.save.health = float.NaN;
                if (rejection == 12) game.dashTime = .121f;
                if (rejection == 13) game.playerHitTime = .121f;
                if (rejection == 14) game.mode = "pause";
                game.PressAttack(request);
                Check(game.playerAction == null && game.projectiles.Count == 0 && AttackBufferReleases(game) == 0,
                    "Invalid or too-early requests grant no action, release or payment: " + rejection);
                game.attackCooldown = .08f; game.save.energy = 100; game.save.health = game.save.maxHealth;
                game.dashTime = game.playerHitTime = 0; game.Close(); Advance(game, .8f);
                Check(game.playerAction == null && AttackBufferReleases(game) == 0 && game.save.energy == 100,
                    "Rejected input clears the older basic and cannot revive after cooldown, energy, health or menu recovery: " + rejection);
            }
            for (int change = 0; change < 4; change++)
            {
                GameCore game = Quiet(data); game.save.weaponStyles[0] = 1; game.save.energy = 4; game.attackCooldown = .08f; game.PressAttack(new Point(1, 0));
                if (change == 0) game.save.energy = 0;
                if (change == 1) game.save.energy = float.NaN;
                if (change == 2) game.attackCooldown = float.PositiveInfinity;
                if (change == 3) game.save.health = 0;
                Advance(game, .1f);
                Check(game.playerAction == null && AttackBufferReleases(game) == 0, "Dispatch rechecks live finite resources and affordability: " + change);
                game.save.energy = 100; game.save.health = game.save.maxHealth; game.attackCooldown = 0; Advance(game, .8f);
                Check(game.playerAction == null && AttackBufferReleases(game) == 0, "Failed dispatch consumes the intent instead of waiting for a refill");
            }
        }

        public static void AttackBufferClearsLifecycleProfilesAndTrainingReview(Campaign data)
        {
            Action<GameCore>[] boundaries = {
                g => { g.mode = "pause"; g.Close(); }, g => { g.mode = "inventory"; g.Close(); }, g => { g.mode = "dialog"; g.Close(); },
                g => g.ClearBufferedCombatInputs(), g => g.Enter(0), g => { g.PrepareSave(); g.Load(CloneSave(g.save)); },
                g => g.save = CloneSave(g.save), g => g.save.room = 0, g => { g.NewGame(0); g.Close(); },
                g => g.save.weapon = 0, g => g.save.profession = 2, g => g.save.weaponStyles[1] = 1, g => g.save.technique = 1,
                g => { g.save.flags.Add("weapon_0"); g.Equip(0); },
                g => { g.invulnerable = 0; g.save.health = 1; g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.01f, new Point()); Check(g.Dying, "Actual lethal contact starts recovery"); }
            };
            for (int boundary = 0; boundary < boundaries.Length; boundary++)
            {
                GameCore game = Quiet(data, 1); game.attackCooldown = .08f; game.PressAttack(new Point(0, -1)); boundaries[boundary](game); Advance(game, 1.4f);
                Check(game.playerAction == null && !game.projectiles.Exists(shot => shot.friendly) && AttackBufferReleases(game) == 0,
                    "A discrete basic cannot cross a menu, clear-input, world, save owner, profile or death boundary: " + boundary);
            }
            for (int boundary = 0; boundary < 6; boundary++)
            {
                GameCore game = RangeGame(data, 1);
                if (boundary == 0) { game.attackCooldown = .08f; game.PressAttack(new Point(1, 0)); game.StartPractice(); }
                else
                {
                    Check(game.StartRange(0), "Start the real range before its boundary"); game.attackCooldown = .08f; game.PressAttack(new Point(1, 0));
                    if (boundary == 1) game.ResetRange();
                    if (boundary == 2) game.ChangeRangeLayout(1);
                    if (boundary == 3) game.EndPractice();
                    if (boundary == 4) { game.enemies.Clear(); game.Tick(.02f, new Point()); Check(game.RangeFinished, "Actual range review starts"); }
                    if (boundary == 5) { game.PrepareSave(); game.Load(CloneSave(game.save)); }
                }
                Advance(game, .8f);
                Check(game.playerAction == null && game.projectiles.Count == 0 && AttackBufferReleases(game) == 0,
                    "Practice entry/reset/layout/exit/review/load severs pending basic input: " + boundary);
            }
            for (int training = 0; training < 2; training++)
            {
                GameCore game = RangeGame(data); Check(training == 0 ? game.StartRange(2) : game.StartSparring(), "Start actual safe training");
                game.save.reducedMotion = true; game.save.weaponStyles[0] = 1; Enemy target = game.enemies[0]; target.hp = .1f; target.timer = 9999;
                game.save.x = target.x - 1; game.save.y = target.y;
                int coins = game.save.coins, xp = game.save.xp, kills = game.save.kills[0], mastery = game.save.mastery[0];
                game.Attack(new Point(1, 0)); game.playerAction.elapsed = game.playerAction.contact - .015f;
                game.attackCooldown = .04f; game.PressAttack(new Point(1, 0)); game.Tick(.05f, new Point());
                Check((training == 0 ? game.RangeFinished : game.SparringAwaitingNext) && game.playerAction == null && Math.Abs(game.save.energy - 96.7f) < .001f
                    && game.projectiles.Count == 0 && AttackBufferReleases(game) == 1,
                    "The final real contact cancels the queued paid-style basic before review without a hidden cost or second release");
                game.PressAttack(new Point(1, 0)); game.Attack(new Point(1, 0)); Advance(game, .8f);
                Check(game.playerAction == null && game.save.energy == 100 && game.save.coins == coins && game.save.xp == xp
                    && game.save.kills[0] == kills && game.save.mastery[0] == mastery && AttackBufferReleases(game) == 1,
                    "Range and sparring review stay inert and preserve saved rewards under queued, fresh and held basic input");
            }
        }
    }
}
