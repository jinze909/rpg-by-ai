using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore CompanionFocusGame(Campaign data, int companion = 1, int bond = 3)
        {
            var game = Quiet(data); game.save.flags.Add("companion_" + companion);
            game.save.relationships[companion] = bond; game.SelectCompanion(companion);
            game.save.health = game.save.maxHealth; game.save.energy = 100;
            return game;
        }
        private static ActorAction CompanionFocusWait(GameCore game)
        {
            for (int frame = 0; frame < 750 && game.companionAction == null; frame++) game.Tick(.01f, new Point());
            Check(game.companionAction != null, "Public simulation ticks reach the companion's actual automatic preparation");
            return game.companionAction;
        }
        private static bool CompanionFocusPoint(Point? point, Point expected)
        { return point.HasValue && (point.Value - expected).Length < .0001f; }
        private static void CompanionFocusRelease(GameCore game, ActorAction action)
        {
            for (int frame = 0; frame < 100 && !action.released; frame++) game.Tick(.01f, new Point());
            Check(action.released, "The companion reaches its actual contact through public simulation ticks");
        }

        public static void CompanionFocusFollowsDeliberateLockAndCycleBeforeAutomaticPreparation(Campaign data)
        {
            for (int companion = 0; companion < 3; companion++) foreach (bool reverse in new[] { false, true })
            {
                var game = CompanionFocusGame(data, companion);
                Enemy near = FocusEnemy(game, 98201, 11, 16), first = FocusEnemy(game, 98202, 16, 14), cycled = FocusEnemy(game, 98203, 16, 16);
                if (reverse) game.enemies.Reverse();
                FocusAt(game, first);
                Check(game.CycleCombatFocus() && ReferenceEquals(game.CombatFocusTarget, cycled), "The actual next-focus command selects the requested actor");
                ActorAction action = CompanionFocusWait(game);
                Check(action.kind == "attack" && CompanionFocusPoint(action.target, cycled.Position) &&
                    (action.direction - (cycled.Position - game.companionPosition).Normalized).Length < .0001f,
                    "Every companion joins the player's current deliberate focus instead of its nearer or earlier listed enemy");
                Check(action.weapon == companion && action.combo == 3 && action.damage == 15 && action.contact == .16f && action.duration == .42f &&
                    (float)TacticsField(game, "companionAttack") == (companion == 1 ? 2.8f : 6) && game.save.energy == 100,
                    "Focus preference preserves the original identity, bond damage, windup, duration, interval and automatic cost");
                Check(near.hp == 999 && first.hp == 999 && cycled.hp == 999 && game.projectiles.Count == 0 && !action.released,
                    "Selecting the joint target neither creates early contact nor damages a different enemy");
            }
        }

        public static void CompanionFocusFallbackChoosesNearestThenIdentityAndStableListPosition(Campaign data)
        {
            foreach (bool reverse in new[] { false, true })
            {
                var game = CompanionFocusGame(data); Point foot = game.companionPosition;
                FocusEnemy(game, 98210, foot.x + 4, foot.y); Enemy near = FocusEnemy(game, 98212, foot.x + 1, foot.y);
                FocusEnemy(game, 98211, foot.x + 2, foot.y + 1); if (reverse) game.enemies.Reverse();
                Check(CompanionFocusPoint(CompanionFocusWait(game).target, near.Position), "Unfocused fallback is the nearest eligible actor regardless of list permutation");

                game = CompanionFocusGame(data); foot = game.companionPosition;
                Enemy high = FocusEnemy(game, 98219, foot.x + 2, foot.y), low = FocusEnemy(game, 98218, foot.x - 2, foot.y);
                Check((high.Position - foot).Length == (low.Position - foot).Length, "The identity fixture uses exactly equal represented distances");
                if (reverse) game.enemies.Reverse();
                Check(CompanionFocusPoint(CompanionFocusWait(game).target, low.Position) && high.hp == 999,
                    "Equal-distance actors use the lower stable ID in either list order");

                game = CompanionFocusGame(data); foot = game.companionPosition;
                Enemy a = FocusEnemy(game, 98220, foot.x + 2, foot.y), b = FocusEnemy(game, 98220, foot.x - 2, foot.y);
                Check((a.Position - foot).Length == (b.Position - foot).Length, "The duplicate-ID fixture uses exactly equal represented distances");
                if (reverse) game.enemies.Reverse();
                Check(CompanionFocusPoint(CompanionFocusWait(game).target, reverse ? b.Position : a.Position),
                    "Malformed duplicate IDs retain the first actual list position when distance is also tied");
            }
            var released = CompanionFocusGame(data); Point origin = released.companionPosition;
            Enemy focused = FocusEnemy(released, 98221, 16, 14); FocusAt(released, focused);
            Enemy closest = FocusEnemy(released, 98222, origin.x + 1, origin.y);
            Check(released.ToggleCombatFocus(new Point(1, 0)) && released.CombatFocusTarget == null &&
                CompanionFocusPoint(CompanionFocusWait(released).target, closest.Position), "Deliberately releasing the lock restores nearest fallback on the next automatic decision");
        }

        public static void CompanionFocusChecksOwnFootRangeSightAndFiniteLivingTargets(Campaign data)
        {
            foreach (float distance in new[] { 6.999f, 7f, 7.001f })
            {
                var game = CompanionFocusGame(data); Point foot = game.companionPosition;
                Enemy focused = FocusEnemy(game, 98230, foot.x + distance, foot.y); FocusAt(game, focused);
                Enemy fallback = FocusEnemy(game, 98231, foot.x + 2, foot.y);
                Check(ReferenceEquals(game.CombatFocusTarget, focused), "The player's longer focus envelope retains this actor");
                ActorAction action = CompanionFocusWait(game);
                Check(CompanionFocusPoint(action.target, distance < 7 ? focused.Position : fallback.Position) && ReferenceEquals(game.CombatFocusTarget, focused),
                    "The companion uses its own strict seven-unit attack range without changing the player's still-valid lock");
            }
            var blocked = CompanionFocusGame(data); blocked.save.x = 14.5f; blocked.save.y = 14.5f;
            blocked.companionPosition = blocked.Position + new Point(-1.65f, .55f);
            string[] rows = blocked.Map.rows;
            try
            {
                blocked.Map.rows = (string[])rows.Clone(); char[] wall = blocked.Map.rows[16].ToCharArray(); wall[13] = '#'; blocked.Map.rows[16] = new string(wall);
                Enemy focus = FocusEnemy(blocked, 98232, 15.5f, 18.5f); FocusAt(blocked, focus);
                Enemy fallback = FocusEnemy(blocked, 98233, blocked.companionPosition.x, blocked.companionPosition.y - 1);
                Check(blocked.CanStand(blocked.Position) && blocked.CanStand(blocked.companionPosition) && blocked.CanStand(focus.Position),
                    "The sight fixture keeps all three feet on real ground");
                Check(CompanionFocusPoint(CompanionFocusWait(blocked).target, fallback.Position) && ReferenceEquals(blocked.CombatFocusTarget, focus),
                    "A wall blocking only the companion's lane forces fallback while the player's visible focus stays selected");
            }
            finally { blocked.Map.rows = rows; }

            foreach (float invalidHealth in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var game = CompanionFocusGame(data); Point foot = game.companionPosition;
                Enemy invalid = FocusEnemy(game, 98234, foot.x + .7f, foot.y); FocusAt(game, invalid); invalid.hp = invalidHealth;
                Enemy valid = FocusEnemy(game, 98235, foot.x + 2, foot.y);
                Check(CompanionFocusPoint(CompanionFocusWait(game).target, valid.Position) && game.CombatFocusTarget == null,
                    "Dead or nonfinite health cannot be an automatic focused or fallback target");
            }
            var malformed = CompanionFocusGame(data); Point start = malformed.companionPosition;
            FocusEnemy(malformed, 98236, float.NaN, start.y); FocusEnemy(malformed, 98237, start.x, float.PositiveInfinity);
            Enemy living = FocusEnemy(malformed, 98238, start.x + 2, start.y);
            Check(CompanionFocusPoint(CompanionFocusWait(malformed).target, living.Position), "Nonfinite coordinates never enter nearest or sight calculations as attackable bodies");
            var empty = CompanionFocusGame(data); FocusEnemy(empty, 98239, empty.companionPosition.x + 7, empty.companionPosition.y);
            Advance(empty, 1.1f); Check(empty.companionAction == null && empty.projectiles.Count == 0, "No eligible actor means no invented automatic attack");
        }

        public static void CompanionFocusKeepsAcceptedAimAndStraightFlightAcrossLaterCycles(Campaign data)
        {
            for (int companion = 0; companion < 3; companion++)
            {
                var game = CompanionFocusGame(data, companion); FocusEnemy(game, 98240, 11, 16);
                Enemy original = FocusEnemy(game, 98241, 16, 14), other = FocusEnemy(game, 98242, 12, 18); FocusAt(game, original);
                ActorAction action = CompanionFocusWait(game); Point captured = original.Position, facing = action.direction, foot = game.companionPosition;
                game.Tick(.05f, new Point()); float elapsed = action.elapsed;
                Check(game.CycleCombatFocus() && ReferenceEquals(game.CombatFocusTarget, other) && ReferenceEquals(game.companionAction, action) &&
                    action.elapsed == elapsed && CompanionFocusPoint(action.target, captured) && (action.direction - facing).Length == 0,
                    "A later live focus cycle cannot replace or retarget the accepted automatic preparation");
                original.y = 13.5f; CompanionFocusRelease(game, action);
                Check(game.projectiles.Count == 1 && (game.companionPosition - foot).Length == 0, "The original automatic contact releases one missile from planted feet");
                Projectile shot = game.projectiles[0]; Point origin = GameCore.WeaponOrigin(foot, companion == 1 ? 1 : 2, facing);
                Check((shot.velocity.Normalized - (captured - origin).Normalized).Length < .0001f && TacticsNear(shot.velocity.Length, 9) &&
                    shot.damage == 15 && shot.explosive == (companion == 2), "Released arrows and spells retain captured aim, original speed, bond damage and explosive identity");
                Point velocity = shot.velocity, position = shot.position;
                Check(game.CycleCombatFocus(-1) && ReferenceEquals(game.CombatFocusTarget, original), "An explicit reverse cycle also works during actual flight");
                game.Tick(.02f, new Point());
                Check(game.projectiles.Contains(shot) && (shot.velocity - velocity).Length == 0 && (shot.position - position - velocity * .02f).Length < .0001f,
                    "Existing automatic missiles remain straight after a second focus change and target motion");
            }
        }

        public static void CompanionFocusPreservesMiraHealingPriorityAndExplicitCommands(Campaign data)
        {
            foreach (bool below in new[] { true, false })
            {
                var game = CompanionFocusGame(data, 0); Enemy enemy = FocusEnemy(game, 98250, 16, 14); FocusAt(game, enemy);
                game.save.health = game.save.maxHealth * (below ? .799f : .8f); float health = game.save.health;
                ActorAction action = CompanionFocusWait(game);
                Check(action.kind == (below ? "heal" : "attack") && (below ? !action.target.HasValue : CompanionFocusPoint(action.target, enemy.Position)),
                    "Mira still heals before joining focus strictly below eighty percent health, and attacks at the threshold");
                CompanionFocusRelease(game, action);
                Check(below ? TacticsNear(game.save.health - health, 9) && game.projectiles.Count == 0 : game.save.health == health && game.projectiles.Count == 1,
                    "Mira's actual automatic contact keeps the original six-plus-bond healing or original offensive release");
            }
            for (int companion = 0; companion < 3; companion++)
            {
                var game = CompanionFocusGame(data, companion); Enemy first = FocusEnemy(game, 98251, 16, 14), next = FocusEnemy(game, 98252, 12, 18); FocusAt(game, first);
                game.Tick(.01f, new Point()); game.save.health = 20; game.save.energy = 20;
                game.CommandCompanion(); ActorAction action = game.companionAction;
                Check(action != null && action.kind == "command" && action.contact == .2f && action.duration == .52f &&
                    game.companionCooldown == 18 && CompanionFocusPoint(action.target, first.Position), "The explicit command still owns its original prepared target, timing and cooldown");
                Check(game.CycleCombatFocus() && ReferenceEquals(game.CombatFocusTarget, next), "A focus change remains available while an explicit command is prepared");
                CompanionFocusRelease(game, action); CompanionCommandReceipt receipt = game.LastCompanionReceipt;
                Check(receipt != null && receipt.id == companion && receipt.bond == 3 && CompanionFocusPoint(action.target, first.Position),
                    "Automatic collaboration cannot replace, retarget or duplicate the original explicit command receipt");
                if (companion == 0) Check(receipt.actualHeal == 27 && game.guardTime == 3 && game.projectiles.Count == 0, "Mira's command retains its original heal and guard");
                if (companion == 1) Check(receipt.arrowCount == 3 && game.projectiles.Count == 3, "Ivo's command retains its original three-arrow volley");
                if (companion == 2) Check(game.projectiles.Count == 0 && game.effects.Exists(effect => effect.kind == "frost"), "Sen's command retains its original frost field");
            }
        }

        public static void CompanionFocusKeepsPauseReadOnlySavesAndWorldOwnershipTransient(Campaign data)
        {
            var paused = CompanionFocusGame(data); Enemy locked = FocusEnemy(paused, 98260, 16, 14); FocusAt(paused, locked);
            ActorAction accepted = CompanionFocusWait(paused); Point target = accepted.target.Value;
            float elapsed = accepted.elapsed, clock = paused.save.totalTime, timer = (float)TacticsField(paused, "companionAttack");
            paused.mode = "pause"; paused.Tick(.05f, new Point());
            Check(paused.CombatFocusTarget == null && ReferenceEquals(paused.companionAction, accepted) && accepted.elapsed == elapsed &&
                paused.save.totalTime == clock && (float)TacticsField(paused, "companionAttack") == timer && paused.projectiles.Count == 0,
                "Pausing freezes the real automatic preparation and timer and exposes no usable live lock");
            paused.Close(); CompanionFocusRelease(paused, accepted);
            Check(CompanionFocusPoint(accepted.target, target) && paused.projectiles.Count == 1, "Closing pause preserves the already accepted automatic aim and releases it once");

            foreach (int version in new[] { 1, 2 })
            {
                var game = CompanionFocusGame(data); Enemy focus = FocusEnemy(game, 98261, 16, 14); FocusAt(game, focus);
                FocusEnemy(game, 98262, 12, 16); game.PrepareSave(); string saved = TacticsState(game.save);
                for (int read = 0; read < 20; read++) Check(ReferenceEquals(game.CombatFocusTarget, focus) && game.CanCycleCombatFocus, "Live focus diagnostics observe the same current actors");
                Check(game.CycleCombatFocus() && game.CycleCombatFocus(-1) && TacticsState(game.save) == saved,
                    "Collaboration selection and repeated diagnostics mutate no persisted field or progression");
                SaveData copy = CloneSave(game.save); copy.version = version;
                var loaded = new GameCore(data, copy); loaded.Start(); foreach (Enemy enemy in loaded.enemies) enemy.stun = 99;
                Check(loaded.CombatFocusTarget == null && loaded.companionAction == null && loaded.projectiles.Count == 0 && loaded.save.companion == (version == 1 ? -1 : 1) &&
                    loaded.save.relationships[1] == 3 && loaded.save.version == 2, "Both save versions retain companion progression without carrying focus, preparations or flight");
                if (version == 1)
                {
                    Advance(loaded, 2.1f);
                    Check(loaded.companionAction == null && loaded.Has("companion_1"), "The original version-one migration still requires deliberately selecting an earned companion");
                }
                else
                {
                    Enemy nearest = loaded.enemies.Find(enemy => enemy.id == 98262);
                    Check(nearest != null && CompanionFocusPoint(CompanionFocusWait(loaded).target, nearest.Position), "A restored companion makes a fresh nearest decision instead of inheriting the saved session's old lock");
                }
                game.Enter(game.save.room);
                Check(game.CombatFocusTarget == null && game.companionAction == null, "Same-room world recreation also clears transient lock and preparation ownership");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("combatFocus", StringComparison.OrdinalIgnoreCase) < 0 &&
                    field.Name.IndexOf("companionFocus", StringComparison.OrdinalIgnoreCase) < 0 && field.Name != "companionAction",
                    "Companion collaboration adds no lock or action field to the existing save format");
        }

        public static void CompanionFocusIdleReadinessRespondsWhenWalkingIntoRange(Campaign data)
        {
            for (int companion = 0; companion < 3; companion++)
            {
                var game = CompanionFocusGame(data, companion);
                Enemy target = FocusEnemy(game, 98270 + companion, 18, 14);
                Point originalTarget = target.Position;
                Check((target.Position - game.companionPosition).Length > 7 && game.CanStand(target.Position),
                    "The unchanged living target starts beyond the companion's own range on the shipped map");
                Advance(game, 1.1f);
                Check(game.companionAction == null && game.projectiles.Count == 0 && game.save.health == game.save.maxHealth && game.companionCooldown == 0,
                    "An empty initial support scan neither prepares an attack nor spends health, a shot or a manual command");
                int eligibleFrame = -1, frame;
                for (frame = 1; frame <= 160 && game.companionAction == null; frame++)
                {
                    game.Tick(.01f, new Point(1, 0));
                    if ((target.Position - game.companionPosition).Length < 7 && eligibleFrame < 0) eligibleFrame = frame;
                }
                ActorAction action = game.companionAction;
                Check((target.Position - originalTarget).Length == 0 && game.save.x > 12 && eligibleFrame > 0,
                    "Only real player and follower walking brings the original unmoved enemy into range");
                Check(action != null && frame - 1 - eligibleFrame <= 1,
                    "Already-ready support starts when walking reveals a target instead of losing another whole interval to the prior empty scan");
                Check(action.kind == "attack" && action.weapon == companion && action.combo == 3 && action.damage == 15 &&
                    action.elapsed == 0 && !action.released && action.contact == .16f && action.duration == .42f &&
                    CompanionFocusPoint(action.target, originalTarget) && (float)TacticsField(game, "companionAttack") == (companion == 1 ? 2.8f : 6),
                    "The first real preparation captures the unchanged target, bond damage and full authored contact, duration and support interval");
                Point direction = action.direction, foot = game.companionPosition; ImpactProfile impact = action.impact;
                game.save.relationships[companion] = 5; game.SetAim(new Point(-1, 0));
                for (int i = 0; i < 15; i++) game.Tick(.01f, new Point());
                Check(!action.released && game.projectiles.Count == 0 && action.damage == 15 && action.combo == 3 &&
                    ReferenceEquals(action.impact, impact) && (action.direction - direction).Length == 0 && CompanionFocusPoint(action.target, originalTarget),
                    "New readiness adds no early contact or later aim, bond or damage recapture");
                game.Tick(.01f, new Point());
                Check(action.released && game.projectiles.Count == 1 && game.projectiles[0].damage == 15 &&
                    ReferenceEquals(game.projectiles[0].impact, impact) && (game.companionPosition - foot).Length == 0,
                    "The same planted action releases exactly one original-damage missile at the original .16-second contact");
                for (int i = 0; i < 25; i++) game.Tick(.01f, new Point());
                Check(ReferenceEquals(game.companionAction, action), "The original action still owns its .41-second recovery");
                game.Tick(.01f, new Point());
                // Repeated .01 additions may remain one represented fraction
                // below .42. Advance only that exact remainder, never a later
                // whole frame or a change to the authored recovery duration.
                if (ReferenceEquals(game.companionAction, action) && action.elapsed < action.duration)
                    game.Tick(action.duration - action.elapsed, new Point());
                Check(game.companionAction == null && action.elapsed == action.duration,
                    "The original automatic action ends at its exact represented .42-second boundary");
                game.projectiles.Clear();
                int intervalFrames = companion == 1 ? 280 : 600;
                for (int i = 42; i < intervalFrames - 2; i++) game.Tick(.01f, new Point());
                Check(game.companionAction == null, "A genuine support attempt still prevents a second preparation before its full original interval");
                for (int i = 0; i < 4 && game.companionAction == null; i++) game.Tick(.01f, new Point());
                Check(game.companionAction != null && !ReferenceEquals(game.companionAction, action) && game.companionAction.elapsed == 0 &&
                    (float)TacticsField(game, "companionAttack") == (companion == 1 ? 2.8f : 6),
                    "Continuous valid targets retain the authored 2.8/6-second preparation cadence after the idle fix");
            }
        }

        public static void CompanionFocusIdleReadinessKeepsHealingCadenceAndWorldBoundaries(Campaign data)
        {
            var mira = CompanionFocusGame(data, 0); Advance(mira, 1.1f);
            mira.invulnerable = 0; float health = mira.save.health;
            mira.projectiles.Add(new Projectile { position = mira.Position, damage = 30, source = "shot" });
            mira.mode = "pause"; float clock = mira.save.totalTime, timer = (float)TacticsField(mira, "companionAttack");
            for (int i = 0; i < 10; i++) mira.Tick(.05f, new Point());
            Check(mira.save.health == health && mira.save.totalTime == clock && (float)TacticsField(mira, "companionAttack") == timer && mira.companionAction == null,
                "Idle support and a waiting genuine projectile remain frozen in a menu");
            mira.Close(); mira.Tick(.01f, new Point()); ActorAction heal = mira.companionAction;
            Check(mira.save.health == health - 30 && heal != null && heal.kind == "heal" && !heal.released && heal.elapsed == 0,
                "A real incoming projectile drops Mira's hero below eighty percent and already-ready support prepares healing in that simulation tick");
            Check(heal.contact == .16f && heal.duration == .42f && heal.combo == 3 && (float)TacticsField(mira, "companionAttack") == 6 && mira.companionCooldown == 0,
                "Responsive healing retains the original automatic contact, duration, bond and six-second interval without spending the manual cooldown");
            for (int i = 0; i < 15; i++) mira.Tick(.01f, new Point());
            Check(!heal.released && mira.save.health == health - 30, "Readiness never heals before the real .16-second contact");
            mira.Tick(.01f, new Point());
            Check(heal.released && mira.save.health == health - 21 && mira.effects.Exists(effect => effect.kind == "healthGain" && effect.text == "+9"),
                "Actual contact keeps the six-plus-bond heal and truthful actual-healing feedback");

            for (int companion = 0; companion < 3; companion++)
            {
                var manual = CompanionFocusGame(data, companion); Advance(manual, 1.1f);
                Enemy target = FocusEnemy(manual, 98280 + companion, 16, 14); FocusAt(manual, target);
                manual.CommandCompanion(); ActorAction command = manual.companionAction;
                Check(command != null && command.kind == "command" && command.contact == .2f && command.duration == .52f && manual.companionCooldown == 18,
                    "An explicit command still takes priority over ready automatic support before the simulation tick");
                for (int i = 0; i < 19; i++) manual.Tick(.01f, new Point());
                Check(ReferenceEquals(manual.companionAction, command) && !command.released && manual.LastCompanionReceipt == null,
                    "Idle readiness cannot replace or prematurely release a manual command");
                manual.Tick(.01f, new Point());
                Check(command.released && ReferenceEquals(manual.companionAction, command) && manual.LastCompanionReceipt != null && manual.LastCompanionReceipt.id == companion,
                    "The original manual command owns its .2-second release and receipt");
            }
            for (int boundary = 0; boundary < 3; boundary++)
            {
                var game = CompanionFocusGame(data); Advance(game, 1.1f);
                if (boundary == 0) game.SelectCompanion(1);
                else if (boundary == 1) game.Enter(game.save.room);
                else { SaveData saved = CloneSave(game.save); game.Load(saved); game.Close(); }
                game.enemies.Clear(); game.save.x = 12; game.save.y = 14; game.companionPosition = game.Position + new Point(-1.65f, .55f);
                FocusEnemy(game, 98290 + boundary, 16, 14);
                float restart = boundary == 0 ? 1 : 2;
                Check(game.companionAction == null && (float)TacticsField(game, "companionAttack") == restart,
                    "Reselection, world recreation and save loading retain their original fresh one/two-second readiness boundary");
                Advance(game, restart - .1f);
                Check(game.companionAction == null, "Idle readiness cannot leak across a fresh companion or world boundary and skip its startup delay");
                ActorAction fresh = CompanionFocusWait(game);
                Check(fresh.kind == "attack" && fresh.elapsed == 0 && (float)TacticsField(game, "companionAttack") == 2.8f,
                    "The fresh world eventually prepares one new attack with the original full interval");
            }
        }
    }
}
