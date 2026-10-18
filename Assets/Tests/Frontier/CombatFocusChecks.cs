using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static Enemy FocusEnemy(GameCore game, int id, float x, float y, float health = 999)
        {
            var enemy = new Enemy(id, 0, x, y, health) { stun = 99 };
            game.enemies.Add(enemy); return enemy;
        }
        private static void FocusAt(GameCore game, Enemy enemy)
        {
            game.ClearCombatFocus();
            Check(game.ToggleCombatFocus(enemy.Position - game.Position) && ReferenceEquals(game.CombatFocusTarget, enemy),
                "A deliberate focus request selects the actual current actor");
        }
        private static void FocusRelease(GameCore game, ActorAction action)
        {
            for (int frame = 0; frame < 100 && !action.released; frame++) game.Tick(.01f, new Point(), new Point(-1, 0));
            Check(action.released, "The accepted focused action reaches its actual production contact");
        }

        public static void CombatFocusSelectionWeightsAimDistanceAndStableActorIdentity(Campaign data)
        {
            var game = Quiet(data);
            Enemy nearSide = FocusEnemy(game, 9601, 12, 16), ahead = FocusEnemy(game, 9602, 20, 14);
            Check(game.CanAcquireCombatFocus && game.ToggleCombatFocus(new Point(1, 0)) && ReferenceEquals(game.CombatFocusTarget, ahead),
                "A farther enemy along the requested aim beats a closer enemy to the side");
            Check(game.ToggleCombatFocus(new Point(0, 1)) && game.CombatFocusTarget == null,
                "Pressing focus while locked releases it rather than silently cycling to another actor");
            Check(game.ToggleCombatFocus(new Point(0, 1)) && ReferenceEquals(game.CombatFocusTarget, nearSide),
                "The next deliberate acquisition honors its new directional intent");
            game.ClearCombatFocus(); Enemy nearerAhead = FocusEnemy(game, 9603, 15, 14);
            Check(game.ToggleCombatFocus(new Point(1, 0)) && ReferenceEquals(game.CombatFocusTarget, nearerAhead),
                "Distance distinguishes candidates on the same aim ray");
            game.enemies.Clear(); game.ClearCombatFocus();
            Enemy highId = FocusEnemy(game, 9619, 16, 14), lowId = FocusEnemy(game, 9610, 16, 14);
            Check(game.ToggleCombatFocus(new Point(1, 0)) && ReferenceEquals(game.CombatFocusTarget, lowId),
                "Equal aim and distance resolve by stable identity rather than insertion order");
            game.enemies.Reverse(); game.ClearCombatFocus();
            Check(game.ToggleCombatFocus(new Point(1, 0)) && ReferenceEquals(game.CombatFocusTarget, lowId),
                "Reordering the current actor list leaves a tied selection deterministic");
            foreach (Point invalid in new[] { new Point(), new Point(float.NaN, 1), new Point(1, float.PositiveInfinity) })
            {
                game.ClearCombatFocus(); game.SetAim(new Point(1, 0));
                Check(game.ToggleCombatFocus(invalid) && ReferenceEquals(game.CombatFocusTarget, lowId),
                    "Absent or nonfinite request directions safely use the existing finite aim");
            }
            game.enemies.Remove(lowId);
            Enemy replacement = FocusEnemy(game, lowId.id, lowId.x, lowId.y);
            Check(game.CombatFocusTarget == null && !ReferenceEquals(replacement, lowId),
                "A copied or reused ID cannot transfer a lock away from its canonical live reference");
            game.Tick(.01f, new Point()); game.enemies.Add(lowId);
            Check(game.CombatFocusTarget == null && highId.hp == 999,
                "After invalidation even reinserting the old reference does not automatically reacquire it");
            var manual = Quiet(data, 1); FocusAt(manual, FocusEnemy(manual, 9611, 17, 14)); manual.Tick(.01f, new Point());
            Check(manual.aimTarget.HasValue && manual.ToggleCombatFocus(new Point(1, 0)) && !manual.aimTarget.HasValue,
                "Deliberately releasing focus relinquishes its old aim point before the next action");
            manual.Attack(new Point(-1, 0)); ActorAction unlocked = manual.playerAction;
            Check(unlocked != null && unlocked.direction.x < -.999f && !unlocked.target.HasValue,
                "The next unlocked attack immediately captures manual direction without a stale focus point");
            FocusRelease(manual, unlocked);
            Check(manual.projectiles.Count == 1 && manual.projectiles[0].velocity.x < 0 && Math.Abs(manual.projectiles[0].velocity.y) < .001f,
                "The actual unlocked arrow flies along manual aim instead of returning to the old focused body");
            var foreignSave = Quiet(data); FocusAt(foreignSave, FocusEnemy(foreignSave, 9612, 16, 14));
            foreignSave.save = CloneSave(foreignSave.save);
            Check(foreignSave.CombatFocusTarget == null,
                "Even the same actor list and coordinates cannot carry focus into a different SaveData owner");
            foreach (bool skill in new[] { false, true })
            {
                var lost = Quiet(data, 1); Enemy original = FocusEnemy(lost, 9613, 17, 14); FocusAt(lost, original);
                lost.Tick(.01f, new Point()); lost.enemies.Remove(original); FocusEnemy(lost, original.id, 17, 14);
                if (skill) lost.Skill(new Point(-1, 0)); else lost.Attack(new Point(-1, 0));
                Check(lost.playerAction != null && lost.playerAction.direction.x < -.999f && !lost.playerAction.target.HasValue && lost.CombatFocusTarget == null,
                    "A direct basic or Q after actor removal uses manual aim immediately, without a Tick or a stale point targeting the reused ID");
            }
        }

        public static void CombatFocusRangeSightAndHealthLossRequireDeliberateReacquisition(Campaign data)
        {
            var game = Quiet(data); Enemy edge = FocusEnemy(game, 9620, 24, 14);
            Check(GameCore.CombatFocusRange == 12, "The optional focus envelope is twelve world units");
            FocusAt(game, edge); edge.x = 24.001f;
            Check(game.CombatFocusTarget == null && !game.CanAcquireCombatFocus,
                "The twelve-unit boundary is inclusive and immediately rejects an actor beyond it");
            game.Tick(.01f, new Point()); edge.x = 24;
            Check(game.CombatFocusTarget == null && game.CanAcquireCombatFocus,
                "Returning to range advertises a candidate without restoring a lost lock");
            foreach (float health in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                edge.hp = 999; FocusAt(game, edge); edge.hp = health;
                Check(game.CombatFocusTarget == null && !game.CanAcquireCombatFocus,
                    "Dead and nonfinite health cannot remain focused or become a fresh candidate");
                game.Tick(.01f, new Point()); edge.hp = 999;
                Check(game.CombatFocusTarget == null, "Restoring health never invents another focus input");
            }
            foreach (float coordinate in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                edge.x = 24; FocusAt(game, edge); edge.x = coordinate;
                Check(game.CombatFocusTarget == null && !game.CanAcquireCombatFocus,
                    "Malformed actor coordinates cannot enter range or sight calculations as a valid candidate");
                game.Tick(.01f, new Point());
            }
            game.enemies.Clear(); game.save.x = 8.5f; game.save.y = 9.5f;
            Enemy visible = FocusEnemy(game, 9621, 8.5f, 12.5f), behindWall = FocusEnemy(game, 9622, 11.5f, 9.5f);
            Check(game.CanStand(game.Position) && game.CanStand(behindWall.Position) && game.Blocked(9.5f, 9.5f),
                "The sight fixture places both bodies on real ground on opposite sides of an authored wall");
            Check(game.ToggleCombatFocus(new Point(1, 0)) && ReferenceEquals(game.CombatFocusTarget, visible),
                "The best aimed enemy behind an intervening wall loses to a visible candidate");
            visible.x = 11.5f; visible.y = 9.5f;
            Check(game.CombatFocusTarget == null && !game.CanAcquireCombatFocus,
                "An intervening wall immediately removes focus even when the actor remains alive and nearby");
            game.Tick(.01f, new Point()); visible.x = 8.5f; visible.y = 12.5f;
            Check(game.CombatFocusTarget == null && game.CanAcquireCombatFocus,
                "Regaining sight leaves selection to the player");
            FocusAt(game, visible); visible.hp = 0; game.Tick(.01f, new Point());
            behindWall.x = 8.5f; behindWall.y = 11.5f;
            Check(game.CombatFocusTarget == null && game.CanAcquireCombatFocus,
                "Losing one target never automatically switches to another eligible living body");
        }

        public static void CombatFocusBackpedalingAndDodgeKeepRealMovementIndependent(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var game = Quiet(data, profession); Enemy target = FocusEnemy(game, 9630 + profession, 17, 14); FocusAt(game, target);
                Point before = game.Position; game.Tick(.05f, new Point(-1, 0), new Point(0, -1));
                Check(Math.Abs(game.Position.x - before.x + game.Speed * .05f) < .0001f && game.Position.y == before.y &&
                    game.locomotionFacing.x < -.999f && game.aim.x > .999f && game.aimTarget.HasValue && (game.aimTarget.Value - target.Position).Length < .0001f,
                    "Focused retreat keeps full actual walking speed and backward gait while combat aim follows the enemy");
                target.x = 16; target.y = 16; game.Tick(.02f, new Point(-1, 0), new Point(0, -1));
                Point expected = (target.Position - (game.Position + new Point(game.Speed * .02f, 0))).Normalized;
                Check((game.aim - expected).Length < .0001f && game.locomotionFacing.x < -.999f,
                    "A moving target updates independent aim without turning the real retreat displacement");
                before = game.Position; float energy = game.save.energy; game.Dash(new Point(-1, 0));
                Check(game.dashTime == .18f && game.dashCooldown == .75f && game.invulnerable == .32f && game.save.energy == energy - 20,
                    "Focus leaves the existing dodge duration, cooldown, immunity and one-time energy cost intact");
                game.Tick(.05f, new Point(-1, 0), new Point(0, -1));
                Check(Math.Abs(game.Position.x - before.x + .75f) < .0001f && game.Position.y == before.y && game.VisualFacing.x < -.999f &&
                    game.aim.x > 0 && game.aim.y > 0 && ReferenceEquals(game.CombatFocusTarget, target),
                    "The real dodge still travels and faces its captured retreat direction while focus remains independently aimed");
            }
        }

        public static void CombatFocusActionsCaptureLiveTargetsWithoutChangingCostsOrContacts(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) foreach (bool skill in new[] { false, true })
            {
                var game = Quiet(data, profession); game.save.flags.Add("core_1"); game.SetStyle(1);
                Enemy target = FocusEnemy(game, 9640 + profession, profession == 0 ? 13.3f : 16, 14); FocusAt(game, target);
                Point captured = target.Position; WeaponProfile profile = game.Profile(game.save.weapon, game.Style);
                if (skill) game.Skill(new Point(-1, 0)); else game.Attack(new Point(-1, 0));
                ActorAction action = game.playerAction;
                Check(action != null && action.kind == (skill ? "skill" : "attack") && action.direction.x > .999f &&
                    action.target.HasValue && (action.target.Value - captured).Length < .0001f && !action.released && target.hp == 999 && game.projectiles.Count == 0,
                    "An accepted action resolves live focus immediately, then prepares a captured direction and target without an early contact");
                Check(game.save.energy == (skill ? 60 : 96) && (skill ? game.skillCooldown == 3.5f : Math.Abs(game.attackCooldown - profile.interval) < .0001f),
                    "Focused Q and paid-style basics retain their actual costs and cooldowns");
                if (skill) game.Skill(new Point(0, -1)); else game.Attack(new Point(0, -1));
                Check(ReferenceEquals(game.playerAction, action) && game.save.energy == (skill ? 60 : 96),
                    "Rejected repeated input neither replaces the preparation nor spends a second cost");
                // Move the live lock away while keeping another body at the prepared contact.
                Enemy atCapture = FocusEnemy(game, 9650 + profession, captured.x, captured.y);
                target.x = 12; target.y = 17;
                game.Tick(.01f, new Point(), new Point(-1, 0));
                Check(game.aim.y > .999f && action.direction.x > .999f && (action.target.Value - captured).Length < .0001f,
                    "Live focus can turn during preparation without rewriting the pending action's original aim or target");
                FocusRelease(game, action);
                if (profession == 0 || skill && profession == 2)
                    Check(atCapture.hp < 999, "The prepared sword or radial technique resolves its real accepted contact");
                else
                {
                    Check(game.projectiles.Count > 0, "The prepared ranged contact launches actual missiles");
                    for (int frame = 0; frame < 150 && atCapture.hp == 999; frame++) game.Tick(.01f, new Point());
                    Check(atCapture.hp < 999, "The released attack reaches the captured point while its old focus body is elsewhere");
                }
                if (!skill) Check(target.hp == 999, "A moving focused body receives no homing or redirected basic contact");
            }
        }

        public static void CombatFocusQueuedSkillsAndFlyingMissilesRetainOriginalAim(Campaign data)
        {
            foreach (int profession in new[] { 1, 2 })
            {
                var game = Quiet(data, profession); Enemy target = FocusEnemy(game, 9660 + profession, 19, 14); FocusAt(game, target);
                game.Attack(new Point(-1, 0)); ActorAction basic = game.playerAction; Point captured = target.Position;
                target.x = 12; target.y = 18; FocusRelease(game, basic);
                Check(game.projectiles.Count == 1, "Each original ranged basic has one real released missile");
                Projectile shot = game.projectiles[0]; Point velocity = shot.velocity, position = shot.position;
                Point origin = GameCore.WeaponOrigin(game.Position, profession, basic.direction);
                Check((velocity.Normalized - (captured - origin).Normalized).Length < .0001f,
                    "The released missile aims from the authored muzzle to the action's captured point");
                target.x = 10; target.y = 14; game.Tick(.05f, new Point());
                Check(game.projectiles.Contains(shot) && (shot.velocity - velocity).Length == 0 && (shot.position - position - velocity * .05f).Length < .0001f,
                    "A launched arrow or starfire keeps straight velocity as the currently locked actor changes sides");
                Advance(game, 1.1f); Check(target.hp == 999,
                    "Straight flight does not bend back to damage the moving focus target");
            }
            for (int profession = 0; profession < 3; profession++)
            {
                var game = Quiet(data, profession); Enemy target = FocusEnemy(game, 9670 + profession, 16, 14); FocusAt(game, target);
                game.Attack(new Point(-1, 0)); ActorAction basic = game.playerAction; Advance(game, basic.contact - .04f);
                target.x = 12; target.y = 17; Point captured = target.Position, facing = (captured - game.Position).Normalized;
                game.Skill(new Point(-1, 0));
                Check(ReferenceEquals(game.playerAction, basic) && game.save.energy == 100 && game.skillCooldown <= 0,
                    "Focused Q during an actual preceding windup queues without cancelling contact or spending early");
                target.x = 16; target.y = 14;
                for (int frame = 0; frame < 100 && ReferenceEquals(game.playerAction, basic); frame++) game.Tick(.01f, new Point());
                ActorAction queued = game.playerAction;
                Check(queued != null && queued.kind == "skill" && basic.released && (queued.direction - facing).Length < .0001f &&
                    queued.target.HasValue && (queued.target.Value - captured).Length < .0001f && game.save.energy == 60 && game.skillCooldown == 3.5f,
                    "The real buffered Q starts once after the preceding contact, retaining the focus position captured at the input, profession " + profession);
                FocusRelease(game, queued);
                if (profession == 1)
                {
                    var volley = game.projectiles.FindAll(p => p.technique);
                    Point expected = (captured - GameCore.WeaponOrigin(game.Position, 1, queued.direction)).Normalized;
                    Check(volley.Count == 5 && (volley[2].velocity.Normalized - expected).Length < .0001f,
                        "All five real Q arrows release around the queued focus point, not the target's later position");
                }
            }
        }

        public static void CombatFocusMenusWorldDeathAndTrainingClearTransientOwnership(Campaign data)
        {
            foreach (string mode in new[] { "pause", "inventory", "journal", "dialog", "controls", "help", "plans", "fish", "title" })
            {
                var game = Quiet(data); Enemy target = FocusEnemy(game, 9680, 16, 14); FocusAt(game, target); game.mode = mode;
                Check(game.CombatFocusTarget == null && !game.CanAcquireCombatFocus && !game.ToggleCombatFocus(new Point(1, 0)),
                    "A paused or unrelated mode clears focus and rejects stale focus callbacks: " + mode);
                game.Close(); Check(game.CombatFocusTarget == null && game.CanAcquireCombatFocus,
                    "Closing a menu does not secretly restore the previous lock");
            }
            foreach (int boundary in new[] { 0, 1, 2, 3 })
            {
                var game = Quiet(data); Enemy target = FocusEnemy(game, 9681, 16, 14); FocusAt(game, target);
                if (boundary == 0) game.Enter(game.save.room);
                if (boundary == 1) { game.PrepareSave(); SaveData sameSave = game.save; game.Load(sameSave); game.Start(); }
                if (boundary == 2) { game.PrepareSave(); game.Load(CloneSave(game.save)); game.Start(); }
                if (boundary == 3) { game.NewGame(2); game.Close(); }
                Check(game.CombatFocusTarget == null, "Same-room travel, same-instance load, copied load and a new journey clear the lock: " + boundary);
                game.enemies.Clear(); game.enemies.Add(target);
                Check(game.CombatFocusTarget == null, "Restoring an old actor reference cannot cross a world reset");
            }
            var death = Quiet(data); FocusAt(death, FocusEnemy(death, 9682, 16, 14));
            death.save.health = 1; death.invulnerable = 0;
            death.projectiles.Add(new Projectile { position = death.Position, damage = 999, source = "shot" }); death.Tick(.01f, new Point());
            Check(death.Dying && death.CombatFocusTarget == null && !death.ToggleCombatFocus(new Point(1, 0)),
                "An actual lethal contact clears focus immediately and the death pose cannot accept another lock");
            Advance(death, 1.1f); Check(!death.Dying && death.CombatFocusTarget == null && death.save.room == 0,
                "Recovery at the inn retains no combat focus");
            var practice = Fresh(data); FocusAt(practice, FocusEnemy(practice, 9683, practice.Position.x + 1, practice.Position.y));
            practice.StartPractice(); Check(practice.practicing && practice.CombatFocusTarget == null, "Entering actual basic practice clears a world lock");
            Enemy dummy = practice.enemies[0]; FocusAt(practice, dummy); practice.Attack(new Point(-1, 0)); Advance(practice, .5f);
            Check(practice.practiceStage == 1 && practice.CombatFocusTarget == null && practice.enemies[0].id == dummy.id && !ReferenceEquals(practice.enemies[0], dummy),
                "The real lesson transition reuses the dummy ID without inheriting the old actor's focus");
            practice.EndPractice(); Check(practice.CombatFocusTarget == null, "Leaving actual basic practice clears its lock");
            var range = RangeGame(data, 1); Check(range.StartRange(0), "Start the existing safe range through its local public callback");
            Enemy first = range.enemies[0]; FocusAt(range, first);
            Check(range.ResetRange() && range.CombatFocusTarget == null && range.enemies[0].id == first.id && !ReferenceEquals(first, range.enemies[0]),
                "A manual range reset creates new reserved actors and clears their predecessor's focus");
            FocusAt(range, range.enemies[0]); Check(range.ChangeRangeLayout(1) && range.CombatFocusTarget == null,
                "Changing the real range layout clears focus before new targets appear");
            FocusAt(range, range.enemies[0]); range.EndPractice(); Check(range.CombatFocusTarget == null, "Leaving the range clears focus");
            var sparring = RangeGame(data); Check(sparring.StartSparring(), "Start the existing local tactical drills");
            FocusAt(sparring, sparring.enemies[0]); Check(sparring.RestartSparring() && sparring.CombatFocusTarget == null,
                "Restarting actual tactical sparring clears focus before the reused drill ID appears");
            FocusAt(sparring, sparring.enemies[0]); sparring.EndPractice(); Check(sparring.CombatFocusTarget == null, "Leaving tactical sparring clears focus");
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("combatFocus", StringComparison.OrdinalIgnoreCase) < 0 && field.FieldType != typeof(Enemy),
                    "Optional combat focus adds neither a saved focus field nor a live actor reference to SaveData");
            foreach (int version in new[] { 1, 2 })
            {
                var loaded = new GameCore(data, new SaveData { version = version, coins = 123 });
                Check(loaded.save.version == 2 && loaded.save.coins == 123 && loaded.CombatFocusTarget == null,
                    "Both accepted save versions retain their mature migration and begin without a lock");
            }
        }

        public static void CombatFocusQueriesLeaveEntireSimulationAndSaveReadOnly(Campaign data)
        {
            var game = Quiet(data, 1); Enemy target = FocusEnemy(game, 9690, 17, 14); FocusAt(game, target);
            game.Attack(new Point(-1, 0)); game.Tick(.03f, new Point());
            game.projectiles.Add(new Projectile { position = new Point(22, 15), velocity = new Point(0, 1), life = 1, source = "shot" });
            game.effects.Add(new Effect("focus_query_sentinel", game.Position, 2)); game.hitStop = .07f;
            string before = TacticsState(game);
            for (int read = 0; read < 100; read++)
                Check(ReferenceEquals(game.CombatFocusTarget, target) && game.CanAcquireCombatFocus,
                    "Repeated focus HUD queries describe the current actor and availability");
            Check(TacticsState(game) == before,
                "Queries advance no timers, preparation, flight, hitstop, effects, RNG, progression, save fields or private simulation state");
            target.hp = 0; before = TacticsState(game);
            for (int read = 0; read < 100; read++) Check(game.CombatFocusTarget == null && !game.CanAcquireCombatFocus,
                "Read-only queries reject a dead target immediately");
            Check(TacticsState(game) == before, "Reading an invalid lock never mutates even its private pending invalidation state");
            target.hp = 999; game.PrepareSave(); string saved = TacticsState(game.save); game.ClearCombatFocus();
            Check(TacticsState(game.save) == saved, "Explicitly clearing focus changes no field of an already prepared save");
            FocusAt(game, target); Check(TacticsState(game.save) == saved,
                "Acquiring focus changes no persistent field, reward, story state or RNG seed");
        }
    }
}
