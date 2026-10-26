using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Reflection keeps this suite compilable against the previous production
        // revision, so --source-root can prove the feature fails there.
        private static object PreparationCall(GameCore game, string method, params object[] args)
        {
            MethodInfo entry = typeof(GameCore).GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            Check(entry != null, "Production must expose " + method);
            return entry.Invoke(game, args);
        }
        private static T PreparationValue<T>(object quote, string name)
        {
            FieldInfo field = quote.GetType().GetField(name);
            if (field != null) return (T)field.GetValue(quote);
            PropertyInfo property = quote.GetType().GetProperty(name);
            Check(property != null, "A supply quote exposes " + name);
            return (T)property.GetValue(quote, null);
        }
        private static object PreparationQuote(GameCore game, int recipe, int quantity)
        { return PreparationCall(game, "GetSupplyBatchQuote", recipe, quantity); }
        private static bool PreparationCraft(GameCore game, int recipe, int quantity)
        { return (bool)PreparationCall(game, "CraftSupplyBatch", recipe, quantity); }
        private static int PreparationMaximum(GameCore game, int recipe)
        { return (int)PreparationCall(game, "MaximumSupplyBatch", recipe); }
        private static bool PreparationEnabled(GameCore game, string action)
        { return (bool)PreparationCall(game, "DialogueChoiceEnabled", action); }
        private static void PreparationStand(GameCore game, WorldObject host)
        {
            for (int y = -7; y <= 7; y++) for (int x = -7; x <= 7; x++)
            {
                Point point = new Point(host.x + x * .2f, host.y + y * .2f);
                if (!game.CanStand(point)) continue;
                game.save.x = point.x; game.save.y = point.y;
                if (game.Nearest() == host) return;
            }
            throw new Exception("Supply preparation needs a real standable E approach to " + host.kind);
        }
        private static GameCore PreparationGame(Campaign data, int room = 0, bool english = true)
        {
            var game = Unlocked(data); game.SkipTutorial(); game.Enter(room); game.Close(); game.enemies.Clear();
            game.save.english = english; game.save.inventory[2] = 40; game.save.inventory[3] = 20;
            game.save.inventory[1] = 20; game.save.inventory[6] = 3; game.save.tea = 2;
            PreparationStand(game, Object(game, room == 0 || room == 9 ? "cook" : "campfire"));
            return game;
        }
        private static string PreparationState(GameCore game)
        {
            return LandmarkDescribe(game.save) + "/" + game.revision + "/" + LandmarkDescribe(game.enemies)
                + "/" + LandmarkDescribe(game.projectiles) + "/" + LandmarkDescribe(game.hazards)
                + "/" + LandmarkDescribe(game.effects);
        }
        private static string PreparationReadState(GameCore game)
        {
            return PreparationState(game) + "/" + game.mode + "/" + game.sound + "/" + game.toast
                + "/" + game.toastTime + "/" + LandmarkDescribe(game.conversation);
        }
        private static int PreparationChoice(GameCore game, string action)
        {
            Check(game.mode == "dialog" && game.conversation != null, "Supply choices belong to an actual conversation");
            for (int i = 0; i < game.conversation.choices.Length; i++)
                if (game.conversation.choices[i].action == action) return i;
            throw new Exception("Missing actual supply preparation choice " + action);
        }
        private static void PreparationChoose(GameCore game, string action)
        { game.Choose(PreparationChoice(game, action)); }
        private static void PreparationInteract(GameCore game)
        { game.Close(); game.Interact(); Check(game.mode == "dialog", "Actual E interaction opens the current nearest service"); }
        private static void PreparationPrefix(GameCore game, params string[] actions)
        {
            Check(game.conversation.choices.Length == actions.Length + 1, "Supply preparation appends exactly one original-host entry");
            for (int i = 0; i < actions.Length; i++)
                Check(game.conversation.choices[i].action == actions[i], "Original numbered action remains in place: " + actions[i]);
            Check(game.conversation.choices[actions.Length].action == "supply_prepare", "The new entry follows all established host choices");
        }

        public static void SupplyPreparationQuotesAreImmutablePureAndExposeExactSharedCosts(Campaign data)
        {
            foreach (bool english in new[] { false, true }) for (int recipe = 0; recipe < 2; recipe++)
            {
                var game = PreparationGame(data, 0, english); game.save.inventory[2] = 17;
                game.save.inventory[3] = 6; game.save.inventory[1] = 9;
                foreach (string mode in new[] { "world", "dialog" })
                {
                    game.mode = mode; string before = PreparationReadState(game);
                    object quote = PreparationQuote(game, recipe, 5);
                    Check(quote.GetType().Name == "SupplyBatchQuote" && quote.GetType().IsSealed, "Production returns the bounded supply quote type");
                    foreach (FieldInfo field in quote.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                        Check(field.IsInitOnly, "Supply quote fields remain immutable snapshots: " + field.Name);
                    Check(PreparationValue<int>(quote, "recipe") == recipe && PreparationValue<int>(quote, "quantity") == 5
                        && PreparationValue<int>(quote, "herbs") == 10 && PreparationValue<int>(quote, "fish") == (recipe == 0 ? 5 : 0)
                        && PreparationValue<int>(quote, "crystals") == (recipe == 1 ? 5 : 0)
                        && PreparationValue<int>(quote, "maxQuantity") == (recipe == 0 ? 6 : 8)
                        && PreparationMaximum(game, recipe) == (recipe == 0 ? 6 : 8) && PreparationValue<bool>(quote, "available"),
                        "Quotes expose full batch costs and a maximum limited by the same shared herbs");
                    Check(before == PreparationReadState(game), "Prices and maximum calculations cannot change saved, combat, dialogue or feedback state");
                    game.save.inventory[2] = 1; game.save.inventory[3] = 0; game.save.inventory[1] = 0;
                    before = PreparationReadState(game); object shortfall = PreparationQuote(game, recipe, 5);
                    string en = PreparationValue<string>(shortfall, "reasonEn").ToLowerInvariant();
                    string zh = PreparationValue<string>(shortfall, "reasonZh");
                    Check(!PreparationValue<bool>(shortfall, "available") && en.Contains("herb") && en.Contains("9")
                        && en.Contains(recipe == 0 ? "fish" : "crystal") && en.Contains("5") && !String.IsNullOrEmpty(zh),
                        "Both languages explain all exact shortages for the complete selected batch");
                    Check(PreparationValue<bool>(quote, "available") && PreparationValue<int>(quote, "maxQuantity") == (recipe == 0 ? 6 : 8),
                        "Changed stock cannot mutate a previously returned quote");
                    Check(before == PreparationReadState(game), "Even unsuccessful previews are read-only");
                    game.save.inventory[2] = 17; game.save.inventory[3] = 6; game.save.inventory[1] = 9;
                }
            }
        }

        public static void SupplyPreparationTransactionsPayOncePreserveBuffsAndRespectOutputCaps(Campaign data)
        {
            for (int recipe = 0; recipe < 2; recipe++) foreach (int quantity in new[] { 1, 5, 37 })
            {
                var game = PreparationGame(data); game.save.inventory[2] = 100; game.save.inventory[3] = 50; game.save.inventory[1] = 50;
                game.save.health = 47; game.save.energy = 23; game.save.heat = 21; game.save.cold = 32;
                game.save.foodUntil = game.save.totalTime + 70; game.save.teaUntil = game.save.totalTime + 80;
                game.save.mealKind = 2; game.save.mealUntil = game.save.totalTime + 120;
                SaveData expected = CloneSave(game.save); expected.inventory[2] -= quantity * 2;
                if (recipe == 0) { expected.inventory[3] -= quantity; expected.inventory[6] += quantity; }
                else { expected.inventory[1] -= quantity; expected.tea += quantity; }
                int revision = game.revision;
                Check(PreparationCraft(game, recipe, quantity), "A genuinely affordable batch crafts once");
                Check(LandmarkDescribe(game.save) == LandmarkDescribe(expected),
                    "Batch preparation changes only its exact ingredients and stored output; health, energy, climate, buffs, road meal, clock and story stay untouched");
                Check(game.revision == revision + 2 && game.sound == "reward" && game.toast.Contains(quantity.ToString()),
                    "One payment and one completion emit a quantity receipt and reward feedback");
            }
            for (int recipe = 0; recipe < 2; recipe++)
            {
                var game = PreparationGame(data); int cap = recipe == 0 ? 9999 : 999;
                if (recipe == 0) game.save.inventory[6] = cap - 2; else game.save.tea = cap - 2;
                Check(PreparationMaximum(game, recipe) == 2 && !PreparationValue<bool>(PreparationQuote(game, recipe, 5), "available"),
                    "Maximum and selected-batch readiness include remaining persistent output capacity");
                Check(PreparationCraft(game, recipe, 2) && (recipe == 0 ? game.save.inventory[6] : game.save.tea) == cap,
                    "The final two supplies fill the supported cap exactly");
                string before = PreparationState(game);
                Check(PreparationMaximum(game, recipe) == 0 && !PreparationCraft(game, recipe, 1) && PreparationState(game) == before,
                    "A full pack rejects repeat crafting without discarding ingredients or revising the save");
                game.PrepareSave(); var restored = new GameCore(data, CloneSave(game.save)); restored.Close();
                Check((recipe == 0 ? restored.save.inventory[6] : restored.save.tea) == cap,
                    "Full batch storage survives actual save preparation and reconstruction without being truncated");
            }
            var shared = PreparationGame(data); shared.save.inventory[2] = 10;
            Check(PreparationCraft(shared, 0, 5) && PreparationMaximum(shared, 1) == 0,
                "Cooking stew consumes the real herbs that the tea maximum also depends on");
        }

        public static void SupplyPreparationRejectsStaleInvalidAndUnauthorizedContextsAtomically(Campaign data)
        {
            foreach (int recipe in new[] { Int32.MinValue, -1, 2, Int32.MaxValue })
            {
                var game = PreparationGame(data); string before = PreparationReadState(game);
                Check(!PreparationValue<bool>(PreparationQuote(game, recipe, 1), "available") && PreparationMaximum(game, recipe) == 0
                    && before == PreparationReadState(game), "An invalid recipe is independently rejected even with a valid quantity, without query side effects");
                before = PreparationState(game);
                Check(!PreparationCraft(game, recipe, 1) && before == PreparationState(game), "No unsupported recipe can spend or produce inventory");
            }
            foreach (int recipe in new[] { -1, 0, 1, 2, Int32.MaxValue })
                foreach (int quantity in new[] { Int32.MinValue, -1, 0, 10000, Int32.MaxValue })
                {
                    var game = PreparationGame(data); string before = PreparationState(game);
                    Check(!PreparationValue<bool>(PreparationQuote(game, recipe, quantity), "available")
                        && !PreparationCraft(game, recipe, quantity) && before == PreparationState(game),
                        "Invalid recipe/quantity cannot overflow costs, partially spend, or grant output");
                }
            for (int recipe = 0; recipe < 2; recipe++) for (int stale = 0; stale < 3; stale++)
            {
                var game = PreparationGame(data); object quote = PreparationQuote(game, recipe, 5);
                Check(PreparationValue<bool>(quote, "available"), "The original batch was legitimately affordable");
                if (stale == 0) game.save.inventory[2] = 9;
                if (stale == 1) game.save.inventory[recipe == 0 ? 3 : 1] = 4;
                if (stale == 2) { if (recipe == 0) game.save.inventory[6] = 9995; else game.save.tea = 995; }
                string before = PreparationState(game);
                Check(!PreparationCraft(game, recipe, 5) && before == PreparationState(game) && PreparationValue<bool>(quote, "available"),
                    "Fresh payment rechecks each stale material/capacity condition without rewriting the old quote");
            }
            foreach (string mode in new[] { "dialog", "inventory", "pause", "journal", "camp", "title" })
            {
                var game = PreparationGame(data); game.mode = mode; string before = PreparationState(game);
                Check(!PreparationCraft(game, 0, 1) && !PreparationCraft(game, 1, 1) && before == PreparationState(game),
                    "Direct crafting is limited to world actions even when a dialog quote may be read");
            }
            for (int context = 0; context < 4; context++)
            {
                var game = PreparationGame(data);
                if (context == 0) PreparationStand(game, Object(game, "garden"));
                if (context == 1) game.practicing = true;
                if (context == 2) game.deathTime = .3f;
                if (context == 3) { game.StartAbyss(0); game.enemies.Clear(); }
                string before = PreparationState(game);
                Check(!PreparationValue<bool>(PreparationQuote(game, 0, 1), "available")
                    && !PreparationValue<bool>(PreparationQuote(game, 1, 1), "available")
                    && !PreparationCraft(game, 0, 1) && !PreparationCraft(game, 1, 1)
                    && !(bool)PreparationCall(game, "ShowSupplyPreparation") && before == PreparationState(game),
                    "Another nearest host, practice, death and the Abyss never authorize pantry production");
            }
            var camp = PreparationGame(data, 5); camp.save.projects[1] = 0;
            camp.enemies.Add(new Enemy(9147, 0, camp.save.x + 2, camp.save.y, 50));
            Check(!camp.SafeTravel && !PreparationCraft(camp, 0, 1) && PreparationCraft(camp, 1, 5),
                "Camp tea preserves its original no-waystation/no-safety requirement, while camp stew remains unavailable");
        }

        public static void SupplyPreparationRealInteractionKeepsOriginalChoicesAndRechecksDisabledSelections(Campaign data)
        {
            foreach (bool english in new[] { false, true }) foreach (int room in new[] { 0, 9, 5, 6, 7 })
            {
                var game = PreparationGame(data, room, english); PreparationInteract(game);
                if (room == 0 || room == 9) PreparationPrefix(game, "cook", "buymeal", "buywood", "tea", "close", "kitchen");
                else PreparationPrefix(game, "fieldrest", "tea", "drinktea", "companions", "close", "kitchen");
                PreparationChoose(game, "supply_prepare");
                Check(game.mode == "dialog" && game.conversation.choices.Length == 7, "The real appended entry opens six batch choices plus close");
                int maximum = PreparationMaximum(game, 1);
                string[] actions = { "supply_batch_0_1", "supply_batch_0_5", "supply_batch_0_" + PreparationMaximum(game, 0),
                    "supply_batch_1_1", "supply_batch_1_5", "supply_batch_1_" + maximum, "close" };
                for (int i = 0; i < actions.Length; i++) Check(game.conversation.choices[i].action == actions[i],
                    "Maximum quantities are captured in the actual numbered action, with stable recipe ordering");
                if (room != 0 && room != 9)
                    Check(!PreparationEnabled(game, "supply_batch_0_1") && !PreparationEnabled(game, "supply_batch_0_5"),
                        "The camp submenu exposes unavailable stew choices without authorizing them");
                int tea = game.save.tea; PreparationChoose(game, "supply_batch_1_5");
                Check(game.mode == "dialog" && game.save.tea == tea + 5 && game.conversation.choices.Length == 7,
                    "Actual selection crafts the requested batch and refreshes the same usable menu");
            }
            var stale = PreparationGame(data); stale.save.inventory[2] = 10;
            Check((bool)PreparationCall(stale, "ShowSupplyPreparation"), "Nearby world action can open supply preparation");
            int maxChoice = 5; string savedAction = stale.conversation.choices[maxChoice].action;
            Check(savedAction == "supply_batch_1_5", "Maximum action captures the offered five tea");
            stale.save.inventory[2] = 30; int priorTea = stale.save.tea;
            stale.Choose(maxChoice);
            Check(stale.save.tea == priorTea + 5, "Newly acquired herbs cannot silently increase the chosen maximum quantity");
            int disabled = PreparationChoice(stale, "supply_batch_0_5"); stale.save.inventory[3] = 0;
            Conversation conversation = stale.conversation; string beforeState = PreparationState(stale);
            Check(!PreparationEnabled(stale, "supply_batch_0_5"), "Fresh availability disables a newly stale displayed choice");
            stale.Choose(disabled);
            Check(stale.mode == "dialog" && System.Object.ReferenceEquals(stale.conversation, conversation) && PreparationState(stale) == beforeState,
                "A disabled numeric selection is rejected before closing its real conversation");
            Check(PreparationEnabled(stale, "close") && PreparationEnabled(stale, "fish") && !PreparationEnabled(stale, "cook")
                && PreparationEnabled(stale, "boon0") == stale.AbyssBoonChoiceEnabled("boon0"),
                "Combined availability retains Close and Abyss gates while refusing single stew with no fish");
            stale.save.inventory[3] = 1;
            Check(PreparationEnabled(stale, "cook"), "Restoring the original single-stew ingredient re-enables its existing action");
            stale.save.inventory[3] = 0;
            foreach (string malformed in new[] { "supply_batch_", "supply_batch_0_0", "supply_batch_0_-1", "supply_batch_2_1",
                "supply_batch_0_2147483648", "supply_batch_0_1_extra", "supply_batch_0_+1" })
            {
                string before = PreparationReadState(stale);
                Check(!PreparationEnabled(stale, malformed) && before == PreparationReadState(stale),
                    "Malformed, overflowing and out-of-range action quantities stay disabled without mutating the current menu");
            }
            var supper = PreparationGame(data); foreach (string flag in new[] { "ally_1", "ally_2", "ally_3" }) supper.save.flags.Add(flag);
            PreparationInteract(supper); PreparationPrefix(supper, "cook", "buymeal", "buywood", "tea", "close", "kitchen", "supper_menu");
            var homeward = HomewardGame(data); homeward.enemies.Clear(); PreparationStand(homeward, Object(homeward, "campfire"));
            PreparationInteract(homeward); PreparationPrefix(homeward, "fieldrest", "tea", "drinktea", "companions", "close", "kitchen", "homeward_camp");
            var commission = PreparationGame(data, 6);
            foreach (string flag in new[] { "personal_1", "commission_accepted", "commission_tested", "commission_reported", "commission_caravan" }) commission.save.flags.Add(flag);
            PreparationInteract(commission); PreparationPrefix(commission, "fieldrest", "tea", "drinktea", "companions", "close", "kitchen", "commission_supply");
        }

        public static void SupplyPreparationPersistenceAndLegacyRestShopSingleRecipesRemainIntact(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                var game = PreparationGame(data); game.save.version = version; game.save.projects[1] = 2;
                game.save.flags.Add("supply_legacy_story"); game.save.kills[1] = 7; game.save.coins = 176;
                int herbs = game.save.inventory[2], fish = game.save.inventory[3], crystal = game.save.inventory[1];
                Check(PreparationCraft(game, 0, 5) && PreparationCraft(game, 1, 5), "Legacy-version saves use real batch transactions without a new schema");
                game.PrepareSave(); var restored = new GameCore(data, CloneSave(game.save)); restored.Close();
                Check(restored.save.version == 2 && restored.save.inventory[6] == 8 && restored.save.tea == 7
                    && restored.save.inventory[2] == herbs - 20 && restored.save.inventory[3] == fish - 5
                    && restored.save.inventory[1] == crystal - 5 && restored.Has("supply_legacy_story")
                    && restored.save.coins == 176 && restored.save.projects[1] == 2 && restored.save.kills[1] == 7,
                    "Stored batch output and original paid materials survive v1/v2 reconstruction without changing story or funds");
            }
            var shop = PreparationGame(data); shop.save.coins = 100;
            foreach (int trust in new[] { 0, 3 })
            {
                shop.save.reputation[1] = trust; int coins = shop.save.coins, stew = shop.save.inventory[6];
                PreparationInteract(shop); PreparationChoose(shop, "buymeal");
                Check(shop.save.coins == coins - (trust == 3 ? 15 : 18) && shop.save.inventory[6] == stew + 1,
                    "The original shop action retains its ordinary or Ferrymen discount price");
            }
            int oldStew = shop.save.inventory[6], oldHerbs = shop.save.inventory[2], oldFish = shop.save.inventory[3];
            PreparationInteract(shop); PreparationChoose(shop, "cook");
            Check(shop.mode == "world" && shop.save.inventory[6] == oldStew + 1 && shop.save.inventory[2] == oldHerbs - 2
                && shop.save.inventory[3] == oldFish - 1, "The original stew action still crafts exactly one and closes normally");
            var camp = PreparationGame(data, 5); int oldTea = camp.save.tea; oldHerbs = camp.save.inventory[2];
            int oldCrystal = camp.save.inventory[1]; PreparationInteract(camp); PreparationChoose(camp, "tea");
            Check(camp.mode == "world" && camp.save.tea == oldTea + 1 && camp.save.inventory[2] == oldHerbs - 2
                && camp.save.inventory[1] == oldCrystal - 1, "The original camp tea action keeps its single-item cost and mode behavior");
            camp.save.projects[1] = 0; string beforeRest = PreparationState(camp);
            PreparationInteract(camp); PreparationChoose(camp, "fieldrest");
            Check(beforeRest == PreparationState(camp), "An unbuilt waystation still cannot recover or advance the camp rest clock");
            camp.save.projects[1] = 1; camp.enemies.Add(new Enemy(9148, 0, camp.save.x + 2, camp.save.y, 50));
            beforeRest = PreparationState(camp); PreparationInteract(camp); PreparationChoose(camp, "fieldrest");
            Check(beforeRest == PreparationState(camp), "Nearby enemies still block waystation rest while brewing remains a separate action");
            camp.enemies.Clear(); oldTea = camp.save.tea; float oldTime = camp.save.totalTime;
            PreparationInteract(camp); PreparationChoose(camp, "fieldrest");
            Check(camp.save.totalTime == oldTime + 20 && camp.save.tea == oldTea, "Tier-I rest advances its original clock without granting tier-II daily tea");
            camp.save.projects[1] = 2; camp.save.totalTime = 100; camp.save.health = 33; camp.save.energy = 14;
            camp.save.heat = 22; camp.save.cold = 44; oldTea = camp.save.tea; int day = camp.Day;
            PreparationInteract(camp); PreparationChoose(camp, "fieldrest");
            Check(camp.save.health == camp.save.maxHealth && camp.save.energy == 100 && camp.save.heat == 0 && camp.save.cold == 0
                && camp.save.totalTime == 120 && camp.save.tea == oldTea + 1 && camp.Has("camptea_" + day),
                "Waystation rest retains recovery, twenty-second clock advance and its single daily tea");
            PreparationInteract(camp); PreparationChoose(camp, "fieldrest");
            Check(camp.save.totalTime == 140 && camp.save.tea == oldTea + 1, "Repeated same-day rest cannot duplicate the waystation tea");
        }
    }
}
