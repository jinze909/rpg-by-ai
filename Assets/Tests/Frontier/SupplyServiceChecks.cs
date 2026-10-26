using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly string[] SupplyServiceActions = { "cook", "tea", "buymeal", "buywood", "seeds" };
        private static int SupplyServiceItem(string action)
        { return action == "tea" ? -1 : action == "buywood" ? 0 : action == "seeds" ? 7 : 6; }
        private static int SupplyServiceQuantity(string action)
        { return action == "buywood" ? 4 : action == "seeds" ? 3 : 1; }
        private static int SupplyServiceStock(GameCore game, string action)
        { int item = SupplyServiceItem(action); return item < 0 ? game.save.tea : game.save.inventory[item]; }
        private static void SupplyServiceSetStock(GameCore game, string action, int amount)
        { int item = SupplyServiceItem(action); if (item < 0) game.save.tea = amount; else game.save.inventory[item] = amount; }
        private static GameCore SupplyServiceGame(Campaign data, int profession = 0)
        {
            var game = Fresh(data, profession); game.SkipTutorial(); game.Close();
            game.save.inventory = new[] { 31, 29, 23, 17, 19, 2, 11, 13 };
            game.save.coins = 157; game.save.tea = 7; game.save.health = 37; game.save.energy = 29;
            game.save.totalTime = 40; game.save.foodUntil = 80; game.save.teaUntil = 100;
            game.save.heat = 17; game.save.cold = 23; game.save.mealKind = 2; game.save.mealUntil = 150;
            return game;
        }
        private static int SupplyServiceOpen(GameCore game, string action)
        {
            game.Close(); WorldObject host = Object(game, action == "seeds" ? "garden" : "cook");
            PreparationStand(game, host); game.Interact();
            Check(game.mode == "dialog" && game.conversation != null, "The authored nearby supply service opens through the actual E interaction");
            for (int i = 0; i < game.conversation.choices.Length; i++)
                if (game.conversation.choices[i].action == action) return i;
            throw new Exception("The existing service must retain its authored action: " + action);
        }
        private static void SupplyServiceExpectedPayment(SaveData expected, string action, int reputation)
        {
            if (action == "cook") { expected.inventory[2] -= 2; expected.inventory[3]--; expected.inventory[6]++; }
            if (action == "tea") { expected.inventory[2] -= 2; expected.inventory[1]--; expected.tea++; }
            if (action == "buymeal") { expected.coins -= reputation >= 3 ? 15 : 18; expected.inventory[6]++; }
            if (action == "buywood") { expected.coins -= reputation >= 3 ? 8 : 10; expected.inventory[0] += 4; }
            if (action == "seeds") { expected.coins -= 5; expected.inventory[7] += 3; }
        }

        public static void SupplyServiceQuotesExposeLiveCostsAndCanonicalCapacity(Campaign data)
        {
            Check(typeof(SupplyServiceQuote).IsSealed, "Single-service prices are immutable snapshots rather than mutable payment authority");
            foreach (FieldInfo field in typeof(SupplyServiceQuote).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public single-service quote field is immutable: " + field.Name);
            foreach (string action in SupplyServiceActions) foreach (int reputation in new[] { 0, 2, 3, 10 })
            {
                var game = SupplyServiceGame(data); game.save.reputation[1] = reputation;
                foreach (string mode in new[] { "world", "dialog", "inventory", "pause", "plans", "materials" })
                {
                    game.mode = mode; string before = PlanStatsState(game);
                    SupplyServiceQuote quote = game.GetSupplyServiceQuote(action);
                    int coins = action == "buymeal" ? reputation >= 3 ? 15 : 18 : action == "buywood" ? reputation >= 3 ? 8 : 10 : action == "seeds" ? 5 : 0;
                    Check(quote != null && quote.action == action && quote.item == SupplyServiceItem(action)
                        && quote.quantity == SupplyServiceQuantity(action) && quote.stock == SupplyServiceStock(game, action)
                        && quote.limit == (action == "tea" ? 999 : 9999) && quote.available,
                        "The quote reports its exact existing service, stored output and canonical save capacity: " + action);
                    Check(quote.herbs == (action == "cook" || action == "tea" ? 2 : 0)
                        && quote.fish == (action == "cook" ? 1 : 0) && quote.crystals == (action == "tea" ? 1 : 0)
                        && quote.coins == coins, "Single recipes and Ferrymen price boundaries match the original transaction: " + action);
                    Choice choice = new Choice("原选项", "Original choice", action);
                    foreach (bool english in new[] { false, true })
                    {
                        game.save.english = english; string languageBefore = PlanStatsState(game);
                        string label = game.SupplyServiceChoiceLabel(choice);
                        Check(!string.IsNullOrEmpty(label) && PlanStatsState(game) == languageBefore,
                            "Both live labels are non-paying, non-healing reads: " + action);
                    }
                    game.save.english = false;
                    Check(PlanStatsState(game) == before, "Reading prices outside a service cannot mutate host, mode, world, feedback or save state");
                    int stock = quote.stock; SupplyServiceSetStock(game, action, stock + 1);
                    Check(quote.stock == stock && quote.available && game.GetSupplyServiceQuote(action).stock == stock + 1,
                        "Old displayed quotes stay immutable while a fresh quote follows current inventory");
                    SupplyServiceSetStock(game, action, stock);
                }
                int limit = action == "tea" ? 999 : 9999, quantity = SupplyServiceQuantity(action);
                SupplyServiceSetStock(game, action, limit - quantity);
                Check(game.GetSupplyServiceQuote(action).available, "The final complete original portion fits exactly: " + action);
                foreach (int stock in new[] { limit - quantity + 1, limit, int.MaxValue })
                {
                    SupplyServiceSetStock(game, action, stock); string before = PlanStatsState(game);
                    SupplyServiceQuote quote = game.GetSupplyServiceQuote(action);
                    Check(!quote.available && !string.IsNullOrEmpty(quote.reasonZh) && !string.IsNullOrEmpty(quote.reasonEn)
                        && PlanStatsState(game) == before, "Overflow and partial-capacity refusal are explicit pure reads: " + action);
                }
            }
            var unknown = SupplyServiceGame(data);
            foreach (string action in new[] { null, "", "close", "plant", "upgrade", "supply_batch_0_1", "Cook", "buywood_extra" })
            {
                string before = PlanStatsState(unknown);
                Check(unknown.GetSupplyServiceQuote(action) == null && PlanStatsState(unknown) == before,
                    "Unrelated or malformed actions do not acquire a single-supply payment quote");
            }
        }

        public static void SupplyServiceTransactionsPreserveRecipesYieldsAndReputationPrices(Campaign data)
        {
            foreach (string action in SupplyServiceActions) foreach (int reputation in new[] { 0, 2, 3, 10 })
                foreach (bool lastPortion in new[] { false, true })
                {
                    var game = SupplyServiceGame(data); game.save.reputation[1] = reputation;
                    if (lastPortion) SupplyServiceSetStock(game, action, (action == "tea" ? 999 : 9999) - SupplyServiceQuantity(action));
                    int choice = SupplyServiceOpen(game, action); SaveData expected = CloneSave(game.save);
                    SupplyServiceExpectedPayment(expected, action, reputation); game.Choose(choice);
                    Check(game.mode == "world" && SmithSaveState(game.save) == SmithSaveState(expected),
                        "A real numbered service pays only its complete original recipe/price and gives its full portion; it preserves health, buffs, clock and story: " + action);
                    int afterRevision = game.revision; string afterSave = SmithSaveState(game.save); game.Choose(choice);
                    Check(game.revision == afterRevision && SmithSaveState(game.save) == afterSave,
                        "A second stale Choose outside dialog cannot charge or produce again: " + action);
                    if (lastPortion)
                    {
                        int again = SupplyServiceOpen(game, action); Conversation retained = game.conversation; afterSave = SmithSaveState(game.save);
                        afterRevision = game.revision; game.Choose(again);
                        Check(game.mode == "dialog" && ReferenceEquals(game.conversation, retained)
                            && SmithSaveState(game.save) == afterSave && game.revision == afterRevision,
                            "A full pack cannot pay for a discarded extra portion: " + action);
                    }
                }
            var camp = PreparationGame(data, 5); camp.save.health = 33; camp.save.energy = 17;
            camp.save.foodUntil = camp.save.totalTime + 45; camp.save.teaUntil = camp.save.totalTime + 75;
            PreparationInteract(camp); int teaChoice = PreparationChoice(camp, "tea"); SaveData expectedCamp = CloneSave(camp.save);
            SupplyServiceExpectedPayment(expectedCamp, "tea", 0); camp.Choose(teaChoice);
            Check(camp.mode == "world" && SmithSaveState(camp.save) == SmithSaveState(expectedCamp),
                "Existing field-camp single tea keeps the same complete recipe without drinking, healing, resting or requiring an inn");
        }

        public static void SupplyServiceChoicesRetainDialogAndRejectChangedStock(Campaign data)
        {
            foreach (string action in SupplyServiceActions)
            {
                int checks = action == "cook" || action == "tea" ? 3 : 2;
                for (int invalid = 0; invalid < checks; invalid++)
                {
                    var game = SupplyServiceGame(data); int index = SupplyServiceOpen(game, action);
                    SupplyServiceQuote displayed = game.GetSupplyServiceQuote(action);
                    Check(displayed.available && game.DialogueChoiceEnabled(action), "The actual offer begins affordable before inventory changes");
                    if (invalid == 0) SupplyServiceSetStock(game, action, (action == "tea" ? 999 : 9999) - SupplyServiceQuantity(action) + 1);
                    else if (action == "cook" || action == "tea") game.save.inventory[invalid == 1 ? 2 : action == "cook" ? 3 : 1] = invalid == 1 ? 1 : 0;
                    else game.save.coins = displayed.coins - 1;
                    string readBefore = PlanStatsState(game); SupplyServiceQuote current = game.GetSupplyServiceQuote(action);
                    string label = game.SupplyServiceChoiceLabel(game.conversation.choices[index]);
                    Check(!current.available && !game.DialogueChoiceEnabled(action) && displayed.available
                        && !string.IsNullOrEmpty(current.reasonZh) && !string.IsNullOrEmpty(current.reasonEn) && !string.IsNullOrEmpty(label)
                        && PlanStatsState(game) == readBefore, "Fresh labels and disabled status recheck each live shortage/capacity without accepting the old quote");
                    Conversation conversation = game.conversation; string before = SmithSaveState(game.save); int revision = game.revision;
                    game.Choose(index);
                    Check(game.mode == "dialog" && ReferenceEquals(game.conversation, conversation)
                        && SmithSaveState(game.save) == before && game.revision == revision,
                        "An unaffordable or over-cap numbered selection retains its real conversation without partial charge, healing or reward: " + action);
                    game.save.coins = 157; game.save.inventory[2] = 23; game.save.inventory[3] = 17; game.save.inventory[1] = 29;
                    SupplyServiceSetStock(game, action, 0);
                    Check(game.DialogueChoiceEnabled(action), "The same retained original choice becomes enabled after a genuine deposit");
                    SaveData expected = CloneSave(game.save); SupplyServiceExpectedPayment(expected, action, 0); game.Choose(index);
                    Check(game.mode == "world" && SmithSaveState(game.save) == SmithSaveState(expected),
                        "Restoring stock permits exactly one original transaction through the retained choice");
                }
            }
            var zero = SupplyServiceGame(data); Array.Clear(zero.save.inventory, 0, zero.save.inventory.Length); zero.save.coins = zero.save.tea = 0;
            foreach (string action in SupplyServiceActions)
            {
                int index = SupplyServiceOpen(zero, action); string before = SmithSaveState(zero.save); Conversation conversation = zero.conversation;
                zero.Choose(index); Check(SmithSaveState(zero.save) == before && ReferenceEquals(zero.conversation, conversation) && zero.mode == "dialog",
                    "Empty materials and purse never create supplies, heal or consume a buff: " + action);
            }
            Check(zero.DialogueChoiceEnabled("close") && !zero.DialogueChoiceEnabled("plant") && zero.DialogueChoiceEnabled("drinktea")
                && zero.DialogueChoiceEnabled("boon0") == zero.AbyssBoonChoiceEnabled("boon0"),
                "Free story choices and existing Abyss readiness remain intact; planting now requires its actual seed");
            zero.save.inventory[7] = 1;
            Check(zero.DialogueChoiceEnabled("plant"), "The original plant action becomes available when a real seed is present");
            zero.save.inventory[7] = 0;
            string emptySave = SmithSaveState(zero.save); zero.Choose(PreparationChoice(zero, "close"));
            Check(zero.mode == "world" && SmithSaveState(zero.save) == emptySave,
                "The original goodbye still closes a rejected service without any payment or forced purchase");
            foreach (string action in new[] { "buymeal", "buywood" })
            {
                var changed = SupplyServiceGame(data); changed.save.english = true;
                int index = SupplyServiceOpen(changed, action); SupplyServiceQuote beforeTrust = changed.GetSupplyServiceQuote(action);
                changed.save.reputation[1] = 3; SupplyServiceQuote discounted = changed.GetSupplyServiceQuote(action);
                string before = PlanStatsState(changed), label = changed.SupplyServiceChoiceLabel(changed.conversation.choices[index]);
                Check(beforeTrust.coins == (action == "buymeal" ? 18 : 10) && discounted.coins == (action == "buymeal" ? 15 : 8)
                    && label.Contains(discounted.coins + " coins") && PlanStatsState(changed) == before,
                    "A still-open shop updates its displayed price when earned Ferrymen reputation changes");
                SaveData expected = CloneSave(changed.save); SupplyServiceExpectedPayment(expected, action, 3); changed.Choose(index);
                Check(SmithSaveState(changed.save) == SmithSaveState(expected),
                    "Selecting an old displayed action pays the fresh earned discount rather than its originally authored price");
            }
        }

        public static void SupplyServiceQuotesPreserveCapturedCombatAndLegacySaves(Campaign data)
        {
            foreach (int version in new[] { 1, 2 }) foreach (string action in SupplyServiceActions)
            {
                var original = SupplyServiceGame(data); original.save.projects = new[] { 2, 1, 2 };
                original.save.reputation = new[] { 2, 3, 4 }; original.save.kills[1] = 7;
                foreach (string flag in new[] { "supply_legacy_story", "weapon_1", "weapon_2", "relic_1", "relic_4", "return_letter_complete", "archive_complete" })
                    original.save.flags.Add(flag);
                original.save.relics = new[] { 1, 4 }; original.save.relicRank = 2; original.save.mastery = new[] { 23, 64, 87 };
                original.save.weaponStyles = new[] { 2, 1, 2 }; original.mode = "plans";
                Check(original.SaveBattlePlan(0), "The legacy fixture retains a genuinely saved earned equipment plan");
                original.PrepareSave(); SaveData old = CloneSave(original.save); old.version = version;
                var game = new GameCore(data, old); game.Start(); string before = PlanStatsState(game);
                foreach (string service in SupplyServiceActions) game.GetSupplyServiceQuote(service);
                Check(PlanStatsState(game) == before, "Fresh pricing cannot modify migrated v" + version + " story, gear, food or economy");
                int index = SupplyServiceOpen(game, action); SaveData expected = CloneSave(game.save);
                SupplyServiceExpectedPayment(expected, action, 3); game.Choose(index);
                Check(SmithSaveState(game.save) == SmithSaveState(expected), "The migrated save pays the current original recipe exactly once");
                game.PrepareSave(); SaveData paid = CloneSave(game.save); var resumed = new GameCore(data, paid); resumed.Start();
                Check(resumed.save.version == 2 && resumed.save.inventory[0] == expected.inventory[0]
                    && resumed.save.inventory[1] == expected.inventory[1] && resumed.save.inventory[2] == expected.inventory[2]
                    && resumed.save.inventory[3] == expected.inventory[3] && resumed.save.inventory[6] == expected.inventory[6]
                    && resumed.save.inventory[7] == expected.inventory[7] && resumed.save.tea == expected.tea && resumed.save.coins == expected.coins
                    && resumed.save.health == expected.health && resumed.save.energy == expected.energy
                    && resumed.save.foodUntil == expected.foodUntil && resumed.save.teaUntil == expected.teaUntil
                    && resumed.save.projects[0] == 2 && resumed.save.projects[2] == 2 && resumed.save.kills[1] == 7
                    && resumed.Has("supply_legacy_story") && resumed.Has("return_letter_complete") && resumed.Has("archive_complete")
                    && resumed.save.relics[0] == expected.relics[0] && resumed.save.relics[1] == expected.relics[1] && resumed.save.mastery[2] == 87
                    && resumed.save.weaponStyles[2] == 2 && resumed.BattlePlanAt(0) != null,
                    "PrepareSave plus actual reconstruction preserves paid output, costs, active consumables and independent mature progress in v" + version);
            }
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var game = SupplyServiceGame(data, weapon); game.Enter(1); game.enemies.Clear(); game.Close();
                game.save.x = 12; game.save.y = 14; game.Equip(weapon); game.invulnerable = 0;
                var enemy = new Enemy(98710 + weapon, 0, weapon == 0 ? 13.1f : 17, 14, 500) { stun = 99 };
                game.enemies.Add(enemy); game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
                Check(action != null && !action.released, "The original weapon really captures an attack before quote reads");
                string before = PlanStatsState(game); float damage = action.damage; ImpactProfile impact = action.impact;
                for (int repeat = 0; repeat < 8; repeat++) foreach (string service in SupplyServiceActions)
                {
                    game.GetSupplyServiceQuote(service); game.DialogueChoiceEnabled(service);
                    game.SupplyServiceChoiceLabel(new Choice("补给", "Supply", service));
                }
                Check(PlanStatsState(game) == before && ReferenceEquals(game.playerAction, action) && ReferenceEquals(action.impact, impact)
                    && action.damage == damage && !action.released,
                    "Repeated availability and live-price reads cannot spend stamina, consume supplies, heal, advance RNG or recapture an accepted attack");
                for (int frame = 0; frame < 400 && enemy.hp == enemy.maxHp; frame++) game.Tick(.01f, new Point());
                Check(action.released && enemy.hp < enemy.maxHp && action.damage == damage && ReferenceEquals(action.impact, impact),
                    "The original captured sword, arrow or starfire still reaches physical contact unchanged");
            }
        }
    }
}
