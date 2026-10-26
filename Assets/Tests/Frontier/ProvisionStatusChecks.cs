using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore ProvisionGame(Campaign data, int profession = 0)
        {
            GameCore game = Quiet(data, profession); game.SkipTutorial();
            game.save.totalTime = 100; game.save.foodUntil = game.save.teaUntil = 0;
            game.save.companion = -1; game.effects.Clear(); return game;
        }
        private static void ProvisionRead(GameCore game)
        {
            for (int repeat = 0; repeat < 20; repeat++)
            {
                float stew = game.StewBoostRemaining, tea = game.TeaBoostRemaining;
                Check(!float.IsNaN(stew) && !float.IsInfinity(stew) && stew >= 0
                    && !float.IsNaN(tea) && !float.IsInfinity(tea) && tea >= 0,
                    "Provision queries return only finite nonnegative remaining world time");
            }
        }
        private static void ProvisionTransactionState(GameCore game, SaveData before, int revision)
        {
            Check(game.save.health == before.health && game.save.energy == before.energy && game.save.heat == before.heat
                && game.save.cold == before.cold && game.save.foodUntil == before.foodUntil && game.save.teaUntil == before.teaUntil
                && game.save.inventory[6] == before.inventory[6] && game.save.tea == before.tea && game.revision == revision,
                "A rejected provision action cannot pay, heal, refill, clear climate or refresh a deadline");
        }

        public static void ProvisionStatusPreservesOriginalTransactionsAndRefreshRules(Campaign data)
        {
            Check(GameCore.StewAttackBonus == 7 && GameCore.StewDuration == 90 && GameCore.TeaRecoveryBonus == 6 && GameCore.TeaDuration == 180,
                "Displayed provision facts retain the four mature gameplay values");
            foreach (string mode in new[] { "world", "inventory" }) foreach (float missing in new[] { 100f, 2.25f, 0f })
            {
                GameCore game = ProvisionGame(data); game.mode = mode; game.save.health = game.save.maxHealth - missing;
                game.save.inventory[6] = 2; game.save.energy = 1; game.save.foodUntil = game.save.totalTime + 120;
                game.save.teaUntil = game.save.totalTime + 150; game.save.mealKind = 1; game.save.mealUntil = game.save.totalTime + 170;
                int revision = game.revision, coins = game.save.coins, seed = game.save.seed; float health = game.save.health;
                game.Eat();
                PlanStatsNear(game.save.health - health, Math.Min(missing, 55), "Actual capped stew healing");
                Check(game.save.inventory[6] == 1 && game.save.energy == 100 && game.revision == revision + 1
                    && game.StewBoostRemaining == 90 && game.TeaBoostRemaining == 150 && game.RoadMealRemaining == 170,
                    "One original stew refreshes to ninety seconds, rather than stacking or erasing tea and road meals");
                Check(game.save.coins == coins && game.save.seed == seed && game.mode == mode,
                    "The status addition changes no economy, RNG or permitted consumption context");
                Check(HealingLabels(game).Count == (missing > 0 ? 1 : 0), "Full health still accepts the buff without a fictitious recovery label");
            }
            foreach (string mode in new[] { "world", "inventory", "camp" })
            {
                GameCore game = ProvisionGame(data); game.mode = mode; game.save.tea = 2; game.save.energy = 1;
                game.save.heat = 88; game.save.cold = 92; game.save.health = 52; game.save.teaUntil = game.save.totalTime + 25;
                game.save.foodUntil = game.save.totalTime + 70; game.save.mealKind = 2; game.save.mealUntil = game.save.totalTime + 80;
                int revision = game.revision; game.DrinkTea();
                Check(game.save.tea == 1 && game.save.energy == 100 && game.save.health == 52 && game.save.heat == 0 && game.save.cold == 0
                    && game.TeaBoostRemaining == 180 && game.StewBoostRemaining == 70 && game.RoadMealRemaining == 80 && game.revision == revision + 1,
                    "One original tea restores only energy, clears climate and refreshes its independent one-hundred-eighty-second deadline");
            }
            foreach (string mode in new[] { "pause", "dialog", "journal", "plans", "title" })
            {
                GameCore game = ProvisionGame(data); game.mode = mode; game.save.tea = 2; game.save.inventory[6] = 2;
                SaveData before = CloneSave(game.save); int revision = game.revision; game.Eat(); game.DrinkTea();
                ProvisionTransactionState(game, before, revision);
            }
            foreach (int denied in new[] { 0, 1, 2 })
            {
                GameCore game = ProvisionGame(data); game.save.inventory[6] = denied == 0 ? 0 : 2; game.save.tea = denied == 0 ? 0 : 2;
                if (denied == 1) game.deathTime = .3f; if (denied == 2) game.practicing = true;
                SaveData before = CloneSave(game.save); int revision = game.revision; game.Eat(); game.DrinkTea();
                ProvisionTransactionState(game, before, revision);
            }
        }

        public static void ProvisionStatusTracksRealDamageRecoveryClimateAndExpiry(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                GameCore plain = ProvisionGame(data, profession), boosted = ProvisionGame(data, profession);
                boosted.save.inventory[6] = 1; boosted.save.tea = 1; boosted.Eat(); boosted.DrinkTea();
                PlanStatsNear(boosted.Damage - plain.Damage, 7, "Stew contributes the existing seven base attack before weapon scaling");
                plain.save.energy = boosted.save.energy = 10;
                Advance(plain, 1); Advance(boosted, 1);
                PlanStatsNear(boosted.save.energy - plain.save.energy, 6, "Actual tea recovery stacks with every profession's original recovery");
                PlanStatsNear(boosted.StewBoostRemaining, 89, "Stew remaining time uses the real simulation clock");
                PlanStatsNear(boosted.TeaBoostRemaining, 179, "Tea remaining time uses the same clock");
                boosted.save.totalTime = 200; boosted.save.foodUntil = boosted.save.teaUntil = 200.025f;
                boosted.save.energy = 10; boosted.Tick(.05f, new Point());
                Check(boosted.StewBoostRemaining == 0 && boosted.TeaBoostRemaining == 0,
                    "The frame crossing either deadline shows neither expired provision as active");
                PlanStatsNear(boosted.Damage, plain.Damage, "The expiry frame has no leftover stew damage");
                PlanStatsNear(boosted.save.energy, 10 + .05f * (profession == 2 ? 18 : 14), "The expiry frame grants no leftover tea recovery");
            }
            foreach (int room in new[] { 6, 7 })
            {
                GameCore game = Unlocked(data); game.Enter(room); game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear(); game.invulnerable = 1000;
                game.save.tea = 1; game.DrinkTea(); Advance(game, 1);
                Check(game.save.heat == 0 && game.save.cold == 0 && game.TeaBoostRemaining > 178.9f,
                    "An actual consumed tea retains original climate immunity in the desert and mountain");
            }
        }

        public static void ProvisionStatusUsesExistingPausedTrainingAndRestClocks(Campaign data)
        {
            GameCore game = ProvisionGame(data); game.save.inventory[6] = 1; game.save.tea = 1; game.Eat(); game.DrinkTea();
            float time = game.save.totalTime, stew = game.StewBoostRemaining, tea = game.TeaBoostRemaining;
            foreach (string mode in new[] { "inventory", "dialog", "journal", "pause", "camp", "fish", "materials" })
            {
                game.mode = mode; game.Tick(.05f, new Point(1, 0));
                Check(game.save.totalTime == time && game.StewBoostRemaining == stew && game.TeaBoostRemaining == tea,
                    "Menus and fishing do not spend the world-clock provision durations: " + mode);
            }
            game.Close(); game.hitStop = .1f; game.Tick(.05f, new Point());
            Check(game.save.totalTime == time && game.StewBoostRemaining == stew && game.TeaBoostRemaining == tea,
                "The existing hit-stop clock freezes both provision timers");
            game.hitStop = 0; game.Enter(0); game.enemies.Clear();
            Action(game, "rest", "rest");
            Check(game.save.totalTime == time + 45 && game.StewBoostRemaining == 45 && game.TeaBoostRemaining == 135,
                "A real inn rest advances forty-five world seconds without refreshing supplies");
            game.Close(); Action(game, "rest", "rest");
            Check(game.StewBoostRemaining == 0 && game.TeaBoostRemaining == 90, "The second genuine rest expires stew while tea remains independent");
            GameCore training = RangeGame(data); training.save.foodUntil = training.save.totalTime + 70; training.save.teaUntil = training.save.totalTime + 140;
            time = training.save.totalTime; Check(training.StartRange(0), "Start the actual safe range before checking its simulation clock");
            Advance(training, .5f);
            Check(training.SafeTrainingActive && training.save.totalTime == time && training.StewBoostRemaining == 70 && training.TeaBoostRemaining == 140,
                "Safe range rehearsal preserves both existing provision deadlines instead of consuming them");
            training.EndPractice();
            Check(training.save.totalTime == time && training.StewBoostRemaining == 70 && training.TeaBoostRemaining == 140,
                "Leaving real safe training restores the existing world owner without reapplying either supply");
        }

        public static void ProvisionStatusReadsPreserveAcceptedAttacksQueuesAndLegacySaves(Campaign data)
        {
            GameCore malformed = ProvisionGame(data);
            foreach (float deadline in new[] { 0f, 99f, 100f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                malformed.save.foodUntil = malformed.save.teaUntil = deadline; string state = PlanStatsState(malformed);
                Check(malformed.StewBoostRemaining == 0 && malformed.TeaBoostRemaining == 0 && PlanStatsState(malformed) == state,
                    "Expired or nonfinite raw deadlines show zero without sanitizing or mutating the owner");
            }
            foreach (float clock in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                malformed.save.totalTime = clock; malformed.save.foodUntil = malformed.save.teaUntil = 200; string state = PlanStatsState(malformed);
                Check(malformed.StewBoostRemaining == 0 && malformed.TeaBoostRemaining == 0 && PlanStatsState(malformed) == state,
                    "A nonfinite raw clock cannot manufacture a visible countdown or write back repairs");
            }
            foreach (bool expiresBeforeRelease in new[] { false, true })
            {
                GameCore game = ProvisionGame(data, 1); if (expiresBeforeRelease) game.save.foodUntil = game.save.totalTime + .015f;
                game.Attack(new Point(1, 0)); ActorAction accepted = game.playerAction;
                Check(accepted != null && !accepted.released, "An actual bow preparation owns captured damage and aim");
                float damage = accepted.damage; object impact = accepted.impact; string before = PlanStatsState(game); ProvisionRead(game);
                Check(PlanStatsState(game) == before && ReferenceEquals(game.playerAction, accepted) && ReferenceEquals(accepted.impact, impact),
                    "Repeated countdown reads preserve accepted attack identity, profile, captured aim, RNG, effects and every public state value");
                if (!expiresBeforeRelease)
                {
                    game.save.inventory[6] = 1; game.Eat();
                    Check(ReferenceEquals(game.playerAction, accepted) && accepted.damage == damage && game.Damage > damage,
                        "Eating after attack acceptance never recaptures the already prepared arrow's damage");
                }
                for (int frame = 0; frame < 80 && !accepted.released; frame++) game.Tick(.01f, new Point());
                Check(accepted.released && game.projectiles.Count == 1, "The accepted action releases one actual arrow on its original contact tick");
                PlanStatsNear(game.projectiles[0].damage, damage, "The real arrow retains its accepted damage across supply application or expiry");
                Projectile shot = game.projectiles[0]; before = PlanStatsState(game); ProvisionRead(game);
                Check(PlanStatsState(game) == before && ReferenceEquals(game.projectiles[0], shot), "Reading a flying arrow's provision status cannot move or recapture it");
            }
            foreach (string kind in new[] { "attack", "skill", "dodge", "companion" })
            {
                GameCore game = ProvisionGame(data); string field;
                if (kind == "attack") { game.attackCooldown = .08f; game.PressAttack(new Point(0, 1)); field = "bufferedAttack"; }
                else if (kind == "skill") { game.skillCooldown = .08f; game.Skill(new Point(0, 1)); field = "bufferedSkill"; }
                else if (kind == "dodge") { game.dashCooldown = .08f; game.Dash(new Point(0, 1)); field = "bufferedDodge"; }
                else { game.save.flags.Add("companion_1"); game.SelectCompanion(1); game.companionCooldown = .08f; game.CommandCompanion(); field = "bufferedCompanionCommand"; }
                object intent = PlanStatsIntent(game, field); Check(intent != null, "An actual queued input exists before status reads: " + kind);
                string before = PlanStatsState(game); ProvisionRead(game);
                Check(PlanStatsState(game) == before && ReferenceEquals(PlanStatsIntent(game, field), intent), "Countdown queries preserve unpaid intent owner, lifetime and captured aim: " + kind);
            }
            foreach (int version in new[] { 1, 2 })
            {
                GameCore source = ProvisionGame(data); source.save.version = version; source.save.foodUntil = 220; source.save.teaUntil = 280;
                source.save.health = 62; source.save.energy = 44; source.save.coins = 91; source.save.flags.Add("legacy_provision_status_keep");
                GameCore loaded = new GameCore(data, CloneSave(source.save));
                Check(loaded.StewBoostRemaining == 120 && loaded.TeaBoostRemaining == 180,
                    "A legacy accepted one-hundred-twenty-second stew deadline is reported truthfully, without a new ninety-second read clamp");
                string before = PlanStatsState(loaded); ProvisionRead(loaded);
                Check(PlanStatsState(loaded) == before && loaded.save.health == 62 && loaded.save.energy == 44 && loaded.save.coins == 91 && loaded.Has("legacy_provision_status_keep"),
                    "v1/v2 migrated save queries do not heal, refill, charge or alter unrelated saved progress");
                loaded.PrepareSave(); GameCore roundtrip = new GameCore(data, CloneSave(loaded.save));
                Check(roundtrip.StewBoostRemaining == 120 && roundtrip.TeaBoostRemaining == 180 && roundtrip.save.health == 62
                    && roundtrip.save.energy == 44 && roundtrip.save.coins == 91 && roundtrip.Has("legacy_provision_status_keep"),
                    "The original prepare/load path retains both existing deadlines and unrelated progress without a new save field");
            }
        }
    }
}
