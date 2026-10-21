using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static int SkillCooldownEffects(GameCore game, string kind)
        { return game.effects.FindAll(effect => effect.kind == kind).Count; }

        private static ActorAction SkillCooldownAwaitCast(GameCore game, float step, bool heldAttack = false)
        {
            for (int frame = 0; frame < 80; frame++)
            {
                game.Tick(step, new Point(), new Point(-1, 0));
                if (heldAttack) game.Attack(new Point(-1, 0));
                if (game.playerAction != null && game.playerAction.kind == "skill") return game.playerAction;
            }
            return null;
        }

        public static void SkillCooldownThresholdsAndOverlappingReadiness(Campaign data)
        {
            foreach (float step in new[] { .01f, .02f, 1f / 30, .05f })
            for (int profession = 0; profession < 3; profession++)
            for (int blocker = 0; blocker < 4; blocker++)
            foreach (float cooldown in new[] { .20f, .201f })
            {
                GameCore game = Quiet(data, profession); ActorAction oldAction = null;
                if (blocker == 1)
                {
                    game.Attack(new Point(1, 0)); oldAction = game.playerAction;
                    Advance(game, oldAction.contact + .1f - .12f);
                }
                if (blocker == 2) game.Dash(new Point(1, 0));
                if (blocker == 3) game.playerHitTime = .18f;
                game.skillCooldown = cooldown;
                float energy = game.save.energy; int effects = game.effects.Count, shots = game.projectiles.Count;
                game.Skill(new Point(0, -1));
                Check(game.playerAction == oldAction && game.save.energy == energy && game.skillCooldown == cooldown
                    && game.effects.Count == effects && game.projectiles.Count == shots,
                    "A cooldown Q never starts an action, spends energy or emits effects at the press");
                ActorAction cast = SkillCooldownAwaitCast(game, step);
                string context = " profession " + profession + " blocker " + blocker + " dt " + step + " cooldown " + cooldown;
                Check((cast != null) == (cooldown == .20f), ".20 is accepted and .201 is rejected independently of the frame cadence:" + context);
                if (cast == null) continue;
                Check(cast.elapsed == 0 && !cast.released && game.skillCooldown == 3.5f && game.dashTime == 0 && game.playerHitTime == 0,
                    "Overlapping cooldown/action/dodge/recoil clocks meet at their maximum and start a fresh cast:" + context);
                if (oldAction != null) Check(oldAction.released && oldAction.elapsed + .00001f >= oldAction.contact + .1f,
                    "A cooldown Q preserves the old attack's entire contact interval");
                float simulated = game.save.totalTime;
                Check(simulated < .46f, "Readiness overlap is never added sequentially to the .20-second window");
            }
            foreach (float step in new[] { .01f, .02f, 1f / 30, .05f })
            foreach (float delay in new[] { .20f, .201f })
            {
                GameCore game = Quiet(data, 2); game.Attack(new Point(1, 0));
                Advance(game, game.playerAction.contact + .1f - delay);
                game.skillCooldown = .06f; game.Skill(new Point(0, -1));
                ActorAction cast = SkillCooldownAwaitCast(game, step);
                Check((cast != null) == (delay == .20f), "The pre-tick action deadline admits .20 but never grants .201 extra grace at dt " + step);
            }
            GameCore immediate = Quiet(data); immediate.Skill(new Point(0, -1));
            Check(immediate.playerAction != null && immediate.playerAction.kind == "skill" && immediate.save.energy == 60
                && immediate.skillCooldown == 3.5f, "A fully ready Q retains its immediate mature behavior");
        }

        public static void SkillCooldownRetainsReleasedVolleyAndExecutionValues(Campaign data)
        {
            GameCore archer = Quiet(data, 1); archer.skillCooldown = .12f;
            Point target = new Point(21, 12), direction = (target - archer.Position).Normalized;
            archer.SetAimTarget(target); archer.Skill(direction);
            archer.SetAimTarget(new Point(5, 18)); archer.SetAim(new Point(-1, 0));
            ActorAction volley = SkillCooldownAwaitCast(archer, .02f);
            Check(volley != null && (volley.direction - direction).Length < .0001f && volley.target.HasValue
                && (volley.target.Value - target).Length < .0001f, "Cooldown Q retains the pressed direction and pointer independently of live aim");
            Check(archer.projectiles.Count == 0 && SkillCooldownEffects(archer, "release") == 0,
                "Dispatch begins preparation without an early arrow release");
            for (int frame = 0; frame < 60 && !volley.released; frame++) archer.Tick(.01f, new Point(), new Point(-1, 0));
            var arrows = archer.projectiles.FindAll(shot => shot.technique);
            Point origin = GameCore.WeaponOrigin(archer.Position, 1, direction), expected = (target - origin).Normalized;
            Check(volley.released && arrows.Count == 5 && (arrows[2].velocity.Normalized - expected).Length < .001f,
                "The actual five-arrow release aims the center arrow from the authored muzzle to the retained target");
            Effect release = archer.effects.Find(effect => effect.kind == "release");
            Check(release != null && (release.position - origin).Length < .001f && (release.direction - direction).Length < .001f,
                "The actual bow release effect shares the saved cast direction and muzzle");

            for (int profession = 0; profession < 3; profession++)
            {
                GameCore game = Quiet(data, profession); game.save.health = 50; game.save.energy = 70; game.skillCooldown = .08f;
                float queuedDamage = game.Damage; game.Skill(new Point(1, 0));
                Check(game.save.health == 50 && game.save.energy == 70 && game.playerAction == null && game.effects.Count == 0,
                    "An accepted intent reserves neither payment, healing nor an action");
                game.save.relics[0] = 4; game.save.talents[0] = 2;
                ActorAction cast = SkillCooldownAwaitCast(game, .02f);
                Check(cast != null && cast.damage == game.Damage && cast.damage > queuedDamage && Math.Abs(cast.impact.criticalChance - .11f) < .001f,
                    "Damage and impact values use the execution-time build, profession " + profession);
                float expectedEnergy = 70 + .08f * (profession == 2 ? 18 : 14) - 50;
                Check(Math.Abs(game.save.energy - expectedEnergy) < .001f && game.save.health == 58 && SkillCooldownEffects(game, "healthGain") == 1,
                    "Execution rechecks the current relic's 50 cost and grants its eight healing exactly once");
                float preparedDamage = cast.damage; game.save.talents[0] = 3;
                for (int frame = 0; frame < 60 && !cast.released; frame++) game.Tick(.01f, new Point());
                Check(cast.released && cast.damage == preparedDamage && SkillCooldownEffects(game, "healthGain") == 1,
                    "Once started, a cast keeps its prepared values and cannot repeat its payment or healing");
            }
        }

        public static void SkillCooldownRejectsInvalidRequestsAndRechecksCosts(Campaign data)
        {
            foreach (bool relic in new[] { false, true })
            {
                GameCore exact = Quiet(data); if (relic) exact.save.relics[0] = 4;
                exact.save.energy = relic ? 50 : 40; exact.skillCooldown = .08f; exact.Skill(new Point(1, 0));
                ActorAction paid = SkillCooldownAwaitCast(exact, .02f);
                Check(paid != null && Math.Abs(exact.save.energy - .08f * 14) < .001f,
                    "The exact mature 40/50 affordability boundary accepts a cooldown intent and pays once");
            }
            for (int rejection = 0; rejection < 10; rejection++)
            {
                GameCore game = Quiet(data); game.skillCooldown = .08f; game.Skill(new Point(0, -1));
                if (rejection == 0) game.skillCooldown = .201f;
                if (rejection == 1) game.save.energy = 39;
                if (rejection == 2) { game.save.relics[0] = 4; game.save.energy = 49; }
                if (rejection == 3) game.skillCooldown = float.NaN;
                if (rejection == 4) game.skillCooldown = float.PositiveInfinity;
                if (rejection == 5) game.skillCooldown = float.NegativeInfinity;
                if (rejection == 6) game.save.energy = float.NaN;
                if (rejection == 7) game.save.energy = float.PositiveInfinity;
                if (rejection == 8) game.save.energy = float.NegativeInfinity;
                if (rejection == 9) game.skillCooldown = 1;
                game.Skill(new Point(1, 0));
                Check(game.playerAction == null && game.effects.Count == 0, "Rejected Q never creates a prepared cast or benefit: " + rejection);
                if (rejection < 3 || rejection == 9) Check(game.toast.Length > 0, "Ordinary unavailable Q retains feedback");
                game.skillCooldown = .08f; game.save.energy = 100; Advance(game, 1);
                Check(game.playerAction == null && game.skillCooldown <= 0 && SkillCooldownEffects(game, "burst") == 0,
                    "A rejected request clears the older intent and cannot revive after cooldown or energy recovery: " + rejection);
            }
            foreach (int change in new[] { 0, 1, 2, 3 })
            {
                GameCore game = Quiet(data); game.skillCooldown = .08f; game.save.energy = 40; game.Skill(new Point(1, 0));
                if (change == 0) game.save.energy = 0;
                if (change == 1) game.save.relics[0] = 4;
                if (change == 2) game.save.energy = float.NaN;
                if (change == 3) game.skillCooldown = float.PositiveInfinity;
                Advance(game, .1f);
                Check(game.playerAction == null && SkillCooldownEffects(game, "healthGain") == 0 && game.save.health == game.save.maxHealth,
                    "Dispatch cancels if current cost or finite resources are invalid: " + change);
                game.save.energy = 100; game.skillCooldown = 0; Advance(game, 1);
                Check(game.playerAction == null && game.skillCooldown <= 0 && SkillCooldownEffects(game, "burst") == 0,
                    "A failed dispatch is consumed instead of waiting for a later refill");
            }
            GameCore tooEarly = Quiet(data); tooEarly.skillCooldown = .8f; tooEarly.Skill(); Advance(tooEarly, 4.5f);
            Check(tooEarly.playerAction == null && tooEarly.save.energy == 100 && tooEarly.projectiles.Count == 0,
                "An early Q cannot become a cooldown automation loop");
        }

        public static void SkillCooldownHonorsDiscretePriorityAndHitStop(Campaign data)
        {
            GameCore skillLast = Quiet(data); skillLast.dashCooldown = .08f; skillLast.Dash(new Point(-1, 0));
            skillLast.skillCooldown = .12f; skillLast.Skill(new Point(0, -1));
            ActorAction cast = SkillCooldownAwaitCast(skillLast, .02f, true);
            Check(cast != null && cast.direction.y < -.999f && SkillCooldownEffects(skillLast, "dash") == 0 && skillLast.dashTime == 0,
                "Accepted cooldown Q supersedes the earlier pending dodge and continuously held basic attack");
            GameCore dodgeLast = Quiet(data); dodgeLast.skillCooldown = .12f; dodgeLast.Skill(new Point(1, 0));
            dodgeLast.dashCooldown = .08f; dodgeLast.Dash(new Point(0, -1)); Advance(dodgeLast, .5f);
            Check(dodgeLast.playerAction == null && dodgeLast.skillCooldown <= 0 && SkillCooldownEffects(dodgeLast, "burst") == 0,
                "A later accepted dodge consumes the pending cooldown Q");
            foreach (bool insufficient in new[] { false, true })
            {
                GameCore rejected = Quiet(data); rejected.dashCooldown = .08f; rejected.Dash(new Point(0, -1));
                rejected.skillCooldown = insufficient ? .08f : .201f; rejected.save.energy = insufficient ? 39 : 100;
                rejected.Skill(new Point(1, 0)); Advance(rejected, .08f);
                Check(rejected.dashTime > 0 && rejected.playerAction == null && rejected.VisualFacing.y < -.999f,
                    "Rejected cooldown/energy Q preserves the earlier valid dodge");
            }
            GameCore latest = Quiet(data, 1); latest.skillCooldown = .12f; latest.Skill(new Point(1, 0)); latest.Tick(.02f, new Point());
            Point target = new Point(18, 17); latest.SetAimTarget(target); latest.Skill(new Point(0, 1));
            ActorAction lastCast = SkillCooldownAwaitCast(latest, .02f, true);
            Check(lastCast != null && lastCast.direction.y > .999f && lastCast.target.HasValue && (lastCast.target.Value - target).Length < .001f,
                "Repeated accepted Q replaces one intent with the latest direction and target");
            Advance(latest, 4.5f); int releases = 0; foreach (string sound in latest.sounds) if (sound == "skill") releases++;
            Check(releases == 1 && latest.playerAction == null && latest.skillCooldown <= 0,
                "One released Q casts once and cannot repeat after its next full cooldown");

            GameCore frozen = Quiet(data); frozen.skillCooldown = .20f; frozen.Skill(new Point(0, -1)); frozen.hitStop = .15f;
            float energy = frozen.save.energy, time = frozen.save.totalTime;
            for (int frame = 0; frame < 3; frame++) frozen.Tick(.05f, new Point());
            Check(frozen.skillCooldown == .20f && frozen.playerAction == null && frozen.save.energy == energy && frozen.save.totalTime == time,
                "Hit stop freezes cooldown, buffer lifetime, payment and world clock together");
            ActorAction resumed = SkillCooldownAwaitCast(frozen, .05f);
            Check(resumed != null && resumed.elapsed == 0 && frozen.save.energy == 60 && frozen.skillCooldown == 3.5f,
                "The full .20 boundary intent survives hit stop and starts once on the resumed clock");
        }

        public static void SkillCooldownClearsAtLifecycleProfilesAndTrainingReview(Campaign data)
        {
            GameCore unchanged = Quiet(data, 1); unchanged.skillCooldown = .12f; unchanged.Skill(new Point(0, -1));
            Check(SkillCooldownAwaitCast(unchanged, .02f) != null, "An unchanged captured profile releases its cooldown intent before testing boundary cancellation");
            Action<GameCore>[] transitions = {
                g => { g.mode = "pause"; g.Close(); }, g => { g.mode = "inventory"; g.Close(); },
                g => { g.mode = "dialog"; g.Close(); }, g => g.ClearBufferedCombatInputs(),
                g => g.Enter(0), g => { g.PrepareSave(); g.Load(CloneSave(g.save)); },
                g => { g.NewGame(0); g.Close(); }, g => g.save.weapon = 0,
                g => g.save.profession = 2, g => g.save.weaponStyles[1] = 1, g => g.save.technique = 1,
                g => { g.save.flags.Add("weapon_0"); g.Equip(0); },
                g => { g.invulnerable = 0; g.save.health = 1; g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.01f, new Point()); Check(g.Dying, "Actual lethal contact starts recovery"); }
            };
            for (int boundary = 0; boundary < transitions.Length; boundary++)
            {
                GameCore game = Quiet(data, 1); game.skillCooldown = .12f; game.Skill(new Point(0, -1)); transitions[boundary](game);
                Advance(game, 1.4f);
                Check(game.playerAction == null && game.skillCooldown <= 0 && !game.projectiles.Exists(shot => shot.technique),
                    "Cooldown intent cannot cross a menu, input-clear, world, save, profile or death boundary: " + boundary);
            }
            for (int boundary = 0; boundary < 6; boundary++)
            {
                GameCore game = RangeGame(data, 1);
                if (boundary == 0) { game.skillCooldown = .12f; game.Skill(); game.StartPractice(); }
                else
                {
                    Check(game.StartRange(0), "Start the real range before its boundary"); game.skillCooldown = .12f; game.Skill();
                    if (boundary == 1) game.ResetRange();
                    if (boundary == 2) game.ChangeRangeLayout(1);
                    if (boundary == 3) game.EndPractice();
                    if (boundary == 4) { game.enemies.Clear(); game.Tick(.02f, new Point()); Check(game.RangeFinished, "Real range review starts"); }
                    if (boundary == 5) { game.PrepareSave(); game.Load(CloneSave(game.save)); }
                }
                Advance(game, .5f);
                Check(game.playerAction == null && game.skillCooldown <= 0 && !game.projectiles.Exists(shot => shot.technique),
                    "Practice entry/reset/layout/exit/review/load clears cooldown input: " + boundary);
            }
            for (int training = 0; training < 2; training++)
            {
                GameCore game = RangeGame(data); Check(training == 0 ? game.StartRange(2) : game.StartSparring(), "Start actual safe training");
                game.save.reducedMotion = true; Enemy target = game.enemies[0]; target.hp = .1f; target.timer = 9999;
                game.save.x = target.x - 1; game.save.y = target.y;
                int coins = game.save.coins, xp = game.save.xp, kills = game.save.kills[0], mastery = game.save.mastery[0];
                game.Attack(new Point(1, 0)); game.playerAction.elapsed = game.playerAction.contact - .04f;
                game.skillCooldown = .04f; game.Skill(new Point(1, 0)); game.Tick(.05f, new Point());
                Check((training == 0 ? game.RangeFinished : game.SparringAwaitingNext) && game.playerAction == null && game.save.energy == 100
                    && game.skillCooldown <= 0 && game.projectiles.Count == 0 && SkillCooldownEffects(game, "healthGain") == 0,
                    "The final actual basic contact clears cooldown Q before review without hidden payment or effects");
                game.Skill(); Advance(game, .5f);
                Check(game.playerAction == null && game.save.coins == coins && game.save.xp == xp && game.save.kills[0] == kills && game.save.mastery[0] == mastery,
                    "Training review stays inert and awards no world farming progress from queued or repeated Q");
            }
        }
    }
}
