using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly string[] TownHandoffActions = { "plant", "ally1", "ally2", "ally3", "engine" };
        private static int TownHandoffItem(string action)
        { return action == "plant" ? 7 : action == "ally1" ? 2 : action == "ally2" ? 3 : 4; }
        private static int TownHandoffCost(string action)
        { return action == "plant" ? 1 : action == "ally1" ? 6 : action == "ally2" || action == "engine" ? 3 : 5; }
        private static string TownHandoffCompletion(string action)
        { return action == "engine" ? "gate_3" : action.StartsWith("ally", StringComparison.Ordinal) ? "ally_" + action.Substring(4) : null; }
        private static GameCore TownHandoffGame(Campaign data, string action = "plant", int profession = 0)
        {
            var game = Fresh(data, profession); game.SkipTutorial();
            game.save.inventory = new[] { 31, 29, 23, 17, 19, 2, 11, 13 };
            game.save.coins = 157; game.save.health = 37; game.save.energy = 29;
            game.save.totalTime = 40; game.save.foodUntil = 80; game.save.teaUntil = 100;
            game.save.heat = 17; game.save.cold = 23; game.save.mealKind = 2; game.save.mealUntil = 150;
            if (action == "engine") { game.save.stage = 2; game.Enter(3); game.enemies.Clear(); }
            return game;
        }
        private static int TownHandoffOpen(GameCore game, string action)
        {
            game.Close(); WorldObject host = Object(game, action == "plant" || action == "harvest" || action == "ally1" ? "garden"
                : action == "ally2" ? "fish" : action == "ally3" ? "forge" : "engine");
            PreparationStand(game, host); game.Interact();
            Check(game.mode == "dialog" && game.conversation != null, "The actual local E interaction opens the existing " + action + " handoff");
            int index = Array.FindIndex(game.conversation.choices, choice => choice.action == action);
            Check(index >= 0, "The authored numbered action remains offered: " + action); return index;
        }
        private static object TownHandoffQuote(GameCore game, string action)
        {
            MethodInfo method = typeof(GameCore).GetMethod("GetTownHandoffQuote", BindingFlags.Public | BindingFlags.Instance);
            Check(method != null, "The live handoff quote API exists after the actual shortage behavior has been exercised");
            return method.Invoke(game, new object[] { action });
        }
        private static T TownHandoffField<T>(object quote, string name)
        {
            Check(quote != null, "A recognized handoff returns its immutable quote");
            FieldInfo field = quote.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Check(field != null && field.IsInitOnly, "Quote field is public and immutable: " + name);
            return (T)field.GetValue(quote);
        }
        private static bool TownHandoffAvailable(object quote)
        {
            Check(quote != null, "Availability belongs to a known handoff quote");
            PropertyInfo property = quote.GetType().GetProperty("available");
            Check(property != null && !property.CanWrite, "Availability is a readonly quote property");
            return (bool)property.GetValue(quote, null);
        }
        private static string TownHandoffLabel(GameCore game, Choice choice)
        {
            MethodInfo method = typeof(GameCore).GetMethod("DialogueChoiceLabel", BindingFlags.Public | BindingFlags.Instance);
            Check(method != null, "The common dialogue label API exists");
            return (string)method.Invoke(game, new object[] { choice });
        }
        private static void TownHandoffActualShortage(Campaign data)
        {
            // This assertion precedes every new API read in the quote suite.
            // The archived baseline compiles and fails real Choose behavior,
            // rather than failing because a newly named type is absent.
            var game = TownHandoffGame(data, "ally1"); game.save.inventory[2] = 5;
            int index = TownHandoffOpen(game, "ally1"); Conversation dialogue = game.conversation;
            string before = SmithSaveState(game.save); int revision = game.revision;
            game.Choose(index);
            Check(game.mode == "dialog" && ReferenceEquals(game.conversation, dialogue)
                && SmithSaveState(game.save) == before && game.revision == revision,
                "A real short six-herb handoff retains the same conversation without closing, charging or rewarding");
        }
        private static void TownHandoffExpected(SaveData expected, string action)
        {
            expected.inventory[TownHandoffItem(action)] -= TownHandoffCost(action);
            if (action == "plant") expected.plantedAt = expected.totalTime;
            else
            {
                expected.flags.Add(TownHandoffCompletion(action));
                if (action != "engine") { expected.coins += 40; expected.inventory[6]++; expected.skillPoints++; }
            }
        }

        public static void TownHandoffQuotesAndLabelsAreLiveImmutableReads(Campaign data)
        {
            TownHandoffActualShortage(data);
            foreach (string action in TownHandoffActions) foreach (int stock in new[] { 0, TownHandoffCost(action) - 1, TownHandoffCost(action), TownHandoffCost(action) + 1, 9999 })
            {
                var game = TownHandoffGame(data, action); game.save.inventory[TownHandoffItem(action)] = stock;
                Choice choice = new Choice("原选项", "Original choice", action);
                foreach (string mode in new[] { "world", "dialog", "inventory", "pause", "plans", "materials" })
                {
                    game.mode = mode;
                    foreach (bool english in new[] { false, true })
                    {
                        game.save.english = english; string before = PlanStatsState(game);
                        object quote = TownHandoffQuote(game, action); string label = TownHandoffLabel(game, choice);
                        Check(quote.GetType().IsSealed && TownHandoffField<string>(quote, "action") == action
                            && TownHandoffField<int>(quote, "item") == TownHandoffItem(action)
                            && TownHandoffField<int>(quote, "required") == TownHandoffCost(action)
                            && TownHandoffField<int>(quote, "stock") == stock
                            && TownHandoffField<int>(quote, "missing") == Math.Max(0, TownHandoffCost(action) - stock)
                            && TownHandoffAvailable(quote) == (stock >= TownHandoffCost(action))
                            && game.DialogueChoiceEnabled(action) == (stock >= TownHandoffCost(action)),
                            "Each original handoff exposes live exact material/count/readiness without changing its cost: " + action);
                        Check(!string.IsNullOrEmpty(label) && label.Contains(stock + "/" + TownHandoffCost(action))
                            && (stock >= TownHandoffCost(action) || label.Contains((TownHandoffCost(action) - stock).ToString()))
                            && PlanStatsState(game) == before,
                            "Both languages show live stock/need and shortage without paying, healing, moving or changing any public state");
                        game.save.inventory[TownHandoffItem(action)] = stock + 1;
                        Check(TownHandoffField<int>(quote, "stock") == stock
                            && TownHandoffField<int>(TownHandoffQuote(game, action), "stock") == stock + 1,
                            "An old quote stays immutable while a new quote follows actual inventory");
                        game.save.inventory[TownHandoffItem(action)] = stock;
                    }
                }
            }
            var unrelated = TownHandoffGame(data);
            foreach (string action in new[] { null, "", "Plant", "ally0", "ally4", "ally1_extra", "harvest", "close", "cook", "watergarden_finish", "supper_accept" })
            {
                string before = PlanStatsState(unrelated);
                Check(TownHandoffQuote(unrelated, action) == null && PlanStatsState(unrelated) == before,
                    "Unrelated and malformed actions acquire no new town material requirement");
            }
            var prepared = TownHandoffGame(data); prepared.save.x = 12; prepared.save.y = 14; prepared.Attack(new Point(1, 0));
            ActorAction contact = prepared.playerAction; Check(contact != null && !contact.released, "Read-only quote fixture contains a genuinely prepared attack");
            string captured = PlanStatsState(prepared);
            foreach (string action in TownHandoffActions)
            { TownHandoffQuote(prepared, action); TownHandoffLabel(prepared, new Choice("原选项", "Original", action)); }
            Check(PlanStatsState(prepared) == captured && ReferenceEquals(contact, prepared.playerAction),
                "Reading town requirements never cancels, pays for or rewrites a captured combat action");
        }

        public static void TownHandoffRejectsShortStaleMalformedAndRepeatedChoicesInPlace(Campaign data)
        {
            TownHandoffActualShortage(data);
            foreach (string action in TownHandoffActions)
            {
                for (int invalid = 0; invalid < 8; invalid++)
                {
                    var game = TownHandoffGame(data, action); int index = TownHandoffOpen(game, action);
                    object displayed = TownHandoffQuote(game, action); Check(TownHandoffAvailable(displayed), "The original displayed offer starts affordable");
                    if (invalid == 0) game.save.inventory[TownHandoffItem(action)] = 0;
                    if (invalid == 1) game.save.inventory[TownHandoffItem(action)] = TownHandoffCost(action) - 1;
                    if (invalid == 2) game.save.inventory[TownHandoffItem(action)] = -1;
                    if (invalid == 3) game.save.inventory = null;
                    if (invalid == 4) game.save.inventory = new int[0];
                    if (invalid == 5) game.save.inventory = new int[7];
                    if (invalid == 6) game.save.inventory = new int[9];
                    if (invalid == 7)
                    { if (action == "plant") game.save.plantedAt = game.save.totalTime; else game.save.flags.Add(TownHandoffCompletion(action)); }
                    string before = SmithSaveState(game.save); int revision = game.revision; Conversation conversation = game.conversation;
                    Check(!game.DialogueChoiceEnabled(action) && !TownHandoffAvailable(TownHandoffQuote(game, action))
                        && TownHandoffAvailable(displayed), "Current stock/completion overrides an old immutable affordable quote: " + action + "/" + invalid);
                    for (int replay = 0; replay < 3; replay++) game.Choose(index);
                    Check(game.mode == "dialog" && ReferenceEquals(game.conversation, conversation)
                        && SmithSaveState(game.save) == before && game.revision == revision,
                        "Short, malformed and already-completed callbacks stay in the same dialogue without costs/rewards: " + action + "/" + invalid);
                    if (invalid < 7)
                    {
                        game.save.inventory = new[] { 31, 29, 23, 17, 19, 2, 11, 13 };
                        game.save.inventory[TownHandoffItem(action)] = TownHandoffCost(action);
                        Check(game.DialogueChoiceEnabled(action), "The retained numbered choice becomes usable after a genuine complete material deposit");
                        SaveData expected = CloneSave(game.save); TownHandoffExpected(expected, action); game.Choose(index);
                        Check(game.mode == "world" && SmithSaveState(game.save) == SmithSaveState(expected),
                            "The retained callback performs precisely one original transaction after stock recovers");
                    }
                    else
                    {
                        int close = Array.FindIndex(game.conversation.choices, choice => choice.action == "close");
                        Check(close >= 0 && game.DialogueChoiceEnabled("close"), "The original cancel remains available after stale completion");
                        game.Choose(close); Check(game.mode == "world" && SmithSaveState(game.save) == before, "Cancel leaves completed progress and remaining materials intact");
                    }
                }
            }
        }

        public static void TownHandoffPreservesAllFiveOriginalPaymentsAndRewards(Campaign data)
        {
            foreach (string action in TownHandoffActions) for (int profession = 0; profession < 3; profession++) foreach (bool exact in new[] { false, true })
            {
                var game = TownHandoffGame(data, action, profession);
                if (exact) game.save.inventory[TownHandoffItem(action)] = TownHandoffCost(action);
                int index = TownHandoffOpen(game, action); Check(game.DialogueChoiceEnabled(action), "Original fully affordable handoffs remain available");
                SaveData expected = CloneSave(game.save); TownHandoffExpected(expected, action); game.Choose(index);
                Check(game.mode == "world" && SmithSaveState(game.save) == SmithSaveState(expected),
                    "The real original transaction keeps every cost, reward, crop time, story flag, health, equipment and unrelated field: " + action);
                string paid = SmithSaveState(game.save); int revision = game.revision; game.Choose(index);
                Check(SmithSaveState(game.save) == paid && game.revision == revision, "A stale outside-dialog callback cannot pay or reward a second time");
                if (action == "plant")
                {
                    int harvest = 5 + game.save.projects[0] * 2 + (game.save.frontierEnding == 1 ? 2 : 0);
                    int herbs = game.save.inventory[2], seeds = game.save.inventory[7], coins = game.save.coins;
                    game.save.totalTime += 60; int gather = TownHandoffOpen(game, "harvest");
                    // The original planting menu now deliberately offers harvest.
                    // Open the real garden instead of fabricating a stale plant.
                    game.Choose(gather);
                    Check(game.save.plantedAt < 0 && game.save.inventory[2] == herbs + harvest
                        && game.save.inventory[7] == seeds + 2 && game.save.coins == coins + 8,
                        "A successful seed handoff still leads to the complete original crop yield");
                }
            }
        }

        public static void TownHandoffLegacySavesRetainTownFollowupsAndUnrelatedChoices(Campaign data)
        {
            TownHandoffActualShortage(data);
            foreach (int version in new[] { 1, 2 })
            {
                var original = TownHandoffGame(data); original.save.flags.Add("town_handoff_saved_story");
                original.save.projects = new[] { 2, 1, 2 }; original.save.reputation = new[] { 2, 3, 4 };
                original.save.kills[1] = 7; original.save.trackedQuest = "main";
                original.PrepareSave(); SaveData checkpoint = CloneSave(original.save); checkpoint.version = version;
                var game = new GameCore(data, checkpoint); game.Start();
                foreach (string action in TownHandoffActions)
                {
                    if (action == "engine") { game.save.stage = 2; game.Enter(3); game.enemies.Clear(); }
                    int index = TownHandoffOpen(game, action); SaveData expected = CloneSave(game.save); TownHandoffExpected(expected, action); game.Choose(index);
                    Check(SmithSaveState(game.save) == SmithSaveState(expected), "A migrated v" + version + " save pays each original handoff exactly once");
                    game.PrepareSave(); game = new GameCore(data, CloneSave(game.save)); game.Start();
                    Check(game.save.version == 2 && game.Has("town_handoff_saved_story") && game.save.projects[0] == 2
                        && game.save.projects[2] == 2 && game.save.reputation[1] == 3 && game.save.kills[1] == 7,
                        "Save round trips preserve earned mature town, route and independent story progress");
                }
                Check(game.Allies == 3 && game.Has("gate_3"), "All original commissions and repaired elevator survive the actual saved handoffs");
                game.Enter(0); game.enemies.Clear();
                Check(game.WaterGardenReady && game.SupperReady, "Earned original requests still unlock both independent mature town follow-ups");
                WaterGardenAccept(game); WaterGardenDraw(game); WaterGardenWater(game); WaterGardenFinish(game);
                Check(game.WaterGardenComplete, "The full existing water-garden physical dialogue chain remains usable after the new material gate");
                SupperAccept(game); SupperPrepareAll(game); SupperCook(game);
                for (int place = 0; place < 3; place++) SupperServe(game, place);
                SupperFinish(game); Check(game.SupperComplete, "The full earned shared-supper story remains usable without extra material handoffs");
                game.PrepareSave(); var resumed = new GameCore(data, CloneSave(game.save)); resumed.Start();
                Check(resumed.WaterGardenComplete && resumed.SupperComplete && resumed.Allies == 3 && resumed.Has("gate_3")
                    && resumed.Has("town_handoff_saved_story"), "Completed independent stories and original request flags persist together without schema changes");
                foreach (string action in new[] { "close", "harvest", "fish", "supper_menu", "watergarden_offer", "drinktea", "rest", "routes" })
                {
                    Choice choice = new Choice("原选项", "Original choice", action); string before = PlanStatsState(resumed);
                    Check(resumed.DialogueChoiceEnabled(action) && TownHandoffQuote(resumed, action) == null
                        && TownHandoffLabel(resumed, choice) == resumed.SupplyServiceChoiceLabel(choice)
                        && PlanStatsState(resumed) == before, "Unrelated story and free actions retain their exact original label/readiness: " + action);
                }
                foreach (string action in SupplyServiceActions)
                {
                    Choice choice = new Choice("原选项", "Original choice", action);
                    Check(resumed.DialogueChoiceEnabled(action) == resumed.GetSupplyServiceQuote(action).available
                        && TownHandoffLabel(resumed, choice) == resumed.SupplyServiceChoiceLabel(choice),
                        "The original paid supply-service label and readiness stay delegated to their mature rules");
                }
            }
        }
    }
}
