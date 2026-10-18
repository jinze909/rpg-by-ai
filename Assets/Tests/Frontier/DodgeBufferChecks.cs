using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore DodgeGame(Campaign data, int profession = 0)
        { GameCore game = Quiet(data, profession); game.invulnerable = 0; return game; }
        private static int DodgeEffects(GameCore game)
        { int count = 0; foreach (Effect effect in game.effects) if (effect.kind == "dash") count++; return count; }
        private static void DodgeHit(GameCore game, float damage)
        { typeof(GameCore).GetMethod("Hurt", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { damage, "shot", (Point?)new Point(1, 0) }); }

        public static void DodgeLatePressCapturesDirectionAndExecutesOnceAtRealCooldown(Campaign data)
        {
            foreach (float step in new[] { .02f, 1f / 30, .05f }) for (int profession = 0; profession < 3; profession++)
            {
                GameCore game = DodgeGame(data, profession); game.dashCooldown = .12f;
                game.Attack(new Point(1, 0)); ActorAction prepared = game.playerAction; float energy = game.save.energy;
                game.Dash(new Point(0, -1));
                Check(game.dashTime == 0 && game.playerAction == prepared && game.invulnerable == 0 && game.save.energy == energy && DodgeEffects(game) == 0,
                    "An early dodge press queues without movement, free invulnerability, payment or cancellation");
                game.SetAim(new Point(1, 0)); int ticks = 0;
                while (game.dashTime <= 0 && ticks++ < 20) game.Tick(step, new Point());
                Check(game.dashTime == .18f && game.save.y == 14 && Math.Abs(game.save.x - 12) < .5f && game.playerAction == null && game.invulnerable == .32f,
                    "The captured north dodge starts on the actual ready tick, including the final 30 fps buffer boundary");
                Check(game.PlayerPose == 8 && game.VisualFacing.y < -.999f && DodgeEffects(game) == 1,
                    "One accepted request presents exactly one captured-direction dodge");
                float expected = energy + ticks * step * (profession == 2 ? 18 : 14) - 20;
                Check(Math.Abs(game.save.energy - Math.Min(80, expected)) < .001f && Math.Abs(game.dashCooldown - .75f) < .0001f,
                    "Buffered execution charges the mature cost once and starts the complete mature cooldown at acceptance");
                Advance(game, 1.2f); Check(DodgeEffects(game) == 0 && game.dashTime == 0, "Releasing the button never repeats the saved request");
            }
            GameCore stationary = DodgeGame(data); stationary.SetAim(new Point(-1, 0)); stationary.dashCooldown = .06f;
            stationary.Dash(new Point()); stationary.SetAim(new Point(0, 1)); Advance(stationary, .06f);
            Check(stationary.PlayerPose == 8 && stationary.VisualFacing.x < -.999f, "A stationary request captures its original aim rather than the later live cursor");
            GameCore frozen = DodgeGame(data); frozen.dashCooldown = .06f; frozen.Dash(new Point(0, -1)); frozen.hitStop = .1f;
            frozen.Tick(.05f, new Point()); frozen.Tick(.05f, new Point());
            Check(frozen.dashCooldown == .06f && frozen.dashTime == 0 && frozen.save.energy == 100,
                "Mature hit stop freezes the cooldown, pending lifetime, energy and protection together");
            Advance(frozen, .06f); Check(frozen.dashTime == .18f, "The short request resumes on the unchanged simulation clock after hit stop");
        }

        public static void DodgeRejectedRequestsNeverWaitForEnergyOrAnUnrelatedCooldown(Campaign data)
        {
            for (int rejection = 0; rejection < 6; rejection++)
            {
                GameCore game = DodgeGame(data); game.dashCooldown = .06f; game.Dash(new Point(0, -1));
                if (rejection == 0) game.dashCooldown = .121f;
                if (rejection == 1) game.save.energy = 19;
                if (rejection == 2) game.dashCooldown = float.NaN;
                if (rejection == 3) game.dashCooldown = float.PositiveInfinity;
                game.Dash(rejection == 4 ? new Point(float.NaN, 0) : rejection == 5 ? new Point(0, float.NegativeInfinity) : new Point(1, 0));
                if (rejection == 2 || rejection == 3) game.dashCooldown = .06f;
                game.save.energy = 100; Advance(game, .3f);
                Check(game.dashTime == 0 && !game.effects.Exists(effect => effect.kind == "dash"), "A rejected request clears old intent and cannot trigger after energy or cooldown recovers: " + rejection);
            }
            GameCore tooEarly = DodgeGame(data); tooEarly.dashCooldown = .5f; tooEarly.Dash(new Point(1, 0)); Advance(tooEarly, .6f);
            Check(tooEarly.dashTime == 0 && tooEarly.save.energy == 100, "The buffer does not turn an ordinary early press into a future automatic dodge");
        }

        public static void DodgeAndSkillHonorTheLastValidDiscreteIntent(Campaign data)
        {
            GameCore skillLast = DodgeGame(data, 1); skillLast.dashCooldown = .08f; skillLast.Dash(new Point(0, -1)); skillLast.Skill(new Point(1, 0));
            ActorAction skill = skillLast.playerAction; Advance(skillLast, .12f);
            Check(skillLast.playerAction == skill && skill.kind == "skill" && skillLast.dashTime == 0 && DodgeEffects(skillLast) == 0,
                "A later accepted Q replaces the not-yet-started dodge without cancelling its cast at cooldown readiness");
            GameCore dodgeLast = DodgeGame(data, 1); dodgeLast.Attack(new Point(1, 0)); Advance(dodgeLast, .12f); dodgeLast.Skill(new Point(0, -1));
            dodgeLast.dashCooldown = .08f; dodgeLast.Dash(new Point(-1, 0)); Advance(dodgeLast, .4f);
            Check(dodgeLast.skillCooldown <= 0 && dodgeLast.playerAction == null, "A later valid dodge request clears the older queued Q before executing");
            GameCore rejectedQ = DodgeGame(data); rejectedQ.dashCooldown = .06f; rejectedQ.Dash(new Point(0, -1)); rejectedQ.skillCooldown = 1; rejectedQ.Skill(); Advance(rejectedQ, .06f);
            Check(rejectedQ.dashTime > 0, "A rejected Q does not erase the valid dodge request");
            GameCore sameFrame = DodgeGame(data); sameFrame.Dash(new Point(1, 0)); sameFrame.Skill(new Point(0, -1)); Advance(sameFrame, .2f);
            Check(sameFrame.playerAction != null && sameFrame.playerAction.kind == "skill" && sameFrame.playerAction.direction.y < -.999f,
                "An already-started dodge still permits the mature same-frame Q buffer after it");
            GameCore latestDirection = DodgeGame(data); latestDirection.dashCooldown = .1f; latestDirection.Dash(new Point(1, 0)); latestDirection.Dash(new Point(0, -1)); Advance(latestDirection, .1f);
            Check(latestDirection.PlayerPose == 8 && latestDirection.VisualFacing.y < -.999f, "The most recent valid dodge press owns the captured direction");
        }

        public static void DodgeIntentCannotSurviveMenusTravelDeathOrTrainingBoundaries(Campaign data)
        {
            for (int boundary = 0; boundary < 6; boundary++)
            {
                GameCore game = DodgeGame(data); game.dashCooldown = .08f; game.Dash(new Point(1, 0));
                if (boundary == 0) { game.mode = "inventory"; game.Close(); }
                if (boundary == 1) game.Enter(0);
                if (boundary == 2) game.Load(game.save);
                if (boundary == 3) { game.NewGame(0); game.Close(); }
                if (boundary == 4) { game.save.health = 1; DodgeHit(game, 999); }
                if (boundary == 5) { game.PrepareSave(); game.Load(game.save); }
                Advance(game, .3f);
                Check(game.dashTime == 0 && DodgeEffects(game) == 0, "No pending dodge crosses the lifecycle boundary " + boundary);
            }
            for (int boundary = 0; boundary < 4; boundary++)
            {
                GameCore game = RangeGame(data); Check(game.StartRange(0), "Real range setup"); game.dashCooldown = .08f; game.Dash(new Point(1, 0));
                if (boundary == 0) game.ResetRange();
                if (boundary == 1) game.ChangeRangeLayout(1);
                if (boundary == 2) game.EndPractice();
                if (boundary == 3) { game.enemies.Clear(); game.Tick(.02f, new Point()); Check(game.RangeFinished, "Actual range clear boundary"); }
                Advance(game, .3f); Check(game.dashTime == 0 && DodgeEffects(game) == 0, "Range reset, layout, exit and clear sever pending dodge input " + boundary);
            }
        }

        public static void DodgeIntentRevalidatesGearProfilesAndCurrentExecutionCost(Campaign data)
        {
            for (int change = 0; change < 8; change++)
            {
                GameCore game = DodgeGame(data); game.dashCooldown = .08f; game.Dash(new Point(1, 0));
                if (change == 0) game.save.weapon = 1;
                if (change == 1) game.save.profession = 1;
                if (change == 2) game.save.weaponStyles[0] = 1;
                if (change == 3) game.save.technique = 1;
                if (change == 4) game.save.relics[0] = 2;
                if (change == 5) game.save.talents[2] = 1;
                if (change == 6) { game.save.flags.Add("weapon_1"); game.Equip(1); }
                if (change == 7) game.save.energy = 0;
                Advance(game, .12f); Check(game.dashTime == 0 && DodgeEffects(game) == 0, "A changed loadout or insufficient execution energy cancels the request: " + change);
            }
            GameCore relic = DodgeGame(data); relic.save.relics[0] = 0; relic.save.relics[1] = 2; relic.save.health = 50; relic.save.energy = 25;
            relic.dashCooldown = .08f; relic.Dash(new Point(1, 0));
            Check(relic.save.health == 50 && relic.save.energy == 25, "A buffered relic dodge grants neither premature healing nor early payment");
            Advance(relic, .08f);
            Check(relic.dashTime > 0 && relic.save.health == 53 && Math.Abs(relic.save.energy - 1.12f) < .001f,
                "The mature healing relic fires once at actual start and the compass keeps its 25-energy cost");
        }

        public static void DodgeBufferPreservesImmediateCancellationHitSafetyAndWalls(Campaign data)
        {
            GameCore immediate = DodgeGame(data); immediate.Attack(new Point(1, 0)); immediate.Dash(new Point(0, -1));
            Check(immediate.playerAction == null && immediate.dashTime == .18f && immediate.dashCooldown == .75f && immediate.invulnerable == .32f && immediate.save.energy == 80,
                "Ready dodges retain their original immediate cancellation, movement duration, cooldown, invulnerability and cost");
            GameCore exposed = DodgeGame(data); exposed.dashCooldown = .08f; exposed.Dash(new Point(1, 0)); float health = exposed.save.health;
            DodgeHit(exposed, 10); Check(exposed.save.health < health && exposed.dashTime == 0, "Queued input cannot retroactively evade an attack before its cooldown is ready");
            Advance(exposed, .08f); health = exposed.save.health; DodgeHit(exposed, 10);
            Check(exposed.dashTime > 0 && exposed.save.health == health, "The real accepted dodge owns protection at its actual start");
            GameCore wall = DodgeGame(data); Point footing = WallStand(wall); wall.save.x = footing.x; wall.save.y = footing.y;
            wall.dashCooldown = .08f; wall.Dash(new Point(-1, 0)); Advance(wall, .26f);
            Check(wall.CanStand(wall.Position) && wall.save.x >= (float)Math.Floor(footing.x) + .21f, "Buffered movement uses the mature swept collision footprint instead of crossing the nearby wall");
            GameCore crossing = DodgeGame(data); crossing.dashCooldown = .04f; crossing.Dash(new Point(1, 0)); crossing.Tick(.05f, new Point());
            Check(crossing.save.x == 12 && crossing.save.y == 14 && crossing.dashTime == .18f && crossing.invulnerable == .32f && crossing.dashCooldown == .75f,
                "Crossing readiness inside a .05s tick gives no backdated movement and consumes none of the new dodge's duration or protection");
            GameCore reference = DodgeGame(data); reference.Dash(new Point(1, 0));
            for (int tick = 0; tick < 4; tick++) { crossing.Tick(.05f, new Point()); reference.Tick(.05f, new Point()); }
            Check((crossing.Position - reference.Position).Length < .0001f && crossing.dashTime == reference.dashTime,
                "The buffered dodge's subsequent displacement exactly matches the mature immediate dodge at the same simulation steps");
            for (int training = 0; training < 2; training++)
            {
                GameCore game = RangeGame(data); Check(training == 0 ? game.StartRange(2) : game.StartSparring(), "Actual training setup");
                game.save.reducedMotion = true; Enemy target = game.enemies[0]; target.hp = .1f; target.timer = 9999;
                game.save.x = target.x - 1; game.save.y = target.y; game.Attack(new Point(1, 0));
                game.playerAction.elapsed = game.playerAction.contact - .04f; game.dashCooldown = .04f; game.Dash(new Point(-1, 0));
                game.Tick(.05f, new Point());
                Check((training == 0 ? game.RangeFinished : game.SparringAwaitingNext) && game.enemies.Count == 0 && game.dashTime == 0 && DodgeEffects(game) == 0 && game.save.energy == 100,
                    "A final real sword contact clears the pending dodge before training review, without charging a hidden 20 energy: " + training);
            }
        }
    }
}
