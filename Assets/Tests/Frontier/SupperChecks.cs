using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    // Exercise the real town/inn conversation transactions offline. Native
    // scene, Player and audiovisual acceptance remain separate gates.
    public static partial class CoreChecks
    {
        private static readonly string[] supperResidents = { "garden", "fish", "forge" };
        private static readonly int[][] supperOrders = {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
        };
        private static GameCore SupperGame(Campaign data, int profession = 0, bool accepted = false)
        {
            var g = Fresh(data, profession); g.SkipTutorial();
            for (int i = 1; i <= 3; i++) g.save.flags.Add("ally_" + i);
            if (accepted) SupperAccept(g);
            return g;
        }
        private static WorldObject SupperStand(GameCore g, string kind)
        {
            g.Close(); WorldObject o = Object(g, kind);
            Point[] offsets = { new Point(0, .8f), new Point(.8f, 0), new Point(-.8f, 0), new Point(0, -.8f) };
            foreach (Point offset in offsets)
                if (g.CanStand(new Point(o.x, o.y) + offset))
                { g.save.x = o.x + offset.x; g.save.y = o.y + offset.y; return o; }
            throw new Exception("Supper interaction lacks real walkable footing: " + kind);
        }
        private static int SupperChoiceIndex(GameCore g, string action)
        {
            if (g.conversation != null)
                for (int i = 0; i < g.conversation.choices.Length; i++)
                    if (g.conversation.choices[i].action == action) return i;
            return -1;
        }
        private static void SupperChoose(GameCore g, string action)
        {
            int index = SupperChoiceIndex(g, action);
            Check(g.mode == "dialog" && index >= 0, "The real local conversation offers " + action);
            g.Choose(index);
        }
        private static void SupperOpen(GameCore g, string kind, string action)
        { WorldObject o = SupperStand(g, kind); g.Talk(o); SupperChoose(g, action); }
        private static void SupperAccept(GameCore g)
        {
            if (g.save.room != 0) g.Enter(0);
            SupperOpen(g, "cook", "supper_menu"); SupperChoose(g, "supper_accept"); g.Close();
            Check(g.SupperAccepted, "The earned Ari conversation accepts the optional shared meal");
        }
        private static void SupperPrepare(GameCore g, int index)
        {
            if (g.save.room != 0) g.Enter(0);
            SupperOpen(g, supperResidents[index], "supper_prep_" + index);
            SupperChoose(g, "supper_help_" + index); g.Close();
            Check(g.SupperPrepared(index), "A real nearby resident contributes to the supper: " + index);
        }
        private static void SupperPrepareAll(GameCore g)
        { for (int i = 0; i < 3; i++) SupperPrepare(g, i); }
        private static void SupperCook(GameCore g)
        {
            g.Enter(9); SupperOpen(g, "cook", "supper_menu"); SupperChoose(g, "supper_cook"); g.Close();
            Check(g.SupperCooked, "All resident preparations permit cooking at the actual inn kitchen");
        }
        private static void SupperServe(GameCore g, int index)
        {
            SupperOpen(g, "supperBowl" + index, "supper_serve_" + index); g.Close();
            Check(g.SupperServed(index), "A nearby distinct table receives its actual bowl: " + index);
        }
        private static void SupperFinish(GameCore g)
        { SupperOpen(g, "cook", "supper_menu"); SupperChoose(g, "supper_finish"); g.Close(); }
        private static void SupperReplay(GameCore g, string action)
        {
            // A hostile/stale UI callback must pass the same physical ownership
            // checks as a genuine choice; this fixture cannot grant authority.
            g.conversation = new Conversation("stale", "stale", "stale", "stale", new Choice("stale", "stale", action));
            g.mode = "dialog"; g.Choose(0);
        }
        private static string SupperEconomy(GameCore g)
        {
            SaveData s = g.save;
            return string.Join(",", s.inventory) + "|" + s.coins + "|" + s.shards + "|" + s.xp + "|" + s.level + "|" +
                s.skillPoints + "|" + s.tea + "|" + s.health + "|" + s.maxHealth + "|" + s.energy + "|" + s.weapon + "|" +
                s.profession + "|" + s.upgrades + "|" + s.totalTime + "|" + s.foodUntil + "|" + s.teaUntil + "|" +
                s.mealKind + "|" + s.mealUntil + "|" + string.Join(",", s.relationships) + "|" + string.Join(",", s.reputation) +
                "|" + string.Join(",", s.talents) + "|" + string.Join(",", s.projects) + "|" + string.Join(",", s.mastery);
        }
        private static void SupperPrefix(GameCore g, string kind, string[] actions)
        {
            WorldObject o = SupperStand(g, kind); g.Talk(o);
            Check(g.conversation != null && g.conversation.choices.Length >= actions.Length, "Mature menu survives at " + kind);
            for (int i = 0; i < actions.Length; i++)
                Check(g.conversation.choices[i].action == actions[i], "Mature menu prefix keeps position/action " + kind + "/" + i);
            g.Close();
        }

        public static void SupperEarnedOfferPreservesOriginalCommissionsAndMenus(Campaign data)
        {
            var g = Fresh(data); g.SkipTutorial();
            Check(!g.SupperReady && !g.SupperAccepted && !g.SupperCooked && !g.SupperComplete && g.GetQuest("supper").locked,
                "A fresh hero cannot skip the three mature resident requests");
            for (int i = 0; i < 3; i++)
            {
                int coins = g.save.coins, points = g.save.skillPoints, stew = g.save.inventory[6];
                g.save.inventory[i == 0 ? 2 : i == 1 ? 3 : 4] = i == 0 ? 6 : i == 1 ? 3 : 5;
                SupperOpen(g, supperResidents[i], "ally" + (i + 1));
                Check(g.Has("ally_" + (i + 1)) && g.save.coins == coins + 40 && g.save.skillPoints == points + 1 && g.save.inventory[6] == stew + 1,
                    "The original resident request still pays its full exact independent reward: " + i);
                Check(g.SupperReady == (i == 2), "The optional meal unlocks only after all three earned requests");
            }
            SupperPrefix(g, "garden", new[] { "plant", "seeds", "close" });
            SupperPrefix(g, "fish", new[] { "fish", "close" });
            SupperPrefix(g, "forge", new[] { "upgrade", "bow", "staff", "close" });
            SupperPrefix(g, "cook", new[] { "cook", "buymeal", "buywood", "tea", "close", "kitchen" });
            string before = SupperEconomy(g); SupperAccept(g);
            Check(g.SupperReady && g.SupperAccepted && g.save.trackedQuest == "supper" && SupperEconomy(g) == before,
                "Accepting the town follow-up records and tracks it without charging or repaying the original requests");
            foreach (string missing in new[] { "ally_1", "ally_2", "ally_3" })
            {
                var locked = SupperGame(data); locked.save.flags.Remove(missing); SupperStand(locked, "cook");
                before = SupperEconomy(locked); SupperReplay(locked, "supper_accept");
                Check(!locked.SupperReady && !locked.SupperAccepted && SupperEconomy(locked) == before,
                    "A fabricated acceptance cannot bypass the absent original request: " + missing);
            }
        }

        public static void SupperAcceptanceAndPreparationRecheckPhysicalCallbacks(Campaign data)
        {
            for (int rejection = 0; rejection < 4; rejection++)
            {
                var g = SupperGame(data); SupperOpen(g, "cook", "supper_menu"); int choice = SupperChoiceIndex(g, "supper_accept");
                Check(choice >= 0, "A real earned acceptance is captured before the locality changes"); string before = SupperEconomy(g);
                if (rejection == 0) { g.save.x = 18; g.save.y = 15; }
                if (rejection == 1) { g.save.room = 1; g.save.x = 8; g.save.y = 18; }
                if (rejection == 2) g.practicing = true;
                if (rejection == 3) g.deathTime = .3f;
                g.Choose(choice);
                Check(!g.SupperAccepted && SupperEconomy(g) == before, "Stale local acceptance is rejected atomically: " + rejection);
            }
            foreach (int index in new[] { 0, 1, 2 })
            for (int rejection = 0; rejection < 4; rejection++)
            {
                var g = SupperGame(data, 0, true); SupperOpen(g, supperResidents[index], "supper_prep_" + index);
                int choice = SupperChoiceIndex(g, "supper_help_" + index); Check(choice >= 0, "The resident offers a separate deliberate preparation confirmation");
                string before = SupperEconomy(g);
                if (rejection == 0) { g.save.x = 18; g.save.y = 15; }
                if (rejection == 1) g.save.room = 9;
                if (rejection == 2) g.practicing = true;
                if (rejection == 3) g.deathTime = .3f;
                g.Choose(choice);
                Check(!g.SupperPrepared(index) && g.SupperPreparedCount == 0 && SupperEconomy(g) == before,
                    "Resident preparation rechecks actual current world/locality/life state: " + index + "/" + rejection);
            }
            var ready = SupperGame(data, 0, true); string unchanged = SupperEconomy(ready);
            for (int i = 0; i < 3; i++)
            {
                SupperPrepare(ready, i); SupperReplay(ready, "supper_help_" + i);
                Check(ready.SupperPreparedCount == i + 1 && SupperEconomy(ready) == unchanged,
                    "Residents share an earned contribution once without charging the hero again");
            }
            foreach (int invalid in new[] { -1, 3, 999 })
            {
                Check(!ready.SupperPrepared(invalid) && !ready.SupperServed(invalid), "Invalid public stage queries remain bounded");
                SupperReplay(ready, "supper_help_" + invalid); SupperReplay(ready, "supper_serve_" + invalid);
            }
            Check(ready.SupperPreparedCount == 3 && ready.SupperServedCount == 0 && SupperEconomy(ready) == unchanged,
                "Malformed callback indices cannot create progress, rewards or consume resources");
        }

        public static void SupperCookingServingAndFinishRequireActualOwnedStations(Campaign data)
        {
            var g = SupperGame(data, 0, true); string before = SupperEconomy(g);
            g.Enter(9); SupperStand(g, "cook"); SupperReplay(g, "supper_cook"); SupperReplay(g, "supper_finish");
            Check(!g.SupperCooked && !g.SupperComplete && SupperEconomy(g) == before,
                "The inn cannot cook incomplete contributions or finish an unserved meal");
            for (int i = 0; i < 3; i++) Check(!g.IsObjectPresent(Object(g, "supperBowl" + i)), "Uncooked tables never expose active bowls");
            SupperPrepareAll(g); SupperStand(g, "cook"); SupperReplay(g, "supper_cook");
            Check(!g.SupperCooked, "The exterior cook cannot substitute for the indoor shared table");
            g.Enter(9); SupperStand(g, "cook"); g.save.x = 18; g.save.y = 18; SupperReplay(g, "supper_cook");
            Check(!g.SupperCooked, "A remote stale kitchen callback cannot prepare the supper");
            SupperCook(g); Check(g.SupperPreparedCount == 3 && g.SupperServedCount == 0 && SupperEconomy(g) == before,
                "Cooking all contributions opens service without instant stat/timer/economy changes");
            for (int i = 0; i < 3; i++)
            {
                WorldObject bowl = Object(g, "supperBowl" + i); Check(g.IsObjectPresent(bowl), "The authored cooked bowl is present");
                SupperStand(g, "cook"); SupperReplay(g, "supper_serve_" + i);
                Check(!g.SupperServed(i), "Kitchen proximity cannot remotely serve a different table: " + i);
                SupperStand(g, bowl.kind);
                var forged = new WorldObject { id = bowl.id, kind = bowl.kind, name = bowl.name, en = bowl.en, x = bowl.x, y = bowl.y };
                g.Talk(forged);
                Check(g.conversation == null || SupperChoiceIndex(g, "supper_serve_" + i) < 0,
                    "A copied ID/kind/coordinate still cannot impersonate the canonical map-object reference");
                g.Close(); SupperServe(g, i); SupperReplay(g, "supper_serve_" + i);
                Check(g.SupperServedCount == i + 1 && SupperEconomy(g) == before, "Distinct table progress is once-only and cannot grant reward early");
            }
            SupperStand(g, "supperBowl2"); SupperReplay(g, "supper_finish"); Check(!g.SupperComplete, "The final handoff must return to Ari");
            g.Enter(0); SupperStand(g, "cook"); SupperReplay(g, "supper_finish"); Check(!g.SupperComplete, "The exterior cook cannot claim an indoor event remotely");
            g.Enter(9); SupperStand(g, "cook"); g.practicing = true; SupperReplay(g, "supper_finish");
            Check(!g.SupperComplete && SupperEconomy(g) == before, "Training cannot turn a prepared meal into a real reward");
        }

        public static void SupperAllProfessionsAndPreparationServiceOrdersFinishOnce(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            foreach (int[] prepare in supperOrders)
            foreach (int[] serve in supperOrders)
            {
                var g = SupperGame(data, profession, true); string before = SupperEconomy(g);
                foreach (int i in prepare) SupperPrepare(g, i);
                SupperCook(g); foreach (int i in serve) SupperServe(g, i);
                Check(SupperEconomy(g) == before && !g.SupperComplete, "Every class/order prepares and serves without prematurely paying");
                int stew = g.save.inventory[6], tea = g.save.tea; SupperFinish(g);
                Check(g.SupperComplete && g.SupperPreparedCount == 3 && g.SupperServedCount == 3 &&
                    g.save.inventory[6] == stew + 2 && g.save.tea == tea + 1,
                    "Each of 108 class/order journeys receives exactly two stew and one tea");
                g.save.inventory[6] -= 2; g.save.tea--;
                Check(SupperEconomy(g) == before, "The earned supplies are the only economy/stat/timer change");
                g.save.inventory[6] += 2; g.save.tea++;
                Check(g.save.moments.Exists(m => m.zh.Contains("共餐") && m.en.Length > 0), "The earned closing moment retains both translations");
                string complete = SupperEconomy(g); int moments = g.save.moments.Count;
                SupperReplay(g, "supper_finish"); SupperReplay(g, "supper_cook"); SupperReplay(g, "supper_accept");
                Check(g.SupperComplete && SupperEconomy(g) == complete && g.save.moments.Count == moments,
                    "Repeated and stale completion, cooking and acceptance cannot duplicate rewards or journal entries");
            }
        }

        public static void SupperEveryCheckpointAndLegacyVersionReloadKeepsEarnedProgress(Campaign data)
        {
            for (int version = 1; version <= 2; version++)
            {
                var g = SupperGame(data, 2, true);
                g.save.flags.Add("guild_letter_reviewed"); g.save.flags.Add("homeward_public"); g.save.flags.Add("personal_1");
                for (int checkpoint = 0; checkpoint <= 8; checkpoint++)
                {
                    if (checkpoint >= 1 && checkpoint <= 3) SupperPrepare(g, checkpoint - 1);
                    if (checkpoint == 4) SupperCook(g);
                    if (checkpoint >= 5 && checkpoint <= 7) SupperServe(g, checkpoint - 5);
                    if (checkpoint == 8) SupperFinish(g);
                    int prepared = g.SupperPreparedCount, served = g.SupperServedCount;
                    bool cooked = g.SupperCooked, complete = g.SupperComplete; string economy = SupperEconomy(g);
                    g.PrepareSave(); SaveData saved = CloneSave(g.save); saved.version = version;
                    g = new GameCore(data, saved); g.Start(); g.Close();
                    Check(g.SupperAccepted && g.SupperPreparedCount == prepared && g.SupperServedCount == served &&
                        g.SupperCooked == cooked && g.SupperComplete == complete && SupperEconomy(g) == economy,
                        "All earned supper checkpoints persist through a real v" + version + " reconstruction: " + checkpoint);
                    Check(g.save.profession == 2 && g.save.weapon == 2 && g.Has("guild_letter_reviewed") && g.Has("homeward_public") && g.Has("personal_1"),
                        "The new flag-owned story preserves identity, original equipment and unrelated mature stories");
                    if (complete)
                    {
                        SupperStand(g, "cook"); SupperReplay(g, "supper_finish");
                        Check(SupperEconomy(g) == economy, "Reloading a paid ending cannot pay a second time");
                    }
                }
            }
            foreach (int version in new[] { 1, 2 })
            {
                var old = Fresh(data); old.save.version = version; old.save.coins = 91; old.save.flags.Add("boss_1");
                var migrated = new GameCore(data, CloneSave(old.save)); migrated.Start(); migrated.Close();
                Check(migrated.save.coins == 91 && migrated.Has("boss_1") && !migrated.SupperAccepted && !migrated.SupperCooked && !migrated.SupperComplete,
                    "An actual unstarted old save receives no invented new progress or reward");
            }
        }

        private static void SupperGuideTarget(GameCore g, int room, string kind)
        {
            QuestGuide q = g.GetQuest("supper"); WorldObject target = Array.Find(g.data.maps[room].objects, o => o.kind == kind);
            Check(target != null && q.id == "supper" && !q.locked && !q.complete && q.room == room &&
                (q.target - new Point(target.x, target.y)).Length < .001f,
                "The live supper guide selects its next genuine interaction: " + kind);
        }

        public static void SupperGuidanceAppendsReadOnlyAndTargetsPhysicalMissingSteps(Campaign data)
        {
            string[] mature = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal", "archive", "reach", "homeward", "commission", "shelter", "fieldcraft" };
            Check(GameCore.QuestIds.Length >= mature.Length + 1 && GameCore.QuestIds[mature.Length] == "supper", "Supper retains its appended index when later stories follow");
            for (int i = 0; i < mature.Length; i++) Check(GameCore.QuestIds[i] == mature[i], "Every mature journal position stays stable: " + i);
            var g = SupperGame(data, 0, true);
            for (int i = 0; i < 3; i++) { SupperGuideTarget(g, 0, supperResidents[i]); SupperPrepare(g, i); }
            SupperGuideTarget(g, 9, "cook"); SupperCook(g);
            for (int i = 0; i < 3; i++) { SupperGuideTarget(g, 9, "supperBowl" + i); SupperServe(g, i); }
            SupperGuideTarget(g, 9, "cook"); string before = SupperEconomy(g); int revision = g.revision, flags = g.save.flags.Count, moments = g.save.moments.Count;
            for (int i = 0; i < 100; i++)
            {
                g.save.english = (i & 1) == 0; QuestGuide q = g.GetQuest("supper");
                Check(!string.IsNullOrEmpty(q.title) && !string.IsNullOrEmpty(q.next) && q.steps.Count >= 4,
                    "Both languages describe earned physical stages without mutating them");
                Check(g.NavigationTarget().HasValue, "The incomplete locally tracked final handoff remains navigable");
            }
            Check(SupperEconomy(g) == before && g.revision == revision && g.save.flags.Count == flags && g.save.moments.Count == moments,
                "Repeated presentation/guidance reads never progress, pay or touch the save");
            SupperFinish(g); Check(g.CurrentGuide.complete && g.CurrentGuide.Progress == 1 && !g.NavigationTarget().HasValue,
                "An earned shared meal completes its tracked task and clears the gold route");
        }

        public static void SupperLeavesMatureKitchenRestAndTownLifeAvailable(Campaign data)
        {
            var g = SupperGame(data, 1, true);
            for (int i = 0; i < 5; i++) g.save.inventory[i] = 20;
            SupperPrepareAll(g); SupperCook(g); SupperServe(g, 0);
            int served = g.SupperServedCount; SupperStand(g, "cook");
            Check(g.CookRoadMeal(2) && g.ActiveRoadMeal == 2, "The original optional kitchen still cooks during unfinished service");
            int stew = g.save.inventory[6]; SupperOpen(g, "cook", "cook");
            Check(g.save.inventory[6] == stew + 1 && g.ActiveRoadMeal == 2 && g.SupperServedCount == served,
                "Mature healing stew remains independent of the new shared table and preparation meal");
            float before = g.save.totalTime; SupperOpen(g, "rest", "rest");
            Check(g.save.totalTime == before + 45 && g.SupperCooked && g.SupperServedCount == served && !g.SupperComplete,
                "Inn rest advances its original clock without auto-serving, clearing or rewarding the supper");
            g.Enter(0); SupperOpen(g, "fish", "fish");
            Check(g.mode == "fish" && g.SupperCooked && g.SupperServedCount == served, "Original lake fishing remains playable during the shared-meal quest");
            g.Close(); g.Enter(9); SupperServe(g, 1); SupperServe(g, 2); SupperFinish(g);
            Check(g.SupperComplete && g.Has("ally_1") && g.Has("ally_2") && g.Has("ally_3") && g.ActiveRoadMeal == 2,
                "The new ending preserves original resident completion and the active kitchen recipe");
        }
    }
}
