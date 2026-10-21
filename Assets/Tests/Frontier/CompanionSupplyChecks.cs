using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore SupplyGame(Campaign data, bool english = false)
        {
            var game = Unlocked(data); game.SkipTutorial(); game.save.english = english;
            game.save.flags.Add("core_1"); game.save.flags.Add("core_2");
            game.save.inventory[1] = game.save.inventory[2] = game.save.inventory[4] = 0;
            return game;
        }
        private static WorldObject SupplyTarget(GameCore game, QuestGuide guide)
        {
            foreach (WorldObject obj in game.data.maps[guide.room].objects)
                if ((new Point(obj.x, obj.y) - guide.target).Length < .001f) return obj;
            throw new Exception("The companion guide must resolve to an authored map object");
        }
        private static void SupplyNode(GameCore game, QuestGuide guide, string kind)
        {
            WorldObject obj = SupplyTarget(game, guide);
            Check(obj.kind == kind && !game.Has("node_" + obj.id) && game.CanEnter(guide.room) && guide.room != 8,
                "A supply destination is an existing remaining accessible " + kind + " node");
        }
        private static void SupplyStand(GameCore game, WorldObject target)
        {
            for (int y = -6; y <= 6; y++) for (int x = -6; x <= 6; x++)
            {
                Point p = new Point(target.x + x * .2f, target.y + y * .2f);
                if (!game.CanStand(p)) continue;
                game.save.x = p.x; game.save.y = p.y;
                if (game.Nearest() == target) return;
            }
            throw new Exception("No actual standable E approach to " + target.kind);
        }
        private static void SupplyInteract(GameCore game, WorldObject target)
        {
            SupplyStand(game, target); game.Close(); game.Interact();
        }
        private static void SupplyChoose(GameCore game, string action)
        {
            Check(game.mode == "dialog" && game.conversation != null, "A real interaction opens the owned conversation");
            for (int i = 0; i < game.conversation.choices.Length; i++)
                if (game.conversation.choices[i].action == action) { game.Choose(i); return; }
            throw new Exception("Missing real companion action " + action);
        }
        private static string SupplySnapshot(GameCore game)
        {
            return LandmarkDescribe(game.save) + "/" + game.mode + "/" + game.revision + "/" +
                LandmarkDescribe(game.enemies) + "/" + LandmarkDescribe(game.projectiles) + "/" +
                LandmarkDescribe(game.effects) + "/" + LandmarkDescribe(game.hazards);
        }

        public static void CompanionSupplyIvoTracksEitherShortfallAndClampsBothCounters(Campaign data)
        {
            foreach (bool english in new[] { false, true }) foreach (int room in new[] { 0, 6 })
                foreach (int parts in new[] { 0, 3, 4, 9 }) foreach (int crystals in new[] { 0, 3, 4, 9 })
                {
                    var game = SupplyGame(data, english); game.Enter(room); game.enemies.Clear();
                    game.save.inventory[4] = parts; game.save.inventory[1] = crystals;
                    string before = SupplySnapshot(game); QuestGuide guide = game.GetQuest("personal1");
                    Check(!guide.locked && !guide.complete && guide.steps.Count == 2,
                        "Ivo's earned unfinished story retains its two existing steps");
                    string expected = english ? "Parts " + Math.Min(4, parts) + "/4 · Crystals " + Math.Min(4, crystals) + "/4"
                        : "零件 " + Math.Min(4, parts) + "/4 · 星晶 " + Math.Min(4, crystals) + "/4";
                    Check(guide.steps[0].text == expected && guide.steps[0].done == (parts >= 4 && crystals >= 4),
                        "Both independently required supplies expose clamped truthful counters: " + expected);
                    if (parts < 4 || crystals < 4)
                    {
                        SupplyNode(game, guide, "ore");
                        Check(guide.room == (room == 6 ? 6 : 1), "Prefer a real local ore node, otherwise Thornwood's existing source");
                        Check(guide.next == (english ? "Gather ore for crystals and parts" : "采集矿石，补充星晶与零件"),
                            "The next step explains both outputs of the existing ore interaction");
                    }
                    else Check(guide.room == 6 && SupplyTarget(game, guide).kind == "tinker",
                        "Meeting both costs immediately restores Ivo's original NPC destination");
                    Check(SupplySnapshot(game) == before, "Reading every threshold cannot grant supplies or change the world");
                }
        }

        public static void CompanionSupplySenFindsTheForestRecordBeforeGatheringHerbs(Campaign data)
        {
            foreach (bool english in new[] { false, true }) foreach (int room in new[] { 0, 7 })
                foreach (bool lore in new[] { false, true }) foreach (int herbs in new[] { 0, 3, 4, 9 })
                {
                    var game = SupplyGame(data, english); game.Enter(room); game.enemies.Clear(); game.save.inventory[2] = herbs;
                    if (lore) game.save.flags.Add("lore_1"); string before = SupplySnapshot(game);
                    QuestGuide guide = game.GetQuest("personal2");
                    string expected = english ? "Forest record and herbs " + Math.Min(4, herbs) + "/4"
                        : "密林记录与药草 " + Math.Min(4, herbs) + "/4";
                    Check(guide.steps[0].text == expected && guide.steps[0].done == (lore && herbs >= 4),
                        "Sen's counter keeps the record prerequisite and clamps carried herbs");
                    if (!lore) Check(guide.room == 1 && SupplyTarget(game, guide).kind == "lore",
                        "An unread original forest record always precedes herb gathering, even with enough herbs");
                    else if (herbs < 4)
                    {
                        SupplyNode(game, guide, "herb");
                        Check(guide.room == (room == 7 ? 7 : 1), "Read forest record switches to a real local herb or Thornwood source");
                        Check(guide.next == (english ? "Gather Moonherbs" : "采集月光草"), "The active next step identifies actual gathering");
                    }
                    else Check(guide.room == 7 && SupplyTarget(game, guide).kind == "keeper", "Both completed prerequisites restore Sen's original keeper destination");
                    Check(SupplySnapshot(game) == before, "Lore and supply previews cannot read the record or gather automatically");
                }
        }

        public static void CompanionSupplyActualGatherRecordAndOriginalPaymentsRemainConnected(Campaign data)
        {
            foreach (bool english in new[] { false, true }) for (int companion = 1; companion <= 2; companion++)
            {
                var game = SupplyGame(data, english); game.save.inventory[4] = 1; game.save.inventory[1] = 3; game.save.inventory[2] = 1;
                string id = "personal" + companion; game.TrackQuest(id);
                QuestGuide guide = game.CurrentGuide; game.Enter(guide.room); game.enemies.Clear();
                WorldObject first = SupplyTarget(game, guide); SupplyInteract(game, first);
                if (companion == 2)
                {
                    Check(first.kind == "lore" && game.Has("lore_1"), "Only actual E on the original forest record records Sen's prerequisite");
                    SupplyChoose(game, "close"); guide = game.CurrentGuide; SupplyNode(game, guide, "herb");
                    game.Enter(guide.room); game.enemies.Clear(); first = SupplyTarget(game, guide); SupplyInteract(game, first);
                }
                Check(first.kind == (companion == 1 ? "ore" : "herb") && game.Has("node_" + first.id),
                    "The original real gathering interaction owns depletion and the three-item yield");
                Check(game.save.inventory[4] == (companion == 1 ? 4 : 1) && game.save.inventory[2] == (companion == 2 ? 4 : 1),
                    "Guidance preserves the authored ore parts and herb yields");
                guide = game.CurrentGuide; Check(guide.steps[0].done && guide.room == companion + 5,
                    "After actual gathering, the same tracked story returns to its companion");
                game.Enter(0); game.enemies.Clear();
                if (companion == 1) { SupplyInteract(game, Object(game, "forge")); SupplyChoose(game, "staff"); }
                else { game.save.inventory[3] = 1; SupplyInteract(game, Object(game, "cook")); SupplyChoose(game, "cook"); }
                Check(!game.CurrentGuide.steps[0].done, "Genuinely crafting a staff or cooking stew reopens the spent companion supply prerequisite");
                QuestGuide spent = game.CurrentGuide; SupplyNode(game, spent, companion == 1 ? "ore" : "herb");
                game.Enter(spent.room); game.enemies.Clear(); WorldObject replacement = SupplyTarget(game, spent); SupplyInteract(game, replacement);
                guide = game.CurrentGuide; Check(guide.steps[0].done && guide.room == companion + 5 && game.save.profession == 0,
                    "A newly gathered source restores turn-in without a stale ready counter or changing the protagonist's class");
                game.Enter(guide.room); game.enemies.Clear(); WorldObject npc = SupplyTarget(game, guide);
                Check(npc.kind == (companion == 1 ? "tinker" : "keeper"), "The original story NPC remains the turn-in destination");
                int coins = game.save.coins, shards = game.save.shards, points = game.save.skillPoints, bond = game.save.relationships[companion];
                int parts = game.save.inventory[4], crystals = game.save.inventory[1], herbs = game.save.inventory[2];
                SupplyInteract(game, npc); SupplyChoose(game, id);
                Check(game.Has("personal_" + companion) && game.save.coins == coins + 35 && game.save.shards == shards + 3 &&
                    game.save.skillPoints == points + 1 && game.save.relationships[companion] == Math.Min(5, bond + 2),
                    "The original mature personal story rewards remain 35 coins, three shards, one point and two bond");
                Check(game.save.inventory[4] == parts - (companion == 1 ? 4 : 0) && game.save.inventory[1] == crystals - (companion == 1 ? 4 : 0) &&
                    game.save.inventory[2] == herbs - (companion == 2 ? 4 : 0), "Turn-in pays exactly the original resources once");
                Check(game.save.trackedQuest == id && game.CurrentGuide.complete, "Payment completes the existing quest identity without replacing tracking");
                string after = SupplySnapshot(game); for (int i = 0; i < 5; i++) game.GetQuest(id);
                Check(SupplySnapshot(game) == after, "Completed-guide refreshes neither re-award nor consume supplies");
            }
        }

        public static void CompanionSupplyDepletedSourcesLeadToExistingRestAndRenewOnlyOnRest(Campaign data)
        {
            foreach (bool english in new[] { false, true }) for (int companion = 1; companion <= 2; companion++)
            {
                var game = SupplyGame(data, english); if (companion == 2) game.save.flags.Add("lore_1");
                string id = "personal" + companion, kind = companion == 1 ? "ore" : "herb";
                foreach (MapData map in data.maps) foreach (WorldObject obj in map.objects)
                    if (obj.kind == kind) game.save.flags.Add("node_" + obj.id);
                foreach (int room in new[] { 0, 6, 9 })
                {
                    game.Enter(room); game.enemies.Clear(); string before = SupplySnapshot(game); QuestGuide guide = game.GetQuest(id);
                    Check(guide.room == (room == 9 ? 9 : 0) && SupplyTarget(game, guide).kind == "rest",
                        "Only actual depleted sources redirect to the original town or inn rest interaction");
                    Check(guide.next == (english ? "Available nodes depleted; rest at the inn to renew them" : "可采资源已用完；到旅店休息更新"),
                        "Both languages explain the existing renewal mechanism honestly");
                    Check(before == SupplySnapshot(game), "Reading a depleted route cannot clear any node flag or advance time");
                }
                QuestGuide resting = game.GetQuest(id); float time = game.save.totalTime;
                SupplyInteract(game, SupplyTarget(game, resting)); SupplyChoose(game, "rest");
                Check(game.save.totalTime == time + 45 && !game.save.flags.Exists(flag => flag.StartsWith("node_", StringComparison.Ordinal)),
                    "The existing explicit inn rest, not navigation, renews all ordinary gathering nodes");
                QuestGuide renewed = game.GetQuest(id); SupplyNode(game, renewed, kind);
                Check(renewed.room == 1 && game.save.inventory[1] == 0 && game.save.inventory[2] == 0 && game.save.inventory[4] == 0,
                    "Renewal restores the real preferred source without granting supplies or paying a story reward");
            }
        }

        public static void CompanionSupplyPreservesLockedCompletedMiraPinsAndLegacyState(Campaign data)
        {
            for (int companion = 1; companion <= 2; companion++)
            {
                var game = SupplyGame(data); string id = "personal" + companion;
                if (companion == 2) game.save.flags.Add("lore_1"); game.save.flags.Remove("companion_" + companion);
                QuestGuide locked = game.GetQuest(id); Check(locked.locked && locked.room == companion + 5 && SupplyTarget(game, locked).kind == (companion == 1 ? "tinker" : "keeper"),
                    "A locked personal story retains its original companion destination without leaking resource guidance");
                game.save.flags.Add("companion_" + companion); game.save.flags.Add("personal_" + companion);
                QuestGuide complete = game.GetQuest(id); Check(complete.complete && complete.room == companion + 5 && SupplyTarget(game, complete).kind == (companion == 1 ? "tinker" : "keeper"),
                    "Completed stories stay at their original NPC even after their spent inventories fall below four");
                Check(complete.steps[0].done && complete.steps[0].text == (companion == 1 ? "零件 4/4 · 星晶 4/4" : "密林记录与药草 4/4"),
                    "Completed resource steps retain the earned four-of-four history after payment consumed the materials");
            }
            var mira = SupplyGame(data); QuestGuide manifest = mira.GetQuest("personal0");
            Check(manifest.room == 5 && SupplyTarget(mira, manifest).kind == "manifest", "Mira's untouched manifest story still guides its original object");
            mira.save.flags.Add("manifest_found"); Check(SupplyTarget(mira, mira.GetQuest("personal0")).kind == "captain", "Mira's earned manifest still returns to the original captain");
            for (int version = 1; version <= 2; version++)
            {
                var original = SupplyGame(data); original.save.version = version; original.save.explored[0] = 16777215;
                original.save.flags.Add("preserve_unknown_story"); original.save.flags.Add("preserve_unknown_story");
                original.mode = "landmarks"; WorldObject cook = Object(original, "cook"); Check(original.PinLandmark(cook), "An authored discovered personal pin can coexist with story tracking");
                original.TrackQuest("personal1"); original.Close(); SaveData saved = CloneSave(original.save);
                var loaded = new GameCore(data, saved); loaded.Close(); loaded.SkipTutorial();
                Check(loaded.save.trackedQuest == "personal1" && loaded.PinnedLandmark != null && loaded.PinnedLandmark.kind == "cook" &&
                    (loaded.NavigationTarget().Value - new Point(cook.x, cook.y)).Length < .001f,
                    "Legacy restoration preserves deliberate personal-pin precedence over the gathering quest");
                string before = SupplySnapshot(loaded); for (int i = 0; i < 5; i++) { loaded.GetQuest("personal1"); loaded.GetQuest("personal2"); loaded.NavigationTarget(); }
                Check(before == SupplySnapshot(loaded) && loaded.save.flags.FindAll(flag => flag == "preserve_unknown_story").Count == 2,
                    "Repeated guide reads retain every save field, mature unknown flag, entity and revision");
                loaded.mode = "landmarks"; Check(loaded.ClearLandmarkPin(), "Clearing the pin remains an explicit existing action"); loaded.Close();
                WorldObject portal = Object(loaded, "portal1"); Check((loaded.NavigationTarget().Value - new Point(portal.x, portal.y)).Length < .001f,
                    "Clearing the personal pin resumes the original cross-map quest portal without changing its tracked identity");
            }
        }
    }
}
