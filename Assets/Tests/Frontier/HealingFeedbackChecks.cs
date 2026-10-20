using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static List<Effect> HealingLabels(GameCore game)
        { return game.effects.FindAll(effect => effect.kind == "healthGain"); }
        private static string HealingText(float amount)
        { return "+" + (amount > 0 && amount < .01f ? "<0.01" : amount.ToString("0.##", CultureInfo.InvariantCulture)); }
        private static void HealingOne(GameCore game, float actual, Point position)
        {
            List<Effect> labels = HealingLabels(game);
            Check(labels.Count == 1 && labels[0].text == HealingText(actual)
                && Math.Abs(labels[0].life - .7f) < .0001f && (labels[0].position - position).Length < .0001f,
                "One real health transaction labels its exact capped gain at its actual player position with the complete .7-second lifetime");
        }
        private static GameCore HealingGame(Campaign data, int profession = 0)
        { GameCore game = Quiet(data, profession); game.save.health = 20; game.save.energy = 100; return game; }
        private static GameCore HealingMira(Campaign data, int bond)
        {
            GameCore game = HealingGame(data); game.save.flags.Add("companion_0"); game.save.relationships[0] = bond;
            game.SelectCompanion(0); game.save.health = 20; return game;
        }

        public static void HealingFoodReportsActualRecoveryAndKeepsExistingBuffTransactions(Campaign data)
        {
            foreach (bool english in new[] { false, true }) foreach (float missing in new[] { 100f, 2.25f, .001f, 0f })
            {
                GameCore game = HealingGame(data); game.save.english = english; game.mode = "inventory";
                game.save.health = game.save.maxHealth - missing; game.save.energy = 1; game.save.inventory[6] = 2;
                float health = game.save.health; int coins = game.save.coins, seed = game.save.seed, revision = game.revision;
                game.Eat(); float actual = game.save.health - health;
                Check(Math.Abs(actual - Math.Min(missing, 55)) < .0001f && game.save.inventory[6] == 1
                    && game.save.energy == 100 && game.save.foodUntil == game.save.totalTime + 90 && game.revision == revision + 1,
                    "Eating retains exact meal payment, capped 55 recovery, full energy, ninety-second food buff and one revision");
                Check(game.save.coins == coins && game.save.seed == seed && game.mode == "inventory", "Feedback never changes coins, RNG or the existing inventory context");
                if (actual > 0) HealingOne(game, actual, game.Position);
                else Check(HealingLabels(game).Count == 0, "An accepted full-health meal grants its buff without inventing recovered health");
                Check(game.toast.Contains(HealingText(actual)), "The meal receipt reports the same actual recovery, including zero or fractional caps");
                float time = game.save.totalTime; game.Tick(.5f, new Point());
                Check(game.save.totalTime == time && (actual == 0 || HealingLabels(game)[0].life == .7f), "Inventory pause freezes the floating receipt on the normal simulation clock");
                game.Close(); Advance(game, .72f); Check(HealingLabels(game).Count == 0, "Resuming normal simulation expires the meal label without a repeated heal");
            }
            foreach (int denial in new[] { 0, 1, 2 })
            {
                GameCore game = HealingGame(data); game.save.inventory[6] = denial == 0 ? 0 : 2;
                if (denial == 1) game.mode = "pause"; if (denial == 2) game.deathTime = 1;
                string before = ForgeState(game); game.Eat();
                Check(ForgeState(game) == before && HealingLabels(game).Count == 0, "No supplies, menus and death cannot fabricate healing, payment or food bonuses");
            }
            GameCore repeated = HealingGame(data); repeated.save.inventory[6] = 3;
            repeated.Eat(); repeated.Eat(); repeated.Eat();
            List<Effect> values = HealingLabels(repeated);
            Check(values.Count == 2 && values[0].text == "+55" && values[1].text == "+45" && repeated.save.health == 120 && repeated.save.inventory[6] == 0,
                "Rapid real meals report each independently capped transaction, while the third full-health meal only refreshes the mature buff");
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                GameCore fractional = HealingGame(data); fractional.save.english = true; fractional.save.inventory[6] = 1;
                fractional.save.health = fractional.save.maxHealth - 2.25f; fractional.Eat();
                Check(HealingLabels(fractional).Count == 1 && HealingLabels(fractional)[0].text == "+2.25" && fractional.toast.Contains("+2.25"),
                    "A comma-decimal OS culture cannot change either the numeric recovery label or the actual food receipt");
            }
            finally { CultureInfo.CurrentCulture = previousCulture; }
        }

        public static void HealingSkillRelicWaitsForThePaidCastAndNeverTheQueuedPress(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) foreach (float missing in new[] { 100f, 2.25f, 0f })
            {
                GameCore game = HealingGame(data, profession); game.save.relics[0] = 4; game.save.health = game.save.maxHealth - missing; game.save.energy = 50;
                float health = game.save.health; game.Skill(new Point(1, 0)); float actual = game.save.health - health;
                Check(Math.Abs(actual - Math.Min(missing, 8)) < .0001f && game.save.energy == 0 && game.skillCooldown == 3.5f
                    && game.playerAction != null && game.playerAction.kind == "skill" && !game.playerAction.released,
                    "All three professions heal only with the real fifty-energy skill acceptance, before its unchanged later offensive contact");
                if (actual > 0) HealingOne(game, actual, game.Position); else Check(HealingLabels(game).Count == 0, "A full-health skill keeps its cost and cooldown without a fake heal");
                int labels = HealingLabels(game).Count; ActorAction accepted = game.playerAction; game.Skill();
                Check(HealingLabels(game).Count == labels && game.playerAction == accepted && game.save.health == health + actual,
                    "Repeated Q during cooldown cannot pay, heal or label twice");
            }
            foreach (bool raceToFull in new[] { false, true })
            {
                GameCore game = HealingGame(data); game.save.relics[0] = 4; game.Attack(new Point(1, 0)); Advance(game, .12f);
                ActorAction attack = game.playerAction; float health = game.save.health, energy = game.save.energy;
                game.Skill(new Point(0, -1));
                Check(game.playerAction == attack && game.save.health == health && game.save.energy == energy && HealingLabels(game).Count == 0,
                    "An actual queued Q press has neither early relic recovery nor a floating promise of recovery");
                if (raceToFull) game.save.health = game.save.maxHealth;
                for (int tick = 0; tick < 30 && game.skillCooldown <= 0; tick++)
                {
                    game.Tick(.01f, new Point());
                    if (game.skillCooldown <= 0) Check(HealingLabels(game).Count == 0 && game.save.health == (raceToFull ? game.save.maxHealth : health), "Every preparation tick remains unhealed until the paid cast actually starts");
                }
                Check(game.skillCooldown == 3.5f && game.playerAction.kind == "skill" && game.playerAction.direction.y < -.99f,
                    "The captured queued direction reaches the mature actual cast, not a manufactured test-only release");
                if (!raceToFull) HealingOne(game, 8, game.Position); else Check(HealingLabels(game).Count == 0, "A race to full health before real cast acceptance suppresses a stale +8 label");
                Advance(game, .72f); Check(HealingLabels(game).Count == 0, "Skill contact and recovery frames cannot repeat the acceptance heal");
            }
            for (int denial = 0; denial < 3; denial++)
            {
                GameCore game = HealingGame(data); game.save.relics[0] = 4;
                if (denial == 0) game.save.energy = 49; if (denial == 1) game.skillCooldown = 1; if (denial == 2) game.mode = "inventory";
                string before = ForgeState(game); game.Skill();
                Check(ForgeState(game) == before && HealingLabels(game).Count == 0 && game.playerAction == null, "Rejected skill conditions never heal or dispatch labels");
            }
            GameCore plain = HealingGame(data); plain.Skill(); Check(plain.save.health == 20 && HealingLabels(plain).Count == 0, "An ordinary skill without the relic creates no new health feedback");
        }

        public static void HealingDodgeRelicLabelsOnlyOneAcceptedImmediateOrBufferedStart(Campaign data)
        {
            foreach (bool buffered in new[] { false, true }) foreach (float missing in new[] { 100f, 2.25f, 0f })
            {
                GameCore game = HealingGame(data); game.save.relics[0] = 0; game.save.health = game.save.maxHealth - missing;
                game.dashCooldown = buffered ? .08f : 0; float health = game.save.health; game.Dash(new Point(1, 0));
                if (buffered)
                {
                    Check(game.save.health == health && game.save.energy == 100 && HealingLabels(game).Count == 0 && game.dashTime == 0,
                        "A saved dodge intent grants neither relic health nor early payment or feedback");
                    game.Tick(.04f, new Point()); Check(HealingLabels(game).Count == 0 && game.save.health == health, "The not-yet-ready midpoint has no healing label");
                    game.Tick(.04f, new Point());
                }
                float actual = game.save.health - health;
                Check(Math.Abs(actual - Math.Min(missing, 3)) < .0001f && game.dashTime == .18f && game.save.energy == 80 && game.dashCooldown == .75f,
                    "Real immediate and buffered dodges preserve the exact twenty-energy cost, duration, cooldown and capped three-health recovery");
                if (actual > 0) HealingOne(game, actual, game.Position); else Check(HealingLabels(game).Count == 0, "A full-health dodge never announces a fictitious +3");
                int labels = HealingLabels(game).Count; game.Dash(new Point(1, 0)); Check(HealingLabels(game).Count == labels, "A second early dodge press cannot repeat the accepted start's heal");
                Advance(game, .72f); Check(game.save.health == health + actual && HealingLabels(game).Count == 0, "Recovery, invulnerability and expiry cannot dispatch duplicate healing");
            }
            GameCore race = HealingGame(data); race.save.relics[0] = 0; race.dashCooldown = .08f; race.Dash(new Point(1, 0)); race.save.health = race.save.maxHealth;
            Advance(race, .08f); Check(race.dashTime == .18f && HealingLabels(race).Count == 0, "Buffered recovery rechecks actual health on execution when another source filled it first");
            for (int denial = 0; denial < 3; denial++)
            {
                GameCore game = HealingGame(data); game.save.relics[0] = 0;
                if (denial == 0) game.save.energy = 19; if (denial == 1) game.dashCooldown = .3f; if (denial == 2) game.mode = "pause";
                string before = ForgeState(game); game.Dash(new Point(1, 0));
                Check(ForgeState(game) == before && game.dashTime == 0 && HealingLabels(game).Count == 0, "Rejected dash conditions preserve health, resources and feedback");
            }
            GameCore plain = HealingGame(data); plain.Dash(new Point(1, 0)); Check(plain.save.health == 20 && HealingLabels(plain).Count == 0, "A dash without the relic adds no invented recovery");
        }

        public static void HealingMiraCommandKeepsTheOriginalRingAndLabelsItsRealContactDelta(Campaign data)
        {
            foreach (int bond in new[] { 0, 5 }) foreach (float missing in new[] { 100f, 2.25f, 0f })
            {
                GameCore game = HealingMira(data, bond); game.save.health = game.save.maxHealth - missing; float health = game.save.health;
                game.Notify("原有剧情", "Original story"); string toast = game.toast; game.CommandCompanion(); ActorAction action = game.companionAction;
                game.save.relationships[0] = 5 - bond; Advance(game, .19f);
                Check(action != null && !action.released && game.save.health == health && game.effects.Count == 0,
                    "Mira's captured command bond and .20-second contact grant no early ring or green text");
                game.Tick(.01f, new Point()); float actual = game.save.health - health;
                Check(Math.Abs(actual - Math.Min(missing, 18 + 3 * bond)) < .0001f && action.released && game.guardTime == 3
                    && game.effects[0].kind == "heal" && game.effects[0].life == .6f && game.effects.Count == (actual > 0 ? 2 : 1),
                    "The mature ring stays first with its original duration; exactly one optional label follows the real clamped command gain");
                if (actual > 0) HealingOne(game, actual, game.Position); else Check(HealingLabels(game).Count == 0, "A full-health command retains its guard and ring without fake healing");
                Check(game.LastCompanionReceipt.actualHeal == actual && game.LastCompanionReceipt.bond == bond && game.toast == toast,
                    "The immutable tactical receipt shares the exact recovered value and does not replace story notifications");
                game.CommandCompanion(); Check(game.companionAction == action && game.save.health == health + actual, "Cooldown rejection does not manufacture a second command transaction");
                game.mode = "pause"; Advance(game, .3f); Check(action.elapsed == .2f && (actual == 0 || HealingLabels(game)[0].life == .7f), "Menus freeze the contact feedback without replaying it");
                game.Close(); Advance(game, .72f); Check(HealingLabels(game).Count == 0 && game.save.health == health + actual, "Remaining command animation and expiry never repeat recovery");
            }
            GameCore race = HealingMira(data, 5); race.CommandCompanion(); Advance(race, .19f); race.save.health = race.save.maxHealth;
            race.Tick(.01f, new Point()); Check(race.LastCompanionReceipt.actualHeal == 0 && HealingLabels(race).Count == 0 && race.effects.Count == 1 && race.effects[0].kind == "heal",
                "Healing to full before Mira's actual command contact cancels only the numeric claim, retaining the mature ring");
        }

        public static void HealingMiraAutomaticRecoveryUsesActualContactAndExpiresWithoutPhantomLabels(Campaign data)
        {
            foreach (int bond in new[] { 0, 5 }) foreach (int recovery in new[] { 0, 1, 2 })
            {
                GameCore game = HealingMira(data, bond); game.save.health = 20;
                for (int tick = 0; tick < 60 && game.companionAction == null; tick++) game.Tick(.02f, new Point());
                ActorAction action = game.companionAction;
                Check(action != null && action.kind == "heal" && action.contact == .16f && action.combo == bond && game.save.health == 20 && game.effects.Count == 0,
                    "Mira's genuine low-health AI creates its existing .16-second cast without early recovery or generic +HP");
                game.save.relationships[0] = 5 - bond; Advance(game, .14f);
                Check(!action.released && game.effects.Count == 0 && game.save.health == 20, "The automatic heal remains wholly unannounced before its actual contact");
                if (recovery == 1) game.save.health = game.save.maxHealth;
                if (recovery == 2) game.save.health = game.save.maxHealth - .001f;
                float health = game.save.health; game.Tick(.02f, new Point()); float actual = game.save.health - health;
                Check(action.released && Math.Abs(actual - Math.Min(game.save.maxHealth - health, 6 + bond)) < .0001f
                    && game.effects.Count == (actual > 0 ? 1 : 0), "Auto-heal uses its captured bond and current health capacity at contact, replacing the old imprecise +HP effect");
                if (actual > 0) HealingOne(game, actual, game.Position); else Check(HealingLabels(game).Count == 0, "A race to full health suppresses the obsolete auto-heal claim");
                Check(!game.effects.Exists(effect => effect.kind == "number" && effect.text == "+HP") && game.LastCompanionReceipt == null,
                    "Automatic recovery neither leaves the old generic +HP label nor fabricates a commanded-cast tactical receipt");
                Advance(game, .72f); Check(HealingLabels(game).Count == 0 && game.save.health == health + actual && game.companionAction == null,
                    "Automatic cast recovery and expiration remove the numeric effect without re-healing on later animation frames");
            }
            GameCore full = HealingMira(data, 0); full.save.health = full.save.maxHealth; Advance(full, 1);
            Check(full.companionAction == null && full.effects.Count == 0 && full.save.health == full.save.maxHealth, "Full-health idle Mira never starts a fake automatic healing transaction");
        }
    }
}
