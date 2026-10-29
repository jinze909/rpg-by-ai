using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void IngredientNeeds(GameCore game, string action, params int[] triples)
        {
            string before = PlanStatsState(game); SupplyIngredientNeed[] needs = game.SupplyIngredientNeeds();
            Check(needs.Length * 3 == triples.Length, "Shortages list only missing ingredients of the first blocked recipe: " + action);
            for (int i = 0; i < needs.Length; i++)
                Check(needs[i].action == action && needs[i].item == triples[i * 3] && needs[i].required == triples[i * 3 + 1]
                    && needs[i].held == triples[i * 3 + 2] && needs[i].missing == needs[i].required - needs[i].held && needs[i].missing > 0,
                    "Each shortage keeps its exact original recipe quantity, held stock and deficit: " + action);
            Check(PlanStatsState(game) == before, "A shortage query never changes saves, dialog, actions, queues or feedback");
        }

        public static void SupplyIngredientsMatchFirstBlockedRecipeAndCapturedBatchQuantities(Campaign data)
        {
            Check(typeof(SupplyIngredientNeed).IsSealed, "Shortage rows are immutable snapshots");
            foreach (FieldInfo field in typeof(SupplyIngredientNeed).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public shortage row field is immutable: " + field.Name);
            foreach (bool english in new[] { false, true }) foreach (int room in new[] { 0, 9 })
            {
                var game = PreparationGame(data, room, english); game.save.inventory[2] = 1;
                game.save.inventory[3] = 0; game.save.inventory[1] = 0; PreparationInteract(game);
                IngredientNeeds(game, "cook", 2, 2, 1, 3, 1, 0);
                SupplyIngredientNeed[] earlier = game.SupplyIngredientNeeds();
                game.save.inventory[2] = 2; game.save.inventory[3] = 1;
                IngredientNeeds(game, "tea", 1, 1, 0);
                Check(earlier[0].held == 1 && earlier[0].missing == 1 && earlier[1].held == 0,
                    "A later stock change does not rewrite an earlier shortage snapshot");
                game.save.inventory[1] = 1; IngredientNeeds(game, "");
            }
            var batches = PreparationGame(data); batches.save.inventory[2] = 3; batches.save.inventory[3] = 2;
            batches.save.inventory[1] = 0; Check(batches.ShowSupplyPreparation(), "The original pantry menu opens near its cook");
            IngredientNeeds(batches, "supply_batch_0_5", 2, 10, 3, 3, 5, 2);
            // The stored x5 action must remain x5 even if the separate Max row changes.
            batches.save.inventory[2] = 8; batches.save.inventory[3] = 3;
            IngredientNeeds(batches, "supply_batch_0_5", 2, 10, 8, 3, 5, 3);
            batches.save.inventory[2] = 10; batches.save.inventory[3] = 5;
            IngredientNeeds(batches, "supply_batch_1_1", 1, 1, 0);
            batches.save.inventory[6] = 9999; batches.save.inventory[2] = 4; batches.save.inventory[1] = 1;
            IngredientNeeds(batches, "supply_batch_1_5", 2, 10, 4, 1, 5, 1);
            Check(batches.conversation.choices[1].action == "supply_batch_0_5" && batches.conversation.choices[4].action == "supply_batch_1_5",
                "Reading shortages preserves all authored batch controls and quantities");
        }

        public static void SupplyIngredientsExcludeCapacityCoinsInvalidAndUnauthorizedOffers(Campaign data)
        {
            var ready = PreparationGame(data); PreparationInteract(ready); IngredientNeeds(ready, "");
            ready.save.coins = 0; IngredientNeeds(ready, "");
            ready.save.inventory[6] = 9999; ready.save.tea = 999;
            ready.save.inventory[2] = ready.save.inventory[3] = ready.save.inventory[1] = 0;
            IngredientNeeds(ready, "");
            ready.Close(); Check(ready.ShowSupplyPreparation(), "Capacity-blocked preparation still opens its existing menu");
            IngredientNeeds(ready, "");
            ready.conversation.choices = new[] { ready.conversation.choices[2], ready.conversation.choices[5], ready.conversation.choices[6] };
            IngredientNeeds(ready, "");
            ready.save.inventory[6] = ready.save.tea = 0;
            foreach (string action in new[] { null, "", "supply_batch_", "supply_batch_0_0", "supply_batch_0_-1", "supply_batch_2_1",
                "supply_batch_0_10000", "supply_batch_0_2147483648", "supply_batch_0_+1", "supply_batch_0_5_extra",
                "buymeal", "buywood", "seeds", "close", "fieldrest", "kitchen", "commission_supply" })
            {
                ready.conversation.choices = new[] { new Choice("原选项", "Original choice", action) };
                IngredientNeeds(ready, "");
            }
            foreach (int room in new[] { 5, 6, 7 })
            {
                var camp = PreparationGame(data, room); camp.save.inventory[2] = 1; camp.save.inventory[1] = 0;
                camp.save.inventory[3] = 0; PreparationInteract(camp);
                IngredientNeeds(camp, "tea", 2, 2, 1, 1, 1, 0);
                camp.Close(); Check(camp.ShowSupplyPreparation(), "The real camp pantry opens without adding stew authority");
                IngredientNeeds(camp, "supply_batch_1_1", 2, 2, 1, 1, 1, 0);
                camp.conversation.choices = new[] { camp.conversation.choices[0], camp.conversation.choices[1], camp.conversation.choices[2] };
                IngredientNeeds(camp, "");
            }
            foreach (string mode in new[] { "world", "pause", "map", "inventory", "materials", "fish", "title", "journal", "plans" })
            {
                var game = PreparationGame(data); game.save.inventory[2] = 0; PreparationInteract(game); game.mode = mode;
                IngredientNeeds(game, "");
            }
            for (int context = 0; context < 8; context++)
            {
                var game = PreparationGame(data); game.save.inventory[2] = 0; PreparationInteract(game);
                if (context == 0) game.practicing = true;
                if (context == 1) game.deathTime = .3f;
                if (context == 2) { game.StartAbyss(0); game.mode = "dialog"; }
                if (context == 3) PreparationStand(game, Object(game, "garden"));
                if (context == 4) { game.save.x = 1; game.save.y = 1; }
                if (context == 5) game.conversation = null;
                if (context == 6) game.conversation.choices = null;
                if (context == 7) game.conversation.choices = new Choice[] { null };
                IngredientNeeds(game, "");
            }
            for (int malformed = 0; malformed < 4; malformed++)
            {
                var game = PreparationGame(data); PreparationInteract(game);
                if (malformed == 0) game.save.inventory = null;
                if (malformed == 1) game.save.inventory = new int[1];
                if (malformed == 2) game.save.inventory[2] = -1;
                if (malformed == 3) game.save.tea = -1;
                IngredientNeeds(game, "");
            }
        }

        public static void SupplyIngredientsPreserveOriginalPaymentsAndDisabledDialogues(Campaign data)
        {
            foreach (string action in new[] { "cook", "tea" }) foreach (bool lastPortion in new[] { false, true })
            {
                var game = PreparationGame(data); if (lastPortion) SupplyServiceSetStock(game, action, action == "tea" ? 998 : 9998);
                PreparationInteract(game); IngredientNeeds(game, ""); SaveData expected = CloneSave(game.save);
                SupplyServiceExpectedPayment(expected, action, 0); PreparationChoose(game, action);
                Check(game.mode == "world" && SmithSaveState(game.save) == SmithSaveState(expected),
                    "A viewed single recipe retains its original exact payment, output, health, buffs and story: " + action);
            }
            for (int recipe = 0; recipe < 2; recipe++) foreach (int quantity in new[] { 1, 5 })
            {
                var game = PreparationGame(data); game.save.inventory[6] = recipe == 0 ? 9999 - quantity : 2;
                game.save.tea = recipe == 1 ? 999 - quantity : 2;
                Check(game.ShowSupplyPreparation(), "The existing batch menu opens for payment verification");
                // An unavailable larger alternative may be visible; it still cannot change this selected recipe.
                game.SupplyIngredientNeeds(); SaveData expected = CloneSave(game.save); expected.inventory[2] -= 2 * quantity;
                if (recipe == 0) { expected.inventory[3] -= quantity; expected.inventory[6] += quantity; }
                else { expected.inventory[1] -= quantity; expected.tea += quantity; }
                PreparationChoose(game, "supply_batch_" + recipe + "_" + quantity);
                Check(game.mode == "dialog" && SmithSaveState(game.save) == SmithSaveState(expected),
                    "Batch query never changes the requested old quantity, cost, full-portion output or retained menu");
            }
            var shortfall = PreparationGame(data); shortfall.save.inventory[2] = 1; shortfall.save.inventory[3] = 0;
            PreparationInteract(shortfall); IngredientNeeds(shortfall, "cook", 2, 2, 1, 3, 1, 0);
            Conversation dialog = shortfall.conversation; string before = SmithSaveState(shortfall.save);
            PreparationChoose(shortfall, "cook");
            Check(shortfall.mode == "dialog" && ReferenceEquals(shortfall.conversation, dialog) && SmithSaveState(shortfall.save) == before,
                "The displayed source shortcut cannot authorize or pay a still-disabled original recipe");
            shortfall.save.inventory[2] = 2; shortfall.save.inventory[3] = 1;
            SaveData paid = CloneSave(shortfall.save); SupplyServiceExpectedPayment(paid, "cook", 0);
            shortfall.SupplyIngredientNeeds(); PreparationChoose(shortfall, "cook");
            Check(SmithSaveState(shortfall.save) == SmithSaveState(paid), "Restoring materials re-enables the unchanged existing transaction exactly once");
        }

        public static void SupplyIngredientsStayPureAcrossCapturedActionsAndLegacyReloads(Campaign data)
        {
            var game = PreparationGame(data); game.save.inventory[2] = 0; game.Equip(1); game.Attack(new Point(1, 0));
            ActorAction paid = game.playerAction;
            Check(paid != null && !paid.released, "The query fixture captures a real paid, unreleased bow attack");
            PreparationInteract(game); Conversation conversation = game.conversation; SaveData owner = game.save;
            string before = PlanStatsState(game); ImpactProfile impact = paid.impact;
            for (int read = 0; read < 12; read++) IngredientNeeds(game, "cook", 2, 2, 0);
            Check(PlanStatsState(game) == before && ReferenceEquals(game.save, owner) && ReferenceEquals(game.conversation, conversation)
                && ReferenceEquals(game.playerAction, paid) && ReferenceEquals(paid.impact, impact) && !paid.released,
                "Repeated shortage reads do not pay, cancel, recapture, tick or heal an accepted action");
            foreach (int version in new[] { 1, 2 })
            {
                var original = PreparationGame(data); original.save.inventory[2] = 1; original.save.inventory[3] = 0;
                original.save.projects = new[] { 2, 1, 2 }; original.save.kills[1] = 7;
                foreach (string flag in new[] { "ingredients_legacy_story", "return_letter_complete", "archive_complete" }) original.save.flags.Add(flag);
                PreparationInteract(original); IngredientNeeds(original, "cook", 2, 2, 1, 3, 1, 0);
                original.PrepareSave(); SaveData old = CloneSave(original.save); old.version = version;
                var restored = new GameCore(data, old); restored.Start(); restored.Close();
                string restoredBefore = SmithSaveState(restored.save); IngredientNeeds(restored, "");
                PreparationStand(restored, Object(restored, "cook")); PreparationInteract(restored);
                IngredientNeeds(restored, "cook", 2, 2, 1, 3, 1, 0);
                Check(SmithSaveState(restored.save) == restoredBefore && restored.Has("ingredients_legacy_story")
                    && restored.Has("return_letter_complete") && restored.Has("archive_complete") && restored.save.projects[0] == 2
                    && restored.save.kills[1] == 7 && restored.save.version == 2,
                    "Old saves reconstruct fresh shortage rows from original stock only after reopening the actual service");
            }
        }
    }
}
