using System;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static object GardenHarvestRead(GameCore game)
        {
            MethodInfo method = typeof(GameCore).GetMethod("GetGardenHarvestQuote", Type.EmptyTypes);
            Check(method != null, "The garden exposes a fresh read-only harvest quote");
            return method.Invoke(game, null);
        }
        private static T GardenHarvestValue<T>(object quote, string name)
        {
            Check(quote != null, "The garden has a quote before reading " + name);
            FieldInfo field = quote.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Check(field != null && field.IsInitOnly && field.FieldType == typeof(T), "Garden field is immutable and typed: " + name);
            return (T)field.GetValue(quote);
        }
        private static string GardenHarvestSave(GameCore game)
        { var text = new StringBuilder(); PlanStatsAppend(text, game.save); return text.ToString(); }
        private static GameCore GardenHarvestGame(Campaign data, int profession = 0)
        {
            GameCore game = Fresh(data,profession); game.SkipTutorial(); game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear();
            game.save.companion = -1; game.save.totalTime = 100; game.save.plantedAt = -1;
            game.save.inventory[2] = 11; game.save.inventory[7] = 5; game.save.coins = 100;
            return game;
        }
        private static void GardenHarvestPlant(GameCore game)
        {
            int seeds = game.save.inventory[7], herbs = game.save.inventory[2], coins = game.save.coins;
            Action(game,"garden","plant");
            Check(game.save.plantedAt == game.save.totalTime && game.save.inventory[7] == seeds-1
                && game.save.inventory[2] == herbs && game.save.coins == coins, "Actual planting consumes exactly one seed without paying a harvest");
        }
        private static void GardenHarvestPay(GameCore game, string label)
        {
            object quote = GardenHarvestRead(game);
            Check(GardenHarvestValue<bool>(quote,"ready") && GardenHarvestValue<bool>(quote,"canHarvest"), label + " is ripe and fits");
            Action(game,"garden","harvest");
            Check(game.save.inventory[2] == GardenHarvestValue<long>(quote,"herbsAfter")
                && game.save.inventory[7] == GardenHarvestValue<long>(quote,"seedsAfter")
                && game.save.coins == GardenHarvestValue<long>(quote,"coinsAfter") && game.save.plantedAt == -1,
                label + " pays the complete quoted result through the original dialogue transaction");
            string paid = GardenHarvestSave(game); Action(game,"garden","plant");
            Check(game.save.inventory[2] == GardenHarvestValue<long>(quote,"herbsAfter")
                && game.save.coins == GardenHarvestValue<long>(quote,"coinsAfter") && game.save.plantedAt >= 0,
                label + " returns to normal planting instead of paying the old crop twice");
            Check(GardenHarvestSave(game) != paid, "A fresh planting is a genuinely separate one-seed action");
        }

        public static void GardenHarvestFullStockRetainsRipeCropAndPaysAfterRoom(Campaign data)
        {
            foreach (string blocker in new[] {"herbs","seeds","coins"})
            {
                GameCore game = GardenHarvestGame(data); GardenHarvestPlant(game); game.save.totalTime = 160;
                if (blocker == "herbs") game.save.inventory[2] = 9995;
                if (blocker == "seeds") game.save.inventory[7] = 9998;
                if (blocker == "coins") game.save.coins = 999992;
                string before = GardenHarvestSave(game); int revision = game.revision;
                // Run the real legacy transaction before requesting any new API.
                // Archived production must compile and expose the lost-crop defect.
                Action(game,"garden","harvest");
                Check(GardenHarvestSave(game) == before && game.save.plantedAt == 100 && game.revision == revision,
                    "Actual harvest must retain its ripe crop and every reward when " + blocker + " would exceed save capacity");
                object quote = GardenHarvestRead(game);
                Check(GardenHarvestValue<bool>(quote,"ready") && !GardenHarvestValue<bool>(quote,"canHarvest")
                    && !string.IsNullOrEmpty(GardenHarvestValue<string>(quote,"reasonZh"))
                    && !string.IsNullOrEmpty(GardenHarvestValue<string>(quote,"reasonEn")), "A ripe " + blocker + " blocker explains retained rewards");
                game.PrepareSave(); game = new GameCore(data,CloneSave(game.save)); game.Start(); game.Close();
                Check(game.save.plantedAt == 100 && !GardenHarvestValue<bool>(GardenHarvestRead(game),"canHarvest"),
                    "A blocked " + blocker + " crop survives actual save reconstruction");
                if (blocker == "herbs") { game.save.inventory[1] = 3; Action(game,"cook","tea"); }
                if (blocker == "coins") { Action(game,"garden","seeds"); Action(game,"garden","seeds"); }
                if (blocker == "seeds")
                {
                    WaterGardenVisit(game,"gardener"); WaterGardenChoose(game,"garden_seed_menu");
                    WaterGardenChoose(game,"garden_return3");
                    Check(game.save.inventory[7] == 9995 && game.save.plantedAt == 100,
                        "An's explicit seed return creates actual harvest capacity without replacing the occupied bed");
                }
                GardenHarvestPay(game,"Retained " + blocker + " crop after capacity is available");
                game.PrepareSave(); var loaded = new GameCore(data,CloneSave(game.save));
                Check(loaded.save.inventory[2] == game.save.inventory[2] && loaded.save.inventory[7] == game.save.inventory[7]
                    && loaded.save.coins == game.save.coins, "Successful formerly blocked " + blocker + " rewards persist without clipping");
            }
            GameCore stale = GardenHarvestGame(data); GardenHarvestPlant(stale); stale.save.totalTime = 160;
            object old = GardenHarvestRead(stale); Check(GardenHarvestValue<bool>(old,"canHarvest"), "An earlier read can fit before stock changes");
            stale.save.coins = 999999; string full = GardenHarvestSave(stale); Action(stale,"garden","harvest");
            Check(GardenHarvestSave(stale) == full && stale.save.plantedAt == 100, "Actual harvest rechecks current stock rather than trusting stale ready feedback");
            GardenHarvestSeedReturnRequiresLiveConfirmation(data);
        }

        private static bool GardenHarvestSeedCall(GameCore game, string methodName)
        {
            MethodInfo method = typeof(GameCore).GetMethod(methodName, Type.EmptyTypes);
            Check(method != null && method.ReturnType == typeof(bool), "Explicit seed-return API exists: " + methodName);
            return (bool)method.Invoke(game,null);
        }
        private static void GardenHarvestSeedReturnRequiresLiveConfirmation(Campaign data)
        {
            GameCore game = GardenHarvestGame(data); GardenHarvestPlant(game); game.save.totalTime = 160; game.save.inventory[7] = 9999;
            WaterGardenStand(game,"gardener"); string initial = GardenHarvestSave(game);
            Check(!GardenHarvestSeedCall(game,"ReturnGardenSeeds") && GardenHarvestSave(game) == initial,
                "A direct world call cannot discard seeds without opening and confirming a review");
            Check(GardenHarvestSeedCall(game,"ShowGardenSeedReturn") && game.mode == "dialog" && GardenHarvestSave(game) == initial,
                "Opening a seed-return review is read-only even beside a ripe blocked crop");
            Check(game.conversation.choices.Length == 2 && game.conversation.choices[0].action == "garden_back"
                && game.conversation.choices[1].action == "garden_return3", "Default review confirmation keeps seeds; disposal requires its separate explicit choice");
            WaterGardenChoose(game,"garden_back");
            Check(GardenHarvestSave(game) == initial && !GardenHarvestSeedCall(game,"ReturnGardenSeeds"),
                "Cancelling through the safe default preserves all stock and invalidates the live confirmation");
            WaterGardenChoose(game,"garden_seed_menu"); game.Close();
            Check(!GardenHarvestSeedCall(game,"ReturnGardenSeeds") && GardenHarvestSave(game) == initial,
                "Closing a review cannot leave a world-call seed disposal authorization");
            WaterGardenReplay(game,"garden_return3");
            Check(GardenHarvestSave(game) == initial && !GardenHarvestSeedCall(game,"ReturnGardenSeeds"),
                "A replayed action string in an unrelated conversation cannot discard seeds");
            game.Close(); WaterGardenVisit(game,"gardener"); WaterGardenChoose(game,"garden_seed_menu");
            SaveData expected = CloneSave(game.save); expected.inventory[7] -= 3;
            var expectedText = new StringBuilder(); PlanStatsAppend(expectedText, expected);
            WaterGardenChoose(game,"garden_return3");
            Check(GardenHarvestSave(game) == expectedText.ToString() && game.save.plantedAt == 100
                && game.mode == "dialog" && Array.Exists(game.conversation.choices,c => c.action == "garden_seed_menu"),
                "Explicit return removes exactly three seeds, pays nothing, preserves the crop and returns to An's original services");
            string after = GardenHarvestSave(game);
            Check(!GardenHarvestSeedCall(game,"ReturnGardenSeeds") && GardenHarvestSave(game) == after,
                "A consumed confirmation cannot pay or discard seeds a second time");
            game.save.inventory[7] = 2; WaterGardenChoose(game,"garden_seed_menu"); string low = GardenHarvestSave(game);
            WaterGardenChoose(game,"garden_return3");
            Check(!GardenHarvestSeedCall(game,"ReturnGardenSeeds") && GardenHarvestSave(game) == low,
                "Insufficient stock is rechecked at confirmation and never creates negative seeds");
            game.Close(); game.save.room = 1;
            Check(!GardenHarvestSeedCall(game,"ShowGardenSeedReturn"), "Seed disposal cannot open remotely away from An");
        }

        public static void GardenHarvestQuotesMatchEveryYieldAndExactCapacity(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) for (int canal = 0; canal < 3; canal++) for (int ending = 0; ending < 4; ending++)
            {
                GameCore game = GardenHarvestGame(data,profession); game.save.projects[0] = canal; game.save.frontierEnding = ending;
                GardenHarvestPlant(game); game.save.totalTime = 160; int herbs = 5+2*canal+(ending == 1 ? 2 : 0);
                game.save.inventory[2] = 9999-herbs; game.save.inventory[7] = 9997; game.save.coins = 999991;
                object quote = GardenHarvestRead(game);
                Check(GardenHarvestValue<int>(quote,"herbs") == herbs && GardenHarvestValue<int>(quote,"seeds") == 2 && GardenHarvestValue<int>(quote,"coins") == 8,
                    "Every class, canal level and ending shows the original full crop yield: " + profession + "/" + canal + "/" + ending);
                Check(GardenHarvestValue<int>(quote,"herbsBefore") == 9999-herbs && GardenHarvestValue<int>(quote,"seedsBefore") == 9997
                    && GardenHarvestValue<int>(quote,"coinsBefore") == 999991 && GardenHarvestValue<long>(quote,"herbsAfter") == 9999
                    && GardenHarvestValue<long>(quote,"seedsAfter") == 9999 && GardenHarvestValue<long>(quote,"coinsAfter") == 999999,
                    "Exact save-cap balances are accurately projected and remain claimable");
                GardenHarvestPay(game,"Exact-cap yield " + profession + "/" + canal + "/" + ending);
                Check(game.save.profession == profession && game.save.weapon == profession && game.save.projects[0] == canal && game.save.frontierEnding == ending,
                    "Harvest preview does not change identity, equipment, project tiers or ending");
            }
            GameCore changed = GardenHarvestGame(data); GardenHarvestPlant(changed); changed.save.totalTime = 160;
            object captured = GardenHarvestRead(changed); changed.save.projects[0] = 2; changed.save.frontierEnding = 1; changed.save.inventory[2] = 30;
            object fresh = GardenHarvestRead(changed);
            Check(GardenHarvestValue<int>(captured,"herbs") == 5 && GardenHarvestValue<int>(captured,"herbsBefore") == 11
                && GardenHarvestValue<int>(fresh,"herbs") == 11 && GardenHarvestValue<int>(fresh,"herbsBefore") == 30,
                "Captured quotes remain immutable and fresh reads use current canal, ending and inventory");
            GardenHarvestPay(changed,"Live eleven-herb yield after canal and ending change");
        }

        public static void GardenHarvestGrowthClockAndReadOnlyQuotesPreservePaidActions(Campaign data)
        {
            GameCore game = GardenHarvestGame(data); object empty = GardenHarvestRead(game);
            Check(!GardenHarvestValue<bool>(empty,"planted") && !GardenHarvestValue<bool>(empty,"ready") && !GardenHarvestValue<bool>(empty,"canHarvest"),
                "An empty bed cannot present a payable harvest");
            GardenHarvestPlant(game); game.save.totalTime = 159.99f;
            object growing = GardenHarvestRead(game);
            Check(GardenHarvestValue<bool>(growing,"planted") && !GardenHarvestValue<bool>(growing,"ready")
                && GardenHarvestValue<float>(growing,"remaining") > 0 && GardenHarvestValue<float>(growing,"remaining") < .011f,
                "The original sixty-second maturity boundary remains strict at 59.99 seconds");
            string before = GardenHarvestSave(game); Action(game,"garden","harvest");
            Check(GardenHarvestSave(game) == before, "Checking an immature crop never charges or pays rewards");
            game.Close(); game.Tick(.02f,new Point());
            Check(GardenHarvestValue<bool>(GardenHarvestRead(game),"ready") && GardenHarvestValue<float>(GardenHarvestRead(game),"remaining") == 0,
                "A real world Tick crosses the original maturity clock");
            game.save.totalTime = 160;
            Check(GardenHarvestValue<bool>(GardenHarvestRead(game),"ready"), "Exactly sixty seconds is ripe");
            game.save.energy = 100; game.Attack(new Point(1,0)); ActorAction accepted = game.playerAction;
            Check(accepted != null && !accepted.released, "Read-only fixture contains a genuinely paid unreleased attack");
            foreach (string mode in new[] {"world","dialog","inventory","pause"})
            {
                game.mode = mode; string state = PlanStatsState(game);
                for (int read = 0; read < 5; read++) GardenHarvestRead(game);
                Check(PlanStatsState(game) == state && ReferenceEquals(accepted,game.playerAction),
                    "Repeated harvest reads preserve save, simulation clock, accepted action and owner in " + mode);
                float clock = game.save.totalTime; game.Tick(.02f,new Point());
                if (mode != "world") Check(game.save.totalTime == clock, "Paused " + mode + " keeps the existing crop clock frozen");
            }
        }

        public static void GardenHarvestLegacyCropsAndFirstHarvestWitnessStaySafe(Campaign data)
        {
            foreach (int version in new[] {1,2}) foreach (bool ripe in new[] {false,true})
            {
                GameCore source = GardenHarvestGame(data,2); source.save.projects[0] = 2; source.save.frontierEnding = 1;
                GardenHarvestPlant(source); source.save.totalTime = ripe ? 160 : 130; source.save.flags.Add("legacy_crop_marker"); source.PrepareSave();
                SaveData checkpoint = CloneSave(source.save); checkpoint.version = version;
                GameCore loaded = new GameCore(data,checkpoint); loaded.Start(); loaded.Close();
                object quote = GardenHarvestRead(loaded);
                Check(loaded.save.plantedAt == 100 && loaded.save.totalTime == source.save.totalTime && loaded.Has("legacy_crop_marker")
                    && GardenHarvestValue<bool>(quote,"ready") == ripe && GardenHarvestValue<float>(quote,"remaining") == (ripe ? 0 : 30),
                    "Legacy v" + version + " restores the existing " + (ripe ? "ripe" : "growing") + " crop without inventing a new timer");
                if (!ripe) { loaded.save.totalTime = 160; }
                GardenHarvestPay(loaded,"Restored v" + version + " crop");
                loaded.PrepareSave(); var again = new GameCore(data,CloneSave(loaded.save));
                Check(again.save.inventory[2] == loaded.save.inventory[2] && again.save.inventory[7] == loaded.save.inventory[7]
                    && again.save.coins == loaded.save.coins && again.Has("legacy_crop_marker"), "Paid legacy crop rewards and unrelated flags survive round-trip");
            }
            GameCore story = FirstHarvestGame(data); FirstHarvestAccept(story); GardenHarvestPlant(story); story.save.totalTime = story.save.plantedAt+60;
            story.save.inventory[2] = 9999; FirstHarvestVisit(story,"gardener"); string full = GardenHarvestSave(story);
            WaterGardenChoose(story,"harvest");
            Check(GardenHarvestSave(story) == full && !FirstHarvestRead(story,"Harvested") && story.save.plantedAt >= 0,
                "Rejected full-stock harvest cannot advance First Harvest or erase the accepted physical crop");
            story.save.inventory[1] = 2; Action(story,"cook","tea"); Action(story,"cook","tea"); Action(story,"cook","tea");
            // This fixture needs enough herbs consumed for the current tier's original yield.
            story.save.inventory[1] = 10; while (!GardenHarvestValue<bool>(GardenHarvestRead(story),"canHarvest")) Action(story,"cook","tea");
            FirstHarvestVisit(story,"gardener"); WaterGardenChoose(story,"harvest");
            Check(FirstHarvestRead(story,"Harvested") && !FirstHarvestRead(story,"Packed") && !FirstHarvestRead(story,"Complete") && story.save.plantedAt == -1,
                "Only the complete actual paid harvest advances the accepted First Harvest witness, without skipping its later story handoffs");
        }
    }
}
